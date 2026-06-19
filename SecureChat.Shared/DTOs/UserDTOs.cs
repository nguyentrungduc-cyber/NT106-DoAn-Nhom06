using System.ComponentModel.DataAnnotations;
using SecureChat.Models;

namespace SecureChat.DTOs
{
	public record UpdateProfileRequest(
		[MaxLength(32)] string? DisplayName,
		[MaxLength(64)] string? Email,
		[MaxLength(32)] string? Username,
		string? BioText
	);

	public record UpdateHashedPasswordRequest(
		[Required] string OldHashedPassword,
		[Required] string NewHashedPassword,
		[Required] string NewHashedBKey,
		[Required] string NewKeySalt,
		[Required] string NewPublicKey
	);

	public record UpdateAvatarRequest(
		[Required] string AvatarURL
	);

	public record UpdatePublicKeyRequest(
		[Required] string PublicKey
	);

	public record UserResponse(
		string UserID,
		string Username,
		string DisplayName,
		string Email,
		string? AvatarURL,
		string? BioText,
		bool ShowReadStatus,
		bool ShowOnlineStatus,
		DateTime? LastSeenUtc,
		string HashedBKey,
		string HashedRecoveryKey,
		string KeySalt,
		string PublicKey,
		DateTime CreatedAt,
		DateTime UpdatedAt,
		bool IsOnline = false
	)
	{

		public static UserResponse From(User u) => new (
			u.UserID, u.Username, u.DisplayName,
			u.Email, u.AvatarURL, u.BioText,
			u.ShowReadStatus, u.ShowOnlineStatus, u.LastSeenUtc,
			u.HashedBKey, u.HashedRecoveryKey,
			u.KeySalt, u.PublicKey, u.CreatedAt, u.UpdatedAt
		);

		public static UserResponse From(User u, bool isOnline) => new (
			u.UserID, u.Username, u.DisplayName,
			u.Email, u.AvatarURL, u.BioText,
			u.ShowReadStatus, u.ShowOnlineStatus, u.LastSeenUtc,
			u.HashedBKey, u.HashedRecoveryKey,
			u.KeySalt, u.PublicKey, u.CreatedAt, u.UpdatedAt,
			isOnline
		);
	}

	public record SessionResponse(
		string SessionID,
		string DeviceName,
		DateTime CreatedAt,
		DateTime ExpiresAt
	);

	public record UpdatePrivacyRequest(
		bool ShowReadStatus,
		bool ShowOnlineStatus
	);

	public record PrivacySettingsResponse(
		PrivacyLevel LastSeenPrivacy,
		PrivacyLevel ProfilePhotoPrivacy,
		PrivacyLevel ForwardedMessagesPrivacy,
		PrivacyLevel CallsPrivacy,
		PrivacyLevel VoiceMessagesPrivacy,
		PrivacyLevel MessagesPrivacy,
		PrivacyLevel BirthdayPrivacy,
		PrivacyLevel BioPrivacy,
		AutoDeleteMode AutoDeleteMode
	);

	public record UpdatePrivacySettingsRequest(
		PrivacyLevel? LastSeenPrivacy,
		PrivacyLevel? ProfilePhotoPrivacy,
		PrivacyLevel? ForwardedMessagesPrivacy,
		PrivacyLevel? CallsPrivacy,
		PrivacyLevel? VoiceMessagesPrivacy,
		PrivacyLevel? MessagesPrivacy,
		PrivacyLevel? BirthdayPrivacy,
		PrivacyLevel? BioPrivacy,
		AutoDeleteMode? AutoDeleteMode
	);
}
