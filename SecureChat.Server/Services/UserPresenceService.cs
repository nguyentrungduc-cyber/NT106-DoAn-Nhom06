using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecureChat.Models;
using SecureChat.Server.Hubs;

namespace SecureChat.Server.Services
{
	public sealed class UserPresenceService
	{
		private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly IHubContext<ChatHub> _hubContext;
		private readonly ILogger<UserPresenceService> _logger;

		public UserPresenceService(
			IServiceScopeFactory scopeFactory,
			IHubContext<ChatHub> hubContext,
			ILogger<UserPresenceService> logger)
		{
			_scopeFactory = scopeFactory;
			_hubContext = hubContext;
			_logger = logger;
		}

		// ── In-memory connection tracking ──
		public bool IsOnline(string userId)
		{
			if (!_connections.TryGetValue(userId, out var conns))
				return false;
			lock (conns) { return conns.Count > 0; }
		}

		public int ConnectionCount(string userId)
		{
			if (!_connections.TryGetValue(userId, out var conns))
				return 0;
			lock (conns) { return conns.Count; }
		}

		public IEnumerable<string> GetAllOnlineUsers()
		{
			foreach (var kvp in _connections)
			{
				bool hasAny;
				lock (kvp.Value) { hasAny = kvp.Value.Count > 0; }
				if (hasAny) yield return kvp.Key;
			}
		}

		// ── Lifecycle ──

		/// <summary>Called when a new SignalR connection is established.</summary>
		public async Task UserConnectedAsync(string userId, string connectionId)
		{
			var conns = _connections.GetOrAdd(userId, _ => new HashSet<string>());
			bool wasOffline;
			lock (conns)
			{
				wasOffline = conns.Count == 0;
				conns.Add(connectionId);
			}

			_logger.LogInformation("UserPresence: {UserId} connected ({ConnectionId}), wasOffline={WasOffline}",
				userId, connectionId, wasOffline);

			if (!wasOffline) return; // already online — no broadcast needed

			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			var row = await db.UserPresences.FindAsync(userId);
			if (row == null)
			{
				db.UserPresences.Add(new UserPresence
				{
					UserID = userId,
					Status = UserStatus.Online,
					LastSeenUtc = null,
					ActiveSessionCount = 1
				});
			}
			else
			{
				row.Status = UserStatus.Online;
				row.ActiveSessionCount++;
				row.LastSeenUtc = null;
			}
			await db.SaveChangesAsync();

			// Check ShowOnlineStatus
			var user = await db.Users.FindAsync(userId);
			if (user?.ShowOnlineStatus == true)
				await BroadcastStatusAsync(userId, UserStatus.Online, null);
		}

		/// <summary>Called when a SignalR connection disconnects.</summary>
		public async Task UserDisconnectedAsync(string userId, string connectionId)
		{
			if (!_connections.TryGetValue(userId, out var conns))
			{
				_logger.LogWarning("UserPresence: {UserId} disconnect but no connections tracked", userId);
				return;
			}

			bool becameEmpty;
			lock (conns)
			{
				conns.Remove(connectionId);
				becameEmpty = conns.Count == 0;
				if (becameEmpty) _connections.TryRemove(userId, out _);
			}

			_logger.LogInformation("UserPresence: {UserId} disconnected ({ConnectionId}), becameEmpty={BecameEmpty}",
				userId, connectionId, becameEmpty);

			if (!becameEmpty) return; // other sessions still active — no broadcast

			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			var row = await db.UserPresences.FindAsync(userId);
			if (row != null)
			{
				row.ActiveSessionCount = Math.Max(0, row.ActiveSessionCount - 1);
				if (row.ActiveSessionCount == 0)
				{
					row.Status = UserStatus.Offline;
					row.LastSeenUtc = DateTime.UtcNow;

					var user = await db.Users.FindAsync(userId);
					if (user != null)
					{
						user.LastSeenUtc = DateTime.UtcNow;
						await db.SaveChangesAsync();

						if (user.ShowOnlineStatus)
							await BroadcastStatusWithPrivacyAsync(userId, user);
					}
				}
				else
				{
					await db.SaveChangesAsync();
				}
			}
		}

		/// <summary>Force a user offline immediately (e.g. logout, session revoke).</summary>
		public async Task ForceOfflineAsync(string userId)
		{
			_logger.LogInformation("UserPresence: ForceOffline {UserId}", userId);

			// Remove all in-memory connections
			if (_connections.TryRemove(userId, out var conns))
			{
				lock (conns) { conns.Clear(); }
			}

			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			var row = await db.UserPresences.FindAsync(userId);
			if (row != null)
			{
				row.Status = UserStatus.Offline;
				row.ActiveSessionCount = 0;
				row.LastSeenUtc = DateTime.UtcNow;
			}
			else
			{
				db.UserPresences.Add(new UserPresence
				{
					UserID = userId,
					Status = UserStatus.Offline,
					ActiveSessionCount = 0,
					LastSeenUtc = DateTime.UtcNow
				});
			}
			await db.SaveChangesAsync();

			var user = await db.Users.FindAsync(userId);
			if (user != null)
			{
				user.LastSeenUtc = DateTime.UtcNow;
				await db.SaveChangesAsync();

				if (user.ShowOnlineStatus)
					await BroadcastStatusWithPrivacyAsync(userId, user);
			}
		}

		// ── Broadcast helpers ──

		private async Task BroadcastStatusAsync(string userId, UserStatus status, DateTime? lastSeenUtc)
		{
			var statusStr = status switch
			{
				UserStatus.Online => "Online",
				UserStatus.Idle => "Idle",
				_ => "Offline"
			};

			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var myConvs = await db.ConversationMembers
				.Where(m => m.UserID == userId && m.LeftAt == null)
				.Select(m => m.ConversationID)
				.Distinct()
				.ToListAsync();

			foreach (var convId in myConvs)
			{
				await _hubContext.Clients.Group(convId)
					.SendAsync("UserStatusChanged", userId, statusStr, lastSeenUtc);
			}
		}

		private async Task BroadcastStatusWithPrivacyAsync(string userId, User user)
		{
			var settings = await GetPrivacySettingsAsync(userId);
			DateTime? lastSeen = user.LastSeenUtc;
			if (settings is not null && settings.LastSeenPrivacy != PrivacyLevel.Everybody)
				lastSeen = null;

			await BroadcastStatusAsync(userId, UserStatus.Offline, lastSeen);
		}

		private async Task<UserPrivacySettings?> GetPrivacySettingsAsync(string userId)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			return await db.UserPrivacySettings.FindAsync(userId);
		}
	}
}
