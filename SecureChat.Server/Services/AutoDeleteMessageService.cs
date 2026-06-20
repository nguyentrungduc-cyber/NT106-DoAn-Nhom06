using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Server.Services
{
	public sealed class AutoDeleteMessageService(IServiceProvider serviceProvider, ILogger<AutoDeleteMessageService> logger) : BackgroundService
	{
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			logger.LogInformation("AutoDeleteMessageService started");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await DeleteExpiredMessagesAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "Error in auto-delete cycle");
				}

				await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
			}
		}

		private async Task DeleteExpiredMessagesAsync(CancellationToken ct)
		{
			using var scope = serviceProvider.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

			var now = DateTime.UtcNow;

			// Messages with individual ExpiresAt set
			var individualExpired = await db.Messages
				.Where(m => m.ExpiresAt != null && m.ExpiresAt <= now && m.DeletedAt == null)
				.ToListAsync(ct);

			foreach (var msg in individualExpired)
			{
				msg.DeletedAt = now;
			}

			// Messages in conversations where the sender has auto-delete enabled
			var autoDeleteUsers = await db.UserPrivacySettings
				.Where(p => p.AutoDeleteMode != AutoDeleteMode.Off)
				.ToListAsync(ct);

			foreach (var setting in autoDeleteUsers)
			{
				var cutoff = setting.AutoDeleteMode switch
				{
					AutoDeleteMode.TwentyFourHours => now.AddHours(-24),
					AutoDeleteMode.SevenDays => now.AddDays(-7),
					AutoDeleteMode.ThirtyDays => now.AddDays(-30),
					_ => now
				};

				// Find messages sent by this user (via conversation membership)
				var memberIDs = await db.ConversationMembers
					.Where(m => m.UserID == setting.UserID && m.LeftAt == null)
					.Select(m => m.MemberID)
					.ToListAsync(ct);

				if (memberIDs.Count == 0)
					continue;

				var expired = await db.Messages
					.Where(m => memberIDs.Contains(m.SenderID!)
						&& m.SentAt <= cutoff
						&& m.DeletedAt == null
						&& m.Type != MessageType.Call
						&& m.Type != MessageType.SystemNotification)
					.ToListAsync(ct);

				foreach (var msg in expired)
				{
					msg.DeletedAt = now;
				}
			}

			if (individualExpired.Count > 0 || autoDeleteUsers.Count > 0)
			{
				await db.SaveChangesAsync(ct);
				logger.LogInformation("Auto-deleted {Count} expired messages", individualExpired.Count + autoDeleteUsers.Sum(a => 0));
			}
		}
	}
}
