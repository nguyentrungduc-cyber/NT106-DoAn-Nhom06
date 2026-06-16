using System.ComponentModel.DataAnnotations;
using SecureChat.Models;

namespace SecureChat.DTOs
{
	public record CreateConversationRequest(
		[Required] ConversationType Type,
		[MaxLength(64)] string? Name,
		string? AvatarUrl,

		[Required, MinLength(2)]
		List<AddMemberEntry> Members
	);

	public record AddMemberEntry(
		[Required] string UserID,
		[Required] string EncryptedKey
	);

	public record UpdateConversationRequest(
		[MaxLength(64)] string? Name,
		string? AvatarUrl
	);

	public record AddMemberRequest(
		[Required] string UserID,
		[Required] string EncryptedKey,
		MemberRole Role = MemberRole.Member
	);

	public record UpdateMemberRequest(
		MemberRole? Role,
		[MaxLength(64)] string? Nickname,
		NotificationMode? ShowNotifications,
		DateTime? BannedUntil,
		string? EncryptedKey
	);

	public record ConversationResponse(
		string ConversationID,
		ConversationType Type,
		string? Name,
		string? AvatarURL,
		string? CreatedBy,
		string? LastMessageID,
		DateTime? LastActivityAt,
		DateTime CreatedAt,
		int MemberCount
	)
	{
		public static ConversationResponse From(Conversation c) => new(
			c.ConversationID, c.Type, c.Name, c.AvatarURL,
			c.CreatedBy, c.LastMessageID, c.LastActivityAt, c.CreatedAt,
			c.Members.Count);
	}

	public record MemberResponse(
		string MemberID,
		string ConversationID,
		string UserID,
		UserResponse? User,
		MemberRole Role,
		string? Nickname,
		string EncryptedKey,
		NotificationMode ShowNotifications,
		DateTime JoinedAt,
		DateTime? LeftAt,
		DateTime? BannedUntil,
		string? LastReadMsgID
	)
	{
		public static MemberResponse From(ConversationMember m) => new(
			m.MemberID, m.ConversationID, m.UserID,
			m.User != null ? UserResponse.From(m.User) : null,
			m.Role, m.Nickname, m.EncryptedKey,
			m.ShowNotifications, m.JoinedAt, m.LeftAt, m.BannedUntil, m.LastReadMsgID);
	}
}
