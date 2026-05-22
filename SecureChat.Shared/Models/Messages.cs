using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SecureChat.Models
{
	[Table("Messages")]
	public class Message
	{
		[Key, Column("message_id"), MaxLength(8)]
		public string MessageID { get; set; } = "";

		[Required, Column("conversation_id"), MaxLength(8)]
		public string ConversationID { get; set; } = "";

		[Column("original_sender_id"), MaxLength(8)]
		public string? OriginalSenderID { get; set; }

		[Column("sender_id"), MaxLength(8)]
		public string? SenderID { get; set; }

		[Column("reply_to_id"), MaxLength(8)]
		public string? ReplyToID { get; set; }

		[Column("message_type")]
		public MessageType Type { get; set; } = MessageType.Text;

		[Column("content")]
		public string? Content { get; set; }

		[Column("content_iv")]
		public string? ContentIV { get; set; }

		[Column("sent_at")]
		public DateTime SentAt { get; set; }

		[Column("deleted_at")]
		public DateTime? DeletedAt { get; set; }

	[Column("edited_at")]
	public DateTime? EditedAt { get; set; }

	[Column("expires_at")]
	public DateTime? ExpiresAt { get; set; }

	[ForeignKey(nameof(ConversationID)), InverseProperty(nameof(Conversation.Messages))]
	public Conversation Conversation { get; set; } = null!;

		[ForeignKey(nameof(SenderID)), InverseProperty(nameof(ConversationMember.SentMessages))]
		public ConversationMember? Sender { get; set; }

		[ForeignKey(nameof(OriginalSenderID))]
		public User? OriginalSender { get; set; }

		[ForeignKey(nameof(ReplyToID))]
		public Message? ReplyTo { get; set; }

		[InverseProperty(nameof(MessageAttachment.Message))]
		public ICollection<MessageAttachment> Attachments { get; set; } = [];

		[InverseProperty(nameof(MessageReaction.Message))]
		public ICollection<MessageReaction> Reactions { get; set; } = [];

		[InverseProperty(nameof(MessageMention.Message))]
		public ICollection<MessageMention> Mentions { get; set; } = [];
	}

	[Table("MessageAttachments")]
	public class MessageAttachment
	{
		[Key, Column("attachment_id"), MaxLength(8)]
		public string AttachmentID { get; set; } = "";

		[Required, Column("message_id"), MaxLength(8)]
		public string MessageID { get; set; } = "";

		[Required, Column("file_url")]
		public string FileURL { get; set; } = "";

		[Required, Column("file_name"),MaxLength(64)]
		public string FileName { get; set; } = "";

        [Required, Column("file_name_in_storage"), MaxLength(64)]
        public string FileNameInStorage { get; set; } = "";

        [Required, Column("file_type"), MaxLength(128)]
		public string FileType { get; set; } = "";

		[Required, Column("file_hash"),MaxLength(256)]
		public string FileHash { get; set; } = "";

		[Required, Column("file_size")]
		public long FileSize { get; set; }

		[Column("width")]
		public int? Width { get; set; }

		[Column("height")]
		public int? Height { get; set; }

		[Column("thumbnail_url")]
		public string? ThumbnailURL { get; set; }

		[Column("duration_secs")]
		public int? DurationSecs { get; set; }

		[Column("file_iv")]
		public string? FileIv { get; set; }

		[Column("thumbnail_iv")]
		public string? ThumbnailIv { get; set; }

		[Column("encrypted_aes_key"), MaxLength(1024)]
		public string? EncryptedAesKey { get; set; }

		[Column("encrypted_aes_iv"), MaxLength(1024)]
		public string? EncryptedAesIv { get; set; }

		[Column("receiver_id"), MaxLength(8)]
		public string? ReceiverId { get; set; }

		[Column("uploaded_at")]
		public DateTime UploadedAt { get; set; }

		[ForeignKey(nameof(MessageID)), InverseProperty(nameof(Message.Attachments))]
		public Message Message { get; set; } = null!;
	}

	[Table("MessagePins")]
	[PrimaryKey(nameof(MessageID), nameof(ConversationID))]
	public class MessagePin
	{
		[Column("message_id"), MaxLength(8)]
		public string MessageID { get; set; } = "";

		[Column("conversation_id"), MaxLength(8)]
		public string ConversationID { get; set; } = "";

		[Column("pinned_by"), MaxLength(8)]
		public string? PinnedBy { get; set; }

		[Column("pinned_at")]
		public DateTime PinnedAt { get; set; }

		[ForeignKey(nameof(MessageID))]
		public Message Message { get; set; } = null!;

		[ForeignKey(nameof(ConversationID)), InverseProperty(nameof(Conversation.PinnedMessages))]
		public Conversation Conversation { get; set; } = null!;

		[ForeignKey(nameof(PinnedBy))]
		public ConversationMember? Member { get; set; }
	}

	[Table("MessageReactions")]
	public class MessageReaction
	{
		[Key, Column("reaction_id"), MaxLength(8)]
		public string ReactionID { get; set; } = "";

		[Required, Column("message_id"), MaxLength(8)]
		public string MessageID { get; set; } = "";

		[Column("member_id"), MaxLength(8)]
		public string MemberID { get; set; } = "";

		[Required, Column("reaction"), MaxLength(8)]
		public string Reaction { get; set; } = "";

		[Column("created_at")]
		public DateTime CreatedAt { get; set; }

		[ForeignKey(nameof(MessageID)), InverseProperty(nameof(Message.Reactions))]
		public Message Message { get; set; } = null!;

		[ForeignKey(nameof(MemberID))]
		public ConversationMember? Member { get; set; }
	}

	[Table("MessageStatuses")]
	public class MessageStatus
	{
		[Key, Column("status_id"), MaxLength(8)]
		public string StatusID { get; set; } = "";

		[Required, Column("message_id"), MaxLength(8)]
		public string MessageID { get; set; } = "";

		[Required, Column("member_id"), MaxLength(8)]
		public string MemberID { get; set; } = "";

		[Column("delivered_at")]
		public DateTime? DeliveredAt { get; set; }

		[Column("read_at")]
		public DateTime? ReadAt { get; set; }

		[ForeignKey(nameof(MessageID))]
		public Message Message { get; set; } = null!;

		[ForeignKey(nameof(MemberID))]
		public ConversationMember? Member { get; set; } = null!;
	}

	[Table("MessageMentions")]
	[PrimaryKey(nameof(MessageID), nameof(MemberID))]
	public class MessageMention
	{
		[Column("message_id"), MaxLength(8)]
		public string MessageID { get; set; } = "";

		[Column("member_id"), MaxLength(8)]
		public string MemberID { get; set; } = "";

		[ForeignKey(nameof(MessageID)), InverseProperty(nameof(Message.Mentions))]
		public Message Message { get; set; } = null!;

		[ForeignKey(nameof(MemberID))]
		public ConversationMember Member { get; set; } = null!;
	}
}
