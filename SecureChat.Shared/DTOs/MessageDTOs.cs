using System.ComponentModel.DataAnnotations;
using SecureChat.Models;

namespace SecureChat.DTOs
{
    public record SendMessageRequest(
        [Required] MessageType Type,
        string? Content,
        string? ContentIV,
        string? ReplyToID,
        string? OriginalSenderID,
        List<CreateAttachmentRequest>? Attachments,
        List<string>? MentionedMemberIDs,
        int? ExpiresAfterSeconds = null
    );

    public record EditMessageRequest(
        [Required] string Content,
        [Required] string ContentIV
    );

    public record RecipientEncryption(
        [Required] string RecipientUserId,
        [Required] string EncryptedAesKey,
        [Required] string EncryptedAesIv
    );

    public record CreateAttachmentRequest(
        [Required] string FileURL,
        [Required, MaxLength(64)] string FileName,
         [Required, MaxLength(64)] string FileNameInStorage,
         [Required, MaxLength(128)] string FileType,
        [Required, MaxLength(256)] string FileHash,
        [Required] long FileSize,
        int? Width,
        int? Height,
        string? ThumbnailURL,
        int? DurationSecs,
        string? FileIV,
        string? ThumbnailIV,
        string? EncryptedAesKey = null,
        string? EncryptedAesIv = null,
        string? ReceiverId = null,
        List<RecipientEncryption>? RecipientEncryptions = null
    );

    public record AddReactionRequest([Required, MaxLength(8)] string Reaction);

    public record MessageResponse(
        string MessageID,
        string ConversationID,
        string? SenderID,
        string? SenderUsername,
        string? OriginalSenderID,
        string? ReplyToID,
        MessageType Type,
        string Content,
        string? ContentIV,
        DateTime SentAt,
        DateTime? EditedAt,
        DateTime? DeletedAt,
        DateTime? ExpiresAt,
        List<AttachmentResponse>? Attachments,
        List<ReactionResponse>? Reactions,
        List<string>? MentionedMemberIDs
    )
    {
        public static MessageResponse From(Message m) => new(
            m.MessageID, m.ConversationID,
            m.SenderID, m.Sender?.User.Username,
            m.OriginalSenderID, m.ReplyToID,
            m.Type, m.Content ?? "", m.ContentIV ?? "",
            m.SentAt, m.EditedAt, m.DeletedAt, m.ExpiresAt,
            m.Attachments.Select(AttachmentResponse.From).ToList(),
            m.Reactions.Select(ReactionResponse.From).ToList(),
            m.Mentions?.Select(mention => mention.MemberID).ToList()
        );
    }

    public record AttachmentResponse(
        string AttachmentID,
        string FileURL,
        string FileName,
        string FileNameInStorage,
        string FileType,
        string FileHash,
        long FileSize,
        int? Width,
        int? Height,
        string? ThumbnailURL,
        int? DurationSecs,
      DateTime UploadedAt,
         string? EncryptedAesKey = null,
         string? EncryptedAesIv = null,
         string? ReceiverId = null
    )
    {
        public static AttachmentResponse From(MessageAttachment a) => new(
            a.AttachmentID, a.FileURL, a.FileName, a.FileNameInStorage,
            a.FileType, a.FileHash, a.FileSize, a.Width, a.Height,
          a.ThumbnailURL, a.DurationSecs, a.UploadedAt,
             a.EncryptedAesKey, a.EncryptedAesIv, a.ReceiverId
        );
    }

    public record ReactionResponse(
        string ReactionID,
        string MessageID,
        string MemberID,
        string? MemberUsername,
        string Reaction,
        DateTime CreatedAt
    )
    {
        public static ReactionResponse From(MessageReaction r) => new(
            r.ReactionID, r.MessageID, r.MemberID,
            r.Member?.User?.Username, r.Reaction, r.CreatedAt
        );
    }

    public record PinResponse(
        string MessageID,
        string ConversationID,
        string? PinnedBy,
        DateTime PinnedAt
    )
    {
        public static PinResponse From(MessagePin p) => new(
            p.MessageID, p.ConversationID, p.PinnedBy, p.PinnedAt
        );
    }

    public record MessageStatusResponse(
        string StatusID,
        string MessageID,
        string MemberID,
        DateTime? DeliveredAt,
        DateTime? ReadAt
    )
    {
        public static MessageStatusResponse From(MessageStatus s) => new(
            s.StatusID, s.MessageID, s.MemberID,
            s.DeliveredAt, s.ReadAt
        );
    }

    public record UnreadCountResponse(string ConversationID, int Count);
}
