using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Repositories
{
	public class MessageRepository(AppDbContext db)
	{
		/*
		 * MESSAGE
		 */

		public async Task<Message> CreateAsync(Message message)
		{
			message.SentAt = DateTime.UtcNow;
			db.Messages.Add(message);
			await db.SaveChangesAsync();
			return message;
		}

		public async Task<Message?> GetByIdAsync(string messageID)
			=> await db.Messages
				.Include(m => m.Sender)
					.ThenInclude(s => s!.User)
				.Include(m => m.OriginalSender)
				.Include(m => m.ReplyTo)
				.Include(m => m.Attachments)
				.Include(m => m.Reactions)
				.Include(m => m.Mentions)
				.FirstOrDefaultAsync(m => m.MessageID == messageID);

		public async Task<List<Message>> GetByConversationAsync(string conversationID, int limit = 50, DateTime? before = null, DateTime? memberJoinedAt = null)
		{
			var query = db.Messages
				.Include(m => m.Sender)
					.ThenInclude(s => s!.User)
				.Include(m => m.OriginalSender)
				.Include(m => m.Attachments)
				.Include(m => m.Reactions)
				.Where(m => m.ConversationID == conversationID && m.DeletedAt == null && (m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow));

			if (before.HasValue)
				query = query.Where(m => m.SentAt < before.Value);

			// If member joined after some messages already existed (HistoryMode = Hidden),
			// filter out messages sent before they joined.
			if (memberJoinedAt.HasValue)
				query = query.Where(m => m.SentAt >= memberJoinedAt.Value);

			return await query.OrderByDescending(m => m.SentAt).Take(limit).ToListAsync();
		}

		public async Task<Message> EditAsync(string messageID, string newContent, string? newIV = null)
		{
			var message = await db.Messages.FindAsync(messageID)
				?? throw new KeyNotFoundException($"Không tìm thấy tin nhắn {messageID} not found.");

			message.Content   = newContent;
			message.ContentIV = newIV ?? message.ContentIV;
			message.EditedAt  = DateTime.UtcNow;
			await db.SaveChangesAsync();
			return message;
		}

		public async Task SoftDeleteAsync(string messageID)
		{
			var message = await db.Messages.FindAsync(messageID)
				?? throw new KeyNotFoundException($"Không tìm thấy tin nhắn {messageID} not found.");

			message.DeletedAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		public async Task RecallAsync(string messageID)
		{
			var message = await db.Messages.FindAsync(messageID)
				?? throw new KeyNotFoundException($"Không tìm thấy tin nhắn {messageID}.");

			message.RecalledAt = DateTime.UtcNow;
			await db.SaveChangesAsync();
		}

		public async Task DeleteAsync(string messageID)
		{
			var message = await db.Messages.FindAsync(messageID);
			if (message is null)
				return;

			db.Messages.Remove(message);
			await db.SaveChangesAsync();
		}

		public async Task DeleteAllByConversationAsync(string conversationID)
		{
			var messages = await db.Messages
				.Where(m => m.ConversationID == conversationID)
				.ToListAsync();
			if (messages.Count == 0)
				return;

			db.Messages.RemoveRange(messages);
			await db.SaveChangesAsync();
		}

		/*
		 * ATTACHMENTS
		 */

		public async Task<MessageAttachment> CreateAttachmentAsync(MessageAttachment attachment)
		{
			attachment.UploadedAt = DateTime.UtcNow;
			db.MessageAttachments.Add(attachment);
			await db.SaveChangesAsync();
			return attachment;
		}

		public async Task<MessageAttachment?> GetAttachmentByIdAsync(string attachmentID)
			=> await db.MessageAttachments
				.Include(a => a.Message)
				.FirstOrDefaultAsync(a => a.AttachmentID == attachmentID);

		public async Task<List<MessageAttachment>> GetAttachmentsByMessageAsync(string messageID)
			=> await db.MessageAttachments
				.Where(a => a.MessageID == messageID)
				.ToListAsync();

		public async Task<MessageAttachment?> GetAttachmentByHashAsync(string fileHash)
			=> await db.MessageAttachments
				.FirstOrDefaultAsync(a => a.FileHash == fileHash);

		public async Task DeleteAttachmentAsync(string attachmentID)
		{
			var attachment = await db.MessageAttachments.FindAsync(attachmentID);
			if (attachment is null)
				return;

			db.MessageAttachments.Remove(attachment);
			await db.SaveChangesAsync();
		}

		/*
		 * PINS
		 */

		public async Task<MessagePin> PinMessageAsync(MessagePin pin)
		{
			pin.PinnedAt = DateTime.UtcNow;
			db.MessagePins.Add(pin);
			await db.SaveChangesAsync();
			return pin;
		}

		public async Task<MessagePin?> GetPinAsync(string messageID, string conversationID)
			=> await db.MessagePins
				.Include(p => p.Message)
				.Include(p => p.Member)
				.FirstOrDefaultAsync(p => p.MessageID == messageID &&
				                          p.ConversationID == conversationID);

		public async Task<List<MessagePin>> GetPinsByConversationAsync(string conversationID)
			=> await db.MessagePins
				.Include(p => p.Message)
				.Include(p => p.Member)
				.Where(p => p.ConversationID == conversationID)
				.OrderByDescending(p => p.PinnedAt)
				.ToListAsync();

		public async Task UnpinMessageAsync(string messageID, string conversationID)
		{
			var pin = await db.MessagePins.FindAsync(messageID, conversationID);
			if (pin is null)
				return;

			db.MessagePins.Remove(pin);
			await db.SaveChangesAsync();
		}

		/*
		 * STATUS
		 */

		public async Task<MessageStatus> CreateStatusAsync(MessageStatus status)
		{
			db.MessageStatuses.Add(status);
			await db.SaveChangesAsync();
			return status;
		}

		public async Task<MessageStatus?> GetStatusAsync(string messageID, string memberID)
			=> await db.MessageStatuses
				.FirstOrDefaultAsync(s => s.MessageID == messageID && s.MemberID == memberID);

		public async Task<List<MessageStatus>> GetStatusesByMessageAsync(string messageID)
			=> await db.MessageStatuses
				.Include(s => s.Member)
					.ThenInclude(m => m!.User)
				.Where(s => s.MessageID == messageID)
				.ToListAsync();

		public async Task<MessageStatus> MarkDeliveredAsync(string messageID, string memberID)
		{
			var status = await GetStatusAsync(messageID, memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy trạng thái tin nhắn {messageID}/{memberID}.");

			status.DeliveredAt ??= DateTime.UtcNow;
			await db.SaveChangesAsync();
			return status;
		}

		public async Task<MessageStatus> MarkReadAsync(string messageID, string memberID)
		{
			var status = await GetStatusAsync(messageID, memberID)
				?? throw new KeyNotFoundException($"Không tìm thấy trạng thái tin nhắn {messageID}/{memberID}.");

			status.DeliveredAt ??= DateTime.UtcNow;
			status.ReadAt      ??= DateTime.UtcNow;
			await db.SaveChangesAsync();
			return status;
		}

		public async Task<int> GetUnreadCountAsync(string conversationID, string memberID)
		{
			var member = await db.ConversationMembers
				.Include(m => m.LastReadMessage)
				.FirstOrDefaultAsync(m => m.ConversationID == conversationID && m.MemberID == memberID);

			if (member is null)
				return 0;

			var query = db.Messages.Where(m => m.ConversationID == conversationID && m.DeletedAt == null && m.SenderID != memberID && (m.ExpiresAt == null || m.ExpiresAt > DateTime.UtcNow));

			if (member.LastReadMessage is not null)
				query = query.Where(m => m.SentAt > member.LastReadMessage.SentAt);

			return await query.CountAsync();
		}

		/*
		 * MENTIONS
		 */

		public async Task<MessageMention> AddMentionAsync(MessageMention mention)
		{
			db.MessageMentions.Add(mention);
			await db.SaveChangesAsync();
			return mention;
		}

		public async Task AddMentionsAsync(IEnumerable<MessageMention> mentions)
		{
			db.MessageMentions.AddRange(mentions);
			await db.SaveChangesAsync();
		}

		public async Task<List<MessageMention>> GetMentionsByMessageAsync(string messageID)
			=> await db.MessageMentions
				.Include(mm => mm.Member)
					.ThenInclude(m => m.User)
				.Where(mm => mm.MessageID == messageID)
				.ToListAsync();

		public async Task<List<MessageMention>> GetMentionsByMemberAsync(string memberID)
			=> await db.MessageMentions
				.Include(mm => mm.Message)
			 	.Where(mm => mm.MemberID == memberID)
				.OrderByDescending(mm => mm.Message.SentAt)
				.ToListAsync();

		public async Task DeleteMentionAsync(string messageID, string memberID)
		{
			var mention = await db.MessageMentions.FindAsync(messageID, memberID);
			if (mention is null)
				return;

			db.MessageMentions.Remove(mention);
			await db.SaveChangesAsync();
		}
	}
}
