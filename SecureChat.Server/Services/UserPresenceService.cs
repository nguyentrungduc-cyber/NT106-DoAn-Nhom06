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
		private readonly ConcurrentDictionary<string, object> _userLocks = new();
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

		private object GetUserLock(string userId) =>
			_userLocks.GetOrAdd(userId, _ => new object());

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
			int countBeforeAdd;
			lock (GetUserLock(userId))
			lock (conns)
			{
				// Re-check after acquiring lock (ForceOfflineAsync may have cleared the set)
				if (!_connections.TryGetValue(userId, out conns))
					conns = _connections.GetOrAdd(userId, _ => new HashSet<string>());
				countBeforeAdd = conns.Count;
				conns.Add(connectionId);
			}

			bool wasOffline = countBeforeAdd == 0;

			_logger.LogInformation("UserPresence: {UserId} connected ({ConnectionId}), wasOffline={WasOffline}",
				userId, connectionId, wasOffline);

			if (!wasOffline) return;

			try
			{
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
						ActiveSessionCount = conns.Count
					});
				}
				else
				{
					row.Status = UserStatus.Online;
					row.ActiveSessionCount = conns.Count;
					row.LastSeenUtc = null;
				}
				await db.SaveChangesAsync();

				var user = await db.Users.FindAsync(userId);
				if (user?.ShowOnlineStatus == true)
					await BroadcastStatusAsync(userId, UserStatus.Online, null);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UserPresence: DB write failed for UserConnectedAsync ({UserId})", userId);
				// Rollback in-memory state: remove the connection we just added
				lock (GetUserLock(userId))
				{
					if (_connections.TryGetValue(userId, out var rollbackConns))
					{
						lock (rollbackConns)
						{
							rollbackConns.Remove(connectionId);
							if (rollbackConns.Count == 0)
								_connections.TryRemove(userId, out _);
						}
					}
				}
			}
		}

		/// <summary>Called when a SignalR connection disconnects.</summary>
		public async Task UserDisconnectedAsync(string userId, string connectionId)
		{
			lock (GetUserLock(userId))
			{
				if (!_connections.TryGetValue(userId, out var conns))
				{
					_logger.LogWarning("UserPresence: {UserId} disconnect but no connections tracked", userId);
					return;
				}

				lock (conns)
				{
					conns.Remove(connectionId);
					if (conns.Count == 0) _connections.TryRemove(userId, out _);
				}
			}

			// Recalculate remaining count from the connection set (might have been modified by ForceOffline)
			_logger.LogInformation("UserPresence: {UserId} disconnected ({ConnectionId})", userId, connectionId);

			try
			{
				using var scope = _scopeFactory.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

				var row = await db.UserPresences.FindAsync(userId);
				if (row == null) return;

				int actualRemaining = ConnectionCount(userId);
				row.ActiveSessionCount = actualRemaining;

				if (actualRemaining > 0)
				{
					await db.SaveChangesAsync();
					return;
				}

				row.Status = UserStatus.Offline;
				row.LastSeenUtc = DateTime.UtcNow;

				var user = await db.Users.FindAsync(userId);
				if (user != null)
				{
					user.LastSeenUtc = DateTime.UtcNow;
					await db.SaveChangesAsync();

					await BroadcastStatusWithPrivacyAsync(userId, user);
				}
				else
				{
					await db.SaveChangesAsync();
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UserPresence: DB write failed for UserDisconnectedAsync ({UserId})", userId);
				// Rollback in-memory state: re-add the connection we removed
				lock (GetUserLock(userId))
				{
					var rollbackConns = _connections.GetOrAdd(userId, _ => new HashSet<string>());
					lock (rollbackConns)
					{
						rollbackConns.Add(connectionId);
					}
				}
			}
		}

		/// <summary>Force a user offline immediately (e.g. logout, session revoke).</summary>
		public async Task ForceOfflineAsync(string userId)
		{
			_logger.LogInformation("UserPresence: ForceOffline {UserId}", userId);

			lock (GetUserLock(userId))
			{
				if (_connections.TryRemove(userId, out var conns))
				{
					lock (conns) { conns.Clear(); }
				}
			}

			try
			{
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

					await BroadcastStatusWithPrivacyAsync(userId, user);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "UserPresence: DB write failed for ForceOfflineAsync ({UserId})", userId);
				// In-memory state is already cleared by ForceOffline; cannot fully rollback.
				// The next UserConnectedAsync will restore the DB state.
			}

			// Abort active SignalR connections so the user stops receiving events
			await ForceDisconnectAsync(userId);
		}

		/// <summary>Rebroadcast current presence when ShowOnlineStatus or LastSeenPrivacy changes.</summary>
		public async Task BroadcastCurrentStatusAsync(string userId)
		{
			bool online = IsOnline(userId);
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var user = await db.Users.FindAsync(userId);
			if (user is null) return;

			if (online)
			{
				if (user.ShowOnlineStatus)
					await BroadcastStatusAsync(userId, UserStatus.Online, null);
				else
					await BroadcastStatusAsync(userId, UserStatus.Offline, null);
			}
			else
			{
				await BroadcastStatusWithPrivacyAsync(userId, user);
			}
		}

		/// <summary>Send a signal to all active SignalR connections of this user to disconnect.</summary>
		public async Task ForceDisconnectAsync(string userId)
		{
			await _hubContext.Clients.User(userId)
				.SendAsync("ForceDisconnect", "Your session has ended. Please re-login.");
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
