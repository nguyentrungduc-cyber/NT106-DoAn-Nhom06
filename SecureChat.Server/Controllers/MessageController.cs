using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/conversations/{conversationID}/messages")]
	public class MessageController(MessageRepository messages, ConversationRepository conversations, FriendRepository friends) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

		async Task<ConversationMember?> GetActiveMember(string conversationID)
			=> (await conversations.GetMemberByConversationAndUserAsync(conversationID, Me)) is { LeftAt: null } m ? m : null;

		[HttpGet]
		public async Task<IActionResult> GetMessages(string conversationID, [FromQuery] int limit = 50, [FromQuery] DateTime? before = null)
		{
			if (await GetActiveMember(conversationID) is null)
				return Forbid();

			var list = await messages.GetByConversationAsync(conversationID, limit, before);
			return Ok(list.Select(MessageResponse.From));
		}

		[HttpGet("{messageID}")]
		public async Task<IActionResult> GetMessage(string conversationID, string messageID)
		{
			if (await GetActiveMember(conversationID) is null)
				return Forbid();

			var msg = await messages.GetByIdAsync(messageID);
			if (msg is null || msg.ConversationID != conversationID)
				return NotFound();

			return Ok(MessageResponse.From(msg));
		}

		[HttpPost]
		public async Task<IActionResult> SendMessage(string conversationID, [FromBody] SendMessageRequest req)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

		if (member.BannedUntil.HasValue && member.BannedUntil > DateTime.UtcNow)
			return BadRequest(new { error = "Bạn đang bị cấm gửi tin nhắn." });

		// Calculate ExpiresAt if ExpiresAfterSeconds is provided
		DateTime? expiresAt = null;
		if (req.ExpiresAfterSeconds.HasValue && req.ExpiresAfterSeconds.Value > 0)
		{
			expiresAt = DateTime.UtcNow.AddSeconds(req.ExpiresAfterSeconds.Value);
		}

		var msg = await messages.CreateAsync(new Message {
			MessageID        = NewID(),
			ConversationID   = conversationID,
			SenderID         = member.MemberID,
			OriginalSenderID = req.OriginalSenderID,
			ReplyToID        = req.ReplyToID,
			Type             = req.Type,
			Content          = req.Content,
			ContentIV        = req.ContentIV,
			ExpiresAt        = expiresAt
		});

			if (req.Attachments is not null)
			{
				foreach (var att in req.Attachments)
				{
					// If RecipientEncryptions is provided, create one attachment per recipient
					// Otherwise, use the single-recipient format (backward compatibility)
					if (att.RecipientEncryptions is not null && att.RecipientEncryptions.Count > 0)
					{
						foreach (var recipientEnc in att.RecipientEncryptions)
						{
							await messages.CreateAttachmentAsync(new MessageAttachment
							{
								AttachmentID = NewID(),
								MessageID = msg.MessageID,
								FileURL = att.FileURL,
								FileName = att.FileName,
								FileNameInStorage = att.FileNameInStorage,
								FileType = att.FileType,
								FileHash = att.FileHash,
								FileSize = att.FileSize,
								Width = att.Width,
								Height = att.Height,
								ThumbnailURL = att.ThumbnailURL,
								DurationSecs = att.DurationSecs,
								FileIv = att.FileIV,
								ThumbnailIv = att.ThumbnailIV,
								EncryptedAesKey = recipientEnc.EncryptedAesKey,
								EncryptedAesIv = recipientEnc.EncryptedAesIv,
								ReceiverId = recipientEnc.RecipientUserId
							});
						}
					}
					else
					{
						// Legacy single-recipient format
						await messages.CreateAttachmentAsync(new MessageAttachment
						{
							AttachmentID = NewID(),
							MessageID = msg.MessageID,
							FileURL = att.FileURL,
							FileName = att.FileName,
							FileNameInStorage = att.FileNameInStorage,
							FileType = att.FileType,
							FileHash = att.FileHash,
							FileSize = att.FileSize,
							Width = att.Width,
							Height = att.Height,
							ThumbnailURL = att.ThumbnailURL,
							DurationSecs = att.DurationSecs,
							FileIv = att.FileIV,
							ThumbnailIv = att.ThumbnailIV,
							EncryptedAesKey = att.EncryptedAesKey,
							EncryptedAesIv = att.EncryptedAesIv,
							ReceiverId = att.ReceiverId
						});
					}
				}
			}

			if (req.MentionedMemberIDs is not null)
				await messages.AddMentionsAsync(req.MentionedMemberIDs.Select(mid =>
					new MessageMention { MessageID = msg.MessageID, MemberID = mid }));

			await conversations.SetLastMessageAsync(conversationID, msg.MessageID, msg.SentAt);

			var activeMembers = await conversations.GetActiveMembersAsync(conversationID);

			// Lấy conversation để check loại (DM hay group)
			var conversation = await conversations.GetByIdAsync(conversationID);
			bool isDm = conversation?.IsGroup == false;

			foreach (var m in activeMembers.Where(m => m.MemberID != member.MemberID))
			{
				// Telegram-style block: với DM, nếu 1 trong 2 chặn nhau
				// thì KHÔNG tạo MessageStatus (không deliver) nhưng vẫn lưu DB
				// → người gửi thấy 1 tick mãi, không biết bị chặn
				if (isDm && await friends.IsBlockedEitherWayAsync(Me, m.UserID))
					continue;

				await messages.CreateStatusAsync(new MessageStatus {
					StatusID  = NewID(),
					MessageID = msg.MessageID,
					MemberID  = m.MemberID
				});
			}

			var loaded = await messages.GetByIdAsync(msg.MessageID);
			return CreatedAtAction(nameof(GetMessage), new { conversationID, messageID = msg.MessageID }, MessageResponse.From(loaded!));
		}

		[HttpPatch("{messageID}")]
		public async Task<IActionResult> EditMessage( string conversationID, string messageID, [FromBody] EditMessageRequest req)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var msg = await messages.GetByIdAsync(messageID);
			if (msg is null || msg.ConversationID != conversationID)
				return NotFound();
			if (msg.SenderID != member.MemberID)
				return Forbid();
			if (msg.DeletedAt is not null)
				return BadRequest(new { error = "Tin nhắn đã bị xóa." });

			var updated = await messages.EditAsync(messageID, req.Content, req.ContentIV);
			var loaded  = await messages.GetByIdAsync(updated.MessageID);

			return Ok(MessageResponse.From(loaded!));
		}

		[HttpDelete("{messageID}")]
		public async Task<IActionResult> DeleteMessage(string conversationID, string messageID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var msg = await messages.GetByIdAsync(messageID);
			if (msg is null || msg.ConversationID != conversationID)
				return NotFound();

			bool isOwner = member.Role >= MemberRole.Moderator;
			if (msg.SenderID != member.MemberID && !isOwner)
				return Forbid();

			await messages.SoftDeleteAsync(messageID);
			return NoContent();
		}

		[HttpGet("pins")]
		public async Task<IActionResult> GetPins(string conversationID)
		{
			if (await GetActiveMember(conversationID) is null)
				return Forbid();
			var pins = await messages.GetPinsByConversationAsync(conversationID);

			return Ok(pins.Select(PinResponse.From));
		}

		[HttpPost("{messageID}/pin")]
		public async Task<IActionResult> PinMessage(string conversationID, string messageID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var msg = await messages.GetByIdAsync(messageID);
			if (msg is null || msg.ConversationID != conversationID)
				return NotFound();

			var existing = await messages.GetPinAsync(messageID, conversationID);
			if (existing is not null)
				return Conflict(new { error = "Tin nhắn đã được ghim." });

			var pins = await messages.GetPinsByConversationAsync(conversationID);
			if (pins.Count >= 3)
				return BadRequest(new { error = "Chỉ được ghim tối đa 3 tin nhắn." });

			var pin = await messages.PinMessageAsync(new MessagePin {
				MessageID      = messageID,
				ConversationID = conversationID,
				PinnedBy       = member.MemberID
			});

			return Ok(PinResponse.From(pin));
		}

		[HttpDelete("{messageID}/pin")]
		public async Task<IActionResult> UnpinMessage(string conversationID, string messageID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			await messages.UnpinMessageAsync(messageID, conversationID);
			return NoContent();
		}

		[HttpPost("{messageID}/read")]
		public async Task<IActionResult> MarkRead(string conversationID, string messageID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			try {
				var status = await messages.MarkReadAsync(messageID, member.MemberID);
				await conversations.SetLastReadMessageAsync(member.MemberID, messageID);
				return Ok(MessageStatusResponse.From(status));
			} catch (KeyNotFoundException) {
				return NotFound();
			}
		}

		[HttpGet("unread")]
		public async Task<IActionResult> GetUnreadCount(string conversationID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			var count = await messages.GetUnreadCountAsync(conversationID, member.MemberID);
			return Ok(new UnreadCountResponse(conversationID, count));
		}
	}
}
