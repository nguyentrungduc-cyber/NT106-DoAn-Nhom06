using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Hubs;
using Microsoft.EntityFrameworkCore;
using System;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/conversations/{conversationID}/messages")]
	public class MessageController(
		MessageRepository messages,
		ConversationRepository conversations,
		PrivacyRepository privacy,
		FriendRepository friends,
		IHubContext<ChatHub> hub) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

		async Task<ConversationMember?> GetActiveMember(string conversationID)
			=> (await conversations.GetMemberByConversationAndUserAsync(conversationID, Me)) is { LeftAt: null } m ? m : null;

		[HttpGet]
		public async Task<IActionResult> GetMessages(string conversationID, [FromQuery] int limit = 50, [FromQuery] DateTime? before = null)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null) return Forbid();

			// Apply HistoryMode filter: if conversation has Hidden history mode,
			// only show messages sent after the member joined
			var conv = await conversations.GetByIdAsync(conversationID);
			DateTime? memberJoinedAt = conv?.HistoryMode == HistoryMode.Hidden ? member.JoinedAt : null;

			var list = await messages.GetByConversationAsync(conversationID, limit, before, memberJoinedAt);

			var result = new List<MessageResponse>();
			foreach (var msg in list)
			{
				// Forward privacy filter (Dev)
				bool hideForward = false;
				if (msg.OriginalSenderID is not null && msg.OriginalSenderID != Me)
				{
					var rawSettings = await privacy.GetRawSettingsAsync(msg.OriginalSenderID);
					if (rawSettings is not null)
					{
						bool isContact = await privacy.AreContactsAsync(Me, msg.OriginalSenderID);
						if (rawSettings.ForwardedMessagesPrivacy == PrivacyLevel.Nobody
							|| (rawSettings.ForwardedMessagesPrivacy == PrivacyLevel.Contacts && !isContact))
							hideForward = true;
					}
				}

				// Delivery status computation (Duck)
				DeliveryStatus delivery = DeliveryStatus.Sent;
				if (msg.SenderID == member.MemberID)
				{
					var statuses = await messages.GetStatusesByMessageAsync(msg.MessageID);
					if (statuses.Any(s => s.ReadAt.HasValue))
						delivery = DeliveryStatus.Read;
					else if (statuses.Any(s => s.DeliveredAt.HasValue))
						delivery = DeliveryStatus.Delivered;
				}

				result.Add(MessageResponse.From(msg, hideForward, delivery));
			}
			return Ok(result);
		}

		/// <summary>
		/// Endpoint nhẹ chỉ trả Delivery status mới nhất cho các tin nhắn DO CHÍNH MÌNH
		/// gửi trong conversation này. Dùng để refresh tick Sent/Delivered/Read mỗi lần
		/// mở lại conversation đã sync trước đó (không cần re-fetch toàn bộ history).
		/// Không có bước này, _syncedConversations chặn re-fetch full GetMessages
		/// nên tick bị "đứng" ở trạng thái cũ cho tới khi người gửi online lúc đối
		/// phương đọc tin (nhận realtime push qua SignalR).
		/// </summary>
		[HttpGet("delivery-status")]
		public async Task<IActionResult> GetDeliveryStatuses(string conversationID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null) return Forbid();

			var list = await messages.GetByConversationAsync(conversationID, limit: 200);
			var myMessages = list.Where(m => m.SenderID == member.MemberID).ToList();

			var result = new List<object>();
			foreach (var msg in myMessages)
			{
				var statuses = await messages.GetStatusesByMessageAsync(msg.MessageID);
				DeliveryStatus delivery = DeliveryStatus.Sent;
				if (statuses.Any(s => s.ReadAt.HasValue))
					delivery = DeliveryStatus.Read;
				else if (statuses.Any(s => s.DeliveredAt.HasValue))
					delivery = DeliveryStatus.Delivered;

				result.Add(new { messageID = msg.MessageID, delivery = delivery.ToString() });
			}

			return Ok(result);
		}

		[HttpPost("{messageID}/delivered")]
		public async Task<IActionResult> MarkDelivered(string conversationID, string messageID)
		{
			var member = await GetActiveMember(conversationID);
			if (member is null) return Forbid();

			await messages.MarkDeliveredAsync(messageID, member.MemberID);

			// Push SignalR về cho người gửi
			var msg = await messages.GetByIdAsync(messageID);
			if (msg is not null)
			{
				var senderMember = await conversations.GetMemberByIdAsync(msg.SenderID);
				if (senderMember is not null && senderMember.UserID != Me)
					await hub.Clients.User(senderMember.UserID)
						.SendAsync("MessageStatusUpdated", messageID, "Delivered");
			}

			return NoContent();
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
			if (req is null) return BadRequest(new { error = "Invalid request body." });
			var member = await GetActiveMember(conversationID);
			if (member is null)
				return Forbid();

			// For direct conversations, check if recipient allows messages from this sender
		var conv = await conversations.GetByIdWithMembersAsync(conversationID);
		if (conv?.Type == ConversationType.Direct)
		{
			var other = conv.Members.FirstOrDefault(m => m.UserID != Me && m.LeftAt == null);
			if (other is not null && !await privacy.CanSendMessageAsync(Me, other.UserID))
				return Forbid();

			// Also enforce VoiceMessagesPrivacy for audio messages
			if (req.Type == MessageType.Audio && other is not null && !await privacy.CanSendVoiceMessageAsync(Me, other.UserID))
				return Forbid();
		}

		if (member.BannedUntil.HasValue && member.BannedUntil > DateTime.UtcNow)
			return BadRequest(new { error = "Bạn đang bị cấm gửi tin nhắn." });

		// Calculate ExpiresAt if ExpiresAfterSeconds is provided
		DateTime? expiresAt = null;
		if (req.ExpiresAfterSeconds.HasValue && req.ExpiresAfterSeconds.Value > 0)
		{
			expiresAt = DateTime.UtcNow.AddSeconds(req.ExpiresAfterSeconds.Value);
		}

		// Wrap all DB writes in a transaction so LastMessage is never stale
		var strategy = conversations.DbContext.Database.CreateExecutionStrategy();
		return await strategy.ExecuteAsync<IActionResult>(async () =>
		{
			using var tx = await conversations.DbContext.Database.BeginTransactionAsync();

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

			var conversation = await conversations.GetByIdAsync(conversationID);
			bool isDm = conversation?.Type == ConversationType.Direct;

			foreach (var m in activeMembers.Where(m => m.MemberID != member.MemberID))
			{
				if (isDm && await friends.IsBlockedEitherWayAsync(Me, m.UserID))
					continue;

				await messages.CreateStatusAsync(new MessageStatus {
					StatusID  = NewID(),
					MessageID = msg.MessageID,
					MemberID  = m.MemberID
				});
			}

			await tx.CommitAsync();

			var loaded = await messages.GetByIdAsync(msg.MessageID);
			return CreatedAtAction(nameof(GetMessage), new { conversationID, messageID = msg.MessageID }, MessageResponse.From(loaded!));
		});
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
			var resp = MessageResponse.From(loaded!);

			try
			{
				await hub.Clients.Group(conversationID).SendAsync("MessageEdited", conversationID, resp);
			}
			catch { /* best-effort broadcast */ }

			return Ok(resp);
		}

		[HttpPost("{messageID}/recall")]
		public async Task<IActionResult> RecallMessage(string conversationID, string messageID)
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
			if (msg.RecalledAt is not null)
				return BadRequest(new { error = "Tin nhắn đã được thu hồi." });

			await messages.RecallAsync(messageID);
			var loaded = await messages.GetByIdAsync(messageID);
			var resp = MessageResponse.From(loaded!);

			try
			{
				await hub.Clients.Group(conversationID).SendAsync("MessageRecalled", resp);
			}
			catch { /* best-effort broadcast */ }

			return Ok(resp);
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

			try
			{
				await hub.Clients.Group(conversationID).SendAsync("MessageDeleted", conversationID, messageID);
			}
			catch { /* best-effort broadcast */ }

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

				// Push SignalR "Read" về cho người gửi
				var msg = await messages.GetByIdAsync(messageID);
				if (msg is not null)
				{
					var senderMember = await conversations.GetMemberByIdAsync(msg.SenderID);
					if (senderMember is not null && senderMember.UserID != Me)
						await hub.Clients.User(senderMember.UserID)
							.SendAsync("MessageStatusUpdated", messageID, "Read");
				}

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
