using Microsoft.EntityFrameworkCore;
using SecureChat.DTOs;
using SecureChat.Models;

namespace SecureChat.Repositories
{
	public class PrivacyRepository(AppDbContext db)
	{
		public async Task<UserPrivacySettings?> GetRawSettingsAsync(string userID)
		{
			return await db.UserPrivacySettings.AsNoTracking()
				.FirstOrDefaultAsync(p => p.UserID == userID);
		}

		public async Task<PrivacySettingsResponse?> GetSettingsAsync(string userID)
		{
			var settings = await db.UserPrivacySettings.AsNoTracking()
				.FirstOrDefaultAsync(p => p.UserID == userID);
			if (settings is null)
				return DefaultResponse();
			return ToResponse(settings);
		}

		public async Task<PrivacySettingsResponse> GetOrCreateSettingsAsync(string userID)
		{
			var settings = await db.UserPrivacySettings
				.FirstOrDefaultAsync(p => p.UserID == userID);
			if (settings is not null)
				return ToResponse(settings);

			settings = new UserPrivacySettings { UserID = userID };
			db.UserPrivacySettings.Add(settings);
			await db.SaveChangesAsync();
			return ToResponse(settings);
		}

		public async Task<PrivacySettingsResponse> UpdateSettingsAsync(string userID, UpdatePrivacySettingsRequest req)
		{
			var settings = await db.UserPrivacySettings
				.FirstOrDefaultAsync(p => p.UserID == userID);
			if (settings is null)
			{
				settings = new UserPrivacySettings { UserID = userID };
				db.UserPrivacySettings.Add(settings);
			}

			if (req.LastSeenPrivacy.HasValue)
				settings.LastSeenPrivacy = req.LastSeenPrivacy.Value;
			if (req.ProfilePhotoPrivacy.HasValue)
				settings.ProfilePhotoPrivacy = req.ProfilePhotoPrivacy.Value;
			if (req.ForwardedMessagesPrivacy.HasValue)
				settings.ForwardedMessagesPrivacy = req.ForwardedMessagesPrivacy.Value;
			if (req.CallsPrivacy.HasValue)
				settings.CallsPrivacy = req.CallsPrivacy.Value;
			if (req.VoiceMessagesPrivacy.HasValue)
				settings.VoiceMessagesPrivacy = req.VoiceMessagesPrivacy.Value;
			if (req.MessagesPrivacy.HasValue)
				settings.MessagesPrivacy = req.MessagesPrivacy.Value;
			if (req.BirthdayPrivacy.HasValue)
				settings.BirthdayPrivacy = req.BirthdayPrivacy.Value;
			if (req.BioPrivacy.HasValue)
				settings.BioPrivacy = req.BioPrivacy.Value;
			if (req.AutoDeleteMode.HasValue)
				settings.AutoDeleteMode = req.AutoDeleteMode.Value;

			settings.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
			return ToResponse(settings);
		}

		public async Task<bool> CanSendMessageAsync(string senderUserID, string recipientUserID)
		{
			var recipient = await db.UserPrivacySettings.AsNoTracking()
				.FirstOrDefaultAsync(p => p.UserID == recipientUserID);
			if (recipient is null)
				return true;
			if (recipient.MessagesPrivacy == PrivacyLevel.Nobody)
				return false;
			if (recipient.MessagesPrivacy == PrivacyLevel.Contacts)
			{
				var areFriends = await db.Friends.AnyAsync(f =>
					(f.UserAID == senderUserID && f.UserBID == recipientUserID) ||
					(f.UserAID == recipientUserID && f.UserBID == senderUserID));
				return areFriends;
			}
			return true;
		}

		public async Task<bool> CanSendVoiceMessageAsync(string senderUserID, string recipientUserID)
		{
			var recipient = await db.UserPrivacySettings.AsNoTracking()
				.FirstOrDefaultAsync(p => p.UserID == recipientUserID);
			if (recipient is null)
				return true;
			if (recipient.VoiceMessagesPrivacy == PrivacyLevel.Nobody)
				return false;
			if (recipient.VoiceMessagesPrivacy == PrivacyLevel.Contacts)
			{
				var areFriends = await db.Friends.AnyAsync(f =>
					(f.UserAID == senderUserID && f.UserBID == recipientUserID) ||
					(f.UserAID == recipientUserID && f.UserBID == senderUserID));
				return areFriends;
			}
			return true;
		}

		public async Task<bool> CanStartCallAsync(string callerUserID, string targetUserID)
		{
			var target = await db.UserPrivacySettings.AsNoTracking()
				.FirstOrDefaultAsync(p => p.UserID == targetUserID);
			if (target is null)
				return true;
			if (target.CallsPrivacy == PrivacyLevel.Nobody)
				return false;
			if (target.CallsPrivacy == PrivacyLevel.Contacts)
			{
				var areFriends = await db.Friends.AnyAsync(f =>
					(f.UserAID == callerUserID && f.UserBID == targetUserID) ||
					(f.UserAID == targetUserID && f.UserBID == callerUserID));
				return areFriends;
			}
			return true;
		}

		public async Task<bool> CanViewProfileAsync(string viewerUserID, string targetUserID)
		{
			var target = await db.UserPrivacySettings.AsNoTracking()
				.FirstOrDefaultAsync(p => p.UserID == targetUserID);
			if (target is null)
				return true;

			// Check all profile-related privacy: profile photo, birthday, bio, last seen
			return true; // granular checks done by the caller
		}

		public async Task<bool> AreContactsAsync(string userA, string userB)
		{
			return await db.Friends.AnyAsync(f =>
				(f.UserAID == userA && f.UserBID == userB) ||
				(f.UserAID == userB && f.UserBID == userA));
		}

		public async Task<string> GetPrivacyLabelAsync(string userID, PrivacyLevel setting)
		{
			return setting switch
			{
				PrivacyLevel.Everybody => "Everybody",
				PrivacyLevel.Contacts => "My contacts",
				PrivacyLevel.Nobody => "Nobody",
				_ => "Everybody"
			};
		}

		private static PrivacySettingsResponse DefaultResponse()
			=> new(PrivacyLevel.Everybody, PrivacyLevel.Everybody, PrivacyLevel.Everybody,
				   PrivacyLevel.Everybody, PrivacyLevel.Everybody, PrivacyLevel.Everybody,
				   PrivacyLevel.Everybody, PrivacyLevel.Everybody, AutoDeleteMode.Off);

		private static PrivacySettingsResponse ToResponse(UserPrivacySettings s)
			=> new(s.LastSeenPrivacy, s.ProfilePhotoPrivacy, s.ForwardedMessagesPrivacy,
				   s.CallsPrivacy, s.VoiceMessagesPrivacy, s.MessagesPrivacy,
				   s.BirthdayPrivacy, s.BioPrivacy, s.AutoDeleteMode);
	}
}
