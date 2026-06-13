using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SecureChat.Models
{
	[Table("Conversations")]
	public class Conversation
	{
		[Key, Column("conversation_id"), MaxLength(8)]
		public string ConversationID { get; set; } = "";

		[Column("conversation_type")]
		public ConversationType Type { get; set; } = ConversationType.Direct;

		[MaxLength(64)]
		public string? Name { get; set; }

		[Column("avatar_url")]
		public string? AvatarURL { get; set; }

		[Column("created_by"), MaxLength(8)]
		public string? CreatedBy { get; set; }

		[Column("last_message_id"), MaxLength(8)]
		public string? LastMessageID { get; set; }

		[Column("last_activity_at")]
		public DateTime? LastActivityAt { get; set; }

		[Column("created_at")]
		public DateTime CreatedAt { get; set; }

		[ForeignKey(nameof(CreatedBy))]
		public User? Creator { get; set; }

		[ForeignKey(nameof(LastMessageID))]
		public Message? LastMessage { get; set; }

		[InverseProperty(nameof(ConversationMember.Conversation))]
		public ICollection<ConversationMember> Members { get; set; } = [];

		[InverseProperty(nameof(Message.Conversation))]
		public ICollection<Message> Messages { get; set; } = [];

		[InverseProperty(nameof(MessagePin.Conversation))]
		public ICollection<MessagePin> PinnedMessages { get; set; } = [];
	}

	[Table("ConversationMembers")]
	public class ConversationMember
	{
		[Key, Column("member_id"), MaxLength(8)]
		public string MemberID { get; set; } = "";

		[Required, Column("conversation_id"), MaxLength(8)]
		public string ConversationID { get; set; } = "";

		[Required, Column("user_id"), MaxLength(8)]
		public string UserID { get; set; } = "";

		[Column("role")]
		public MemberRole Role { get; set; } = MemberRole.Member;

		[Column("nickname"), MaxLength(64)]
		public string? Nickname { get; set; }

		[Required, Column("encrypted_key")]
		public string EncryptedKey { get; set; } = "";

		[Column("joined_at")]
		public DateTime JoinedAt { get; set; }

		[Column("left_at")]
		public DateTime? LeftAt { get; set; }

		[Column("show_notifications")]
		public NotificationMode ShowNotifications { get; set; } = NotificationMode.All;

		[Column("banned_until")]
		public DateTime? BannedUntil { get; set; }

		[Column("last_read_msg_id"), MaxLength(8)]
		public string? LastReadMsgID { get; set; }

		[ForeignKey(nameof(ConversationID)), InverseProperty(nameof(Conversation.Members))]
		public Conversation Conversation { get; set; } = null!;

		[ForeignKey(nameof(UserID)), InverseProperty(nameof(User.ConversationMemberships))]
		public User User { get; set; } = null!;

		[ForeignKey(nameof(LastReadMsgID))]
		public Message? LastReadMessage { get; set; }

		[InverseProperty(nameof(Message.Sender))]
		public ICollection<Message> SentMessages { get; set; } = [];
		
		[InverseProperty(nameof(CallParticipant.Member))]
		public ICollection<CallParticipant> CallsJoined { get; set; } = [];
	}
}
