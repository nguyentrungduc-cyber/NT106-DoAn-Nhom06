using System.ComponentModel.DataAnnotations;
using SecureChat.Models;

namespace SecureChat.DTOs
{
	public record UpdateProfileRequest(
		[MaxLength(32)] string? DisplayName,
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
		string HashedBKey,
		string HashedRecoveryKey,
		string KeySalt,
		string PublicKey,
		DateTime CreatedAt,
		DateTime UpdatedAt
	)
	{

		public static UserResponse From(User u) => new (
			u.UserID, u.Username, u.DisplayName,
			u.Email, u.AvatarURL, u.BioText,
			u.ShowReadStatus, u.ShowOnlineStatus, u.HashedBKey, u.HashedRecoveryKey,
			u.KeySalt, u.PublicKey, u.CreatedAt, u.UpdatedAt
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
}
