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
		string? AvatarUrl,
		[MaxLength(1024)] string? Description = null,
		GroupVisibility? GroupType = null,
		HistoryMode? ChatHistoryMode = null
	);

	public record AddMemberRequest(
		[Required] string UserID,
		[Required] string EncryptedKey,
		MemberRole Role = MemberRole.Member
	);

	public record LeaveConversationRequest(
		string? NewOwnerMemberId = null
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
		int MemberCount,
		int AdminCount,
		int Version,
		string? Description = null,
		GroupVisibility? GroupType = null,
		HistoryMode? ChatHistoryMode = null,
		string? LastMessageContent = null,
		string? LastMessageSenderName = null,
		string? OtherUserId = null
	)
	{
		public static ConversationResponse From(Conversation c) => new(
			c.ConversationID, c.Type, c.Name, c.AvatarURL,
			c.CreatedBy, c.LastMessageID, c.LastActivityAt, c.CreatedAt,
			c.Members.Count(m => m.LeftAt == null),
			c.Members.Count(m => m.LeftAt == null && (m.Role == MemberRole.Owner || m.Role == MemberRole.Moderator)),
			c.Version,
			c.Description, c.GroupType, c.HistoryMode,
			c.LastMessage?.Content,
			c.LastMessage?.Sender?.User?.DisplayName);
	}

	public record ConversationViewResponse(
		ConversationResponse Metadata,
		List<MemberResponse> Members,
		List<MemberResponse> Admins
	);

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
		string? LastReadMsgID,
		bool IsOnline = false
	)
	{
		public static MemberResponse From(ConversationMember m, bool isOnline = false) => new(
			m.MemberID, m.ConversationID, m.UserID,
			m.User != null ? UserResponse.From(m.User) : null,
			m.Role, m.Nickname, m.EncryptedKey,
			m.ShowNotifications, m.JoinedAt, m.LeftAt, m.BannedUntil, m.LastReadMsgID,
			isOnline);
	}
}
