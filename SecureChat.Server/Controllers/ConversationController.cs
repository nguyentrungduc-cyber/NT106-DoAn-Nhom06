using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Hubs;
using SecureChat.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/conversations")]
	public class ConversationController(ConversationRepository conversations, UserRepository users, MessageRepository messages, PrivacyRepository privacy, IHubContext<ChatHub> hubContext, PresenceTracker presence, GroupLockService groupLock) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<IActionResult> GetMyConversations()
        {
            var list = await conversations.GetByUserAsync(Me);
            var result = list.Select(c =>
            {
                var res = ConversationResponse.From(c);

                // Với Direct conversation: dùng tên & avatar của người kia
                if (c.Type == ConversationType.Direct)
                {
                    var other = c.Members.FirstOrDefault(m => m.UserID != Me && m.LeftAt == null);
                    if (other?.User != null)
                    {
                        if (string.IsNullOrEmpty(res.Name))
                            res = res with { Name = other.User.DisplayName };
                        if (!string.IsNullOrWhiteSpace(other.User.AvatarURL))
                            res = res with { AvatarURL = other.User.AvatarURL };
                        res = res with { OtherUserId = other.UserID };
                    }
                }

                return res;
            });
            return Ok(result);
        }

		[HttpGet("{conversationID}")]
		public async Task<IActionResult> GetConversation(string conversationID)
		{
			var conv = await conversations.GetByIdWithMembersAsync(conversationID);
			if (conv is null)
				return NotFound();
			var member = conv.Members.FirstOrDefault(m => m.UserID == Me && m.LeftAt == null);
			if (member is null)
				return Forbid();

			var res = ConversationResponse.From(conv);
			if (conv.Type == ConversationType.Direct)
			{
				var other = conv.Members.FirstOrDefault(m => m.UserID != Me && m.LeftAt == null);
				if (other?.User != null)
				{
					if (string.IsNullOrEmpty(res.Name))
						res = res with { Name = other.User.DisplayName };
					if (!string.IsNullOrWhiteSpace(other.User.AvatarURL))
						res = res with { AvatarURL = other.User.AvatarURL };
					res = res with { OtherUserId = other.UserID };
				}
			}
			return Ok(res);
		}

		[HttpGet("{conversationID}/view")]
		public async Task<IActionResult> GetConversationView(string conversationID)
		{
			var conv = await conversations.GetByIdWithMembersAsync(conversationID);
			if (conv is null)
				return NotFound();
			var member = conv.Members.FirstOrDefault(m => m.UserID == Me && m.LeftAt == null);
			if (member is null)
				return Forbid();

			var metadata = ConversationResponse.From(conv);
			var activeMembers = conv.Members
				.Where(m => m.LeftAt == null)
				.ToList();
			var members = activeMembers
				.Select(m => MemberResponse.From(m, presence.IsOnline(m.UserID)))
				.ToList();
			var admins = activeMembers
				.Where(m => m.Role >= MemberRole.Moderator)
				.Select(m => MemberResponse.From(m, presence.IsOnline(m.UserID)))
				.ToList();

			return Ok(new ConversationViewResponse(metadata, members, admins));
		}

		[HttpGet("saved")]
		public async Task<IActionResult> GetSavedMessages()
		{
			var conv = await conversations.GetOrCreateSavedMessagesConversationAsync(Me);
			return Ok(ConversationResponse.From(conv));
		}

		[HttpPost]
		public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest req)
		{
			if (req.Type == ConversationType.SavedMessages)
				return BadRequest(new { error = "Cannot create Saved Messages through this endpoint." });

			foreach (var entry in req.Members)
				if (!await users.ExistsByIdAsync(entry.UserID))
					return BadRequest(new { error = $"User '{entry.UserID}' không tồn tại." });

			// Respect MessagesPrivacy for direct conversations
			if (req.Type == ConversationType.Direct)
			{
				var otherID = req.Members.FirstOrDefault(m => m.UserID != Me)?.UserID;
				if (otherID is not null && !await privacy.CanSendMessageAsync(Me, otherID))
					return Forbid();
			}

			if (req.Type == ConversationType.Direct)
			{
				var otherID = req.Members.FirstOrDefault(m => m.UserID != Me)?.UserID;
				if (otherID is not null) {
					var existing = await conversations.GetDirectConversationAsync(Me, otherID);
					if (existing is not null)
					{
						// Cập nhật EncryptedKey cho các member nếu cần
						var existingWithMembers = await conversations.GetByIdWithMembersAsync(existing.ConversationID);
						if (existingWithMembers is not null)
						{
							foreach (var entry in req.Members)
							{
								var member = existingWithMembers.Members.FirstOrDefault(m => m.UserID == entry.UserID && m.LeftAt == null);
								if (member is not null && member.EncryptedKey != entry.EncryptedKey)
								{
									await conversations.UpdateEncryptedKeyAsync(member.MemberID, entry.EncryptedKey);
								}
							}
						}
						return Ok(ConversationResponse.From(existing));
					}
				}
			}

			var conv = await conversations.CreateAsync(new Conversation {
				ConversationID = NewID(),
				Type           = req.Type,
				Name           = req.Name,
				AvatarURL      = req.AvatarUrl,
				CreatedBy      = Me
			});

			foreach (var entry in req.Members)
			{
				var role = entry.UserID == Me ? MemberRole.Owner : MemberRole.Member;
				await conversations.AddMemberAsync(new ConversationMember {
					MemberID       = NewID(),
					ConversationID = conv.ConversationID,
					UserID         = entry.UserID,
					EncryptedKey   = entry.EncryptedKey,
					Role           = role
				});
			}

			var loaded = await conversations.GetByIdWithMembersAsync(conv.ConversationID);

			// Notify all other members via SignalR about the new conversation
			try
			{
				foreach (var entry in req.Members)
				{
					if (entry.UserID != Me)
					{
						await hubContext.Clients.User(entry.UserID).SendAsync("ConversationCreated", conv.ConversationID);
					}
				}
			}
			catch { /* best-effort notification */ }

			return CreatedAtAction(nameof(GetConversation), new { conversationID = conv.ConversationID }, ConversationResponse.From(loaded!));
		}

		[HttpPatch("{conversationID}")]
		public async Task<IActionResult> UpdateConversation(string conversationID, [FromBody] UpdateConversationRequest req)
		{
			var conv = await conversations.GetByIdAsync(conversationID);
			if (conv is null)
				return NotFound();
			if (conv.Type == ConversationType.SavedMessages)
				return Forbid();

			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.LeftAt is not null)
				return Forbid();
			if (member.Role < MemberRole.Moderator)
				return Forbid();

			bool settingsChanged = false;

			// Name / Avatar (legacy settings)
			if (req.Name is not null)
				conv.Name = req.Name;
			if (req.AvatarUrl is not null)
				conv.AvatarURL = req.AvatarUrl;

			// Group settings (Description, GroupType, HistoryMode)
			if (req.Description is not null || req.GroupType.HasValue || req.ChatHistoryMode.HasValue)
			{
				settingsChanged = true;
				// Use the dedicated method to handle version conflict
				conv = await conversations.UpdateGroupSettingsAsync(
					conversationID, req.Description, req.GroupType, req.ChatHistoryMode);
			}
			else
			{
				await conversations.UpdateAsync(conv);
			}

			// Notify all active members about the update
			try
			{
				var eventName = settingsChanged ? "GroupSettingsUpdated" : "ConversationUpdated";
				var activeMembers = await conversations.GetActiveMembersAsync(conversationID);
				foreach (var m in activeMembers)
				{
					if (m.UserID != Me)
						await hubContext.Clients.User(m.UserID).SendAsync(eventName, conversationID, conv.Version);
				}
			}
			catch { /* best-effort */ }

			return Ok(ConversationResponse.From(conv));
		}

		[HttpDelete("{conversationID}")]
		public async Task<IActionResult> DeleteConversation(string conversationID)
		{
			var conv = await conversations.GetByIdAsync(conversationID);
			if (conv is null)
				return NotFound();
			if (conv.Type == ConversationType.SavedMessages)
				return Forbid();

			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.LeftAt is not null)
				return Forbid();

			// Group: only Owner can delete. Direct: any active member can delete.
			if (conv.Type != ConversationType.Direct && member.Role != MemberRole.Owner)
				return Forbid();

			// Fetch active members BEFORE deleting — after DeleteAsync the membership rows are gone
			List<ConversationMember>? allMembers = null;
			try
			{
				allMembers = (await conversations.GetActiveMembersAsync(conversationID))
					.Where(m => m.UserID != Me)
					.ToList();
			}
			catch { /* best-effort */ }

			await conversations.DeleteAsync(conversationID);

			// Notify all remaining members that this conversation was deleted
			if (allMembers is { Count: > 0 })
			{
				foreach (var m in allMembers)
				{
					try
					{
						await hubContext.Clients.User(m.UserID).SendAsync("ConversationDeleted", conversationID);
					}
					catch { /* per-user best-effort */ }
				}
			}

			return NoContent();
		}

		[HttpPost("{conversationID}/clear")]
		public async Task<IActionResult> ClearConversationMessages(string conversationID)
		{
			var conv = await conversations.GetByIdAsync(conversationID);
			if (conv is null)
				return NotFound();

			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.LeftAt is not null)
				return Forbid();

			// Reset last-read pointer before deleting messages
			member.LastReadMsgID = null;

			await messages.DeleteAllByConversationAsync(conversationID);
			await conversations.ClearLastMessageAsync(conversationID);

			// Notify all active members that messages were cleared
			var members = await conversations.GetActiveMembersAsync(conversationID);
			foreach (var m in members)
			{
				try
				{
					await hubContext.Clients.User(m.UserID).SendAsync("MessagesCleared", conversationID);
				}
				catch { /* per-user best-effort */ }
			}

			return NoContent();
		}

		[HttpPost("{conversationID}/members")]
		public async Task<IActionResult> AddMember(string conversationID, [FromBody] AddMemberRequest req)
		{
			var myMember = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (myMember is null || myMember.LeftAt is not null)
				return Forbid();
			if (myMember.Role < MemberRole.Moderator) return Forbid();

			if (!await users.ExistsByIdAsync(req.UserID))
				return NotFound(new { error = "Người dùng không tồn tại." });

			var existing = await conversations.GetMemberByConversationAndUserAsync(conversationID, req.UserID);
			if (existing is not null && existing.LeftAt is null)
				return Conflict(new { error = "Người dùng đã là thành viên." });

			// Cap role at adder's level — a Moderator cannot add an Owner
			var assignedRole = req.Role;
			if (myMember.Role < MemberRole.Owner && assignedRole >= MemberRole.Moderator)
				assignedRole = MemberRole.Member;

			var newMember = await conversations.AddMemberAsync(new ConversationMember {
				MemberID       = NewID(),
				ConversationID = conversationID,
				UserID         = req.UserID,
				EncryptedKey   = req.EncryptedKey,
				Role           = assignedRole
			});

			var loaded = await conversations.GetMemberByIdAsync(newMember.MemberID);

			// Notify the new member so their sidebar shows this conversation
			try
			{
				await hubContext.Clients.User(req.UserID).SendAsync("ConversationCreated", conversationID);
			}
			catch { /* best-effort */ }

			// Notify existing members that a new member joined
			try
			{
				var activeMembers = await conversations.GetActiveMembersAsync(conversationID);
				foreach (var m in activeMembers)
				{
					if (m.UserID != Me && m.UserID != req.UserID)
						await hubContext.Clients.User(m.UserID).SendAsync("MemberAdded", conversationID, req.UserID);
				}
			}
			catch { /* best-effort */ }

			return CreatedAtAction(nameof(GetConversationMembers), new { conversationID }, MemberResponse.From(loaded!));
		}

		[HttpPatch("{conversationID}/members/{memberID}")]
		public async Task<IActionResult> UpdateMember(string conversationID, string memberID, [FromBody] UpdateMemberRequest req)
		{
			var myMember = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (myMember is null || myMember.LeftAt is not null)
				return Forbid();

			var target = await conversations.GetMemberByIdAsync(memberID);
			if (target is null || target.ConversationID != conversationID)
				return NotFound();

			bool roleChanged = false;
			if (req.Role.HasValue)
			{
				if (myMember.Role != MemberRole.Owner)
					return Forbid();

				// Prevent self-demotion if this is the last Owner
				if (target.Role == MemberRole.Owner && target.UserID == Me)
				{
					var ownerCount = await conversations.GetActiveMembersAsync(conversationID);
					if (ownerCount.Count(m => m.Role == MemberRole.Owner) <= 1)
						return BadRequest(new { error = "Cannot demote yourself as the last owner. Use the leave flow to transfer ownership." });
				}

				// Prevent promoting anyone to Owner through this endpoint
				// Ownership transfer must go through the /leave flow.
				if (req.Role.Value == MemberRole.Owner)
					return BadRequest(new { error = "Cannot promote to Owner. Use the leave flow to transfer ownership." });

				await conversations.UpdateRoleAsync(memberID, req.Role.Value);
				roleChanged = true;
			}
			if (req.Nickname is not null)
				await conversations.UpdateNicknameAsync(memberID, req.Nickname);
			if (req.ShowNotifications.HasValue)
				await conversations.UpdateNotificationModeAsync(memberID, req.ShowNotifications.Value);
			if (req.BannedUntil.HasValue && myMember.Role >= MemberRole.Moderator)
				await conversations.SetBanAsync(memberID, req.BannedUntil.Value);
			if (req.EncryptedKey is not null)
				await conversations.UpdateEncryptedKeyAsync(memberID, req.EncryptedKey);

			var updated = await conversations.GetMemberByIdAsync(memberID);

			// Notify all active members about the role change
			if (roleChanged)
			{
				try
				{
					var activeMembers = await conversations.GetActiveMembersAsync(conversationID);
					foreach (var m in activeMembers)
					{
						if (m.UserID != Me)
							await hubContext.Clients.User(m.UserID).SendAsync("ConversationUpdated", conversationID);
					}
				}
				catch { /* best-effort */ }
			}

			return Ok(MemberResponse.From(updated!));
		}

		[HttpDelete("{conversationID}/members/{memberID}")]
		public async Task<IActionResult> RemoveMember(string conversationID, string memberID)
		{
			var myMember = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (myMember is null || myMember.LeftAt is not null)
				return Forbid();
			if (myMember.Role < MemberRole.Moderator)
				return Forbid();

			var target = await conversations.GetMemberByIdAsync(memberID);
			if (target is null || target.ConversationID != conversationID)
				return NotFound();
			if (target.Role >= myMember.Role)
				return Forbid();

			// Prevent removing the last Owner
			if (target.Role == MemberRole.Owner)
			{
				var activeMembers = await conversations.GetActiveMembersAsync(conversationID);
				if (activeMembers.Count(m => m.Role == MemberRole.Owner) <= 1)
					return BadRequest(new { error = "Cannot remove the last owner." });
			}

			await conversations.RemoveMemberAsync(memberID);

			// Notify the removed member (so their UI removes the conversation)
			try
			{
				if (target.UserID != Me)
					await hubContext.Clients.User(target.UserID).SendAsync("ConversationDeleted", conversationID);
			}
			catch { /* best-effort */ }

			// Notify remaining members that someone was removed
			try
			{
				var activeMembers = await conversations.GetActiveMembersAsync(conversationID);
				foreach (var m in activeMembers)
				{
					if (m.UserID != Me)
						await hubContext.Clients.User(m.UserID).SendAsync("MemberRemoved", conversationID, target.UserID);
				}
			}
			catch { /* best-effort */ }

			return NoContent();
		}

		[HttpPost("{conversationID}/leave")]
		public Task<IActionResult> LeaveConversation(string conversationID, [FromBody] LeaveConversationRequest? req = null)
		{
			var strategy = conversations.DbContext.Database.CreateExecutionStrategy();
			return strategy.ExecuteAsync<IActionResult>(async () =>
			{
				const int maxRetries = 3;

				for (int attempt = 1; attempt <= maxRetries; attempt++)
				{
					using var lockHandle = await groupLock.AcquireAsync(conversationID);

					try
					{
						await using var tx = await conversations.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

						var conv = await conversations.GetByIdWithMembersAsync(conversationID);
						if (conv is null)
							return NotFound();
						if (conv.Type == ConversationType.SavedMessages)
							return Forbid();

						var member = conv.Members.FirstOrDefault(m => m.UserID == Me && m.LeftAt == null);
						if (member is null)
							return NotFound();

						var activeMembers = conv.Members.Where(m => m.LeftAt == null).ToList();
						int activeCount = activeMembers.Count;

						if (member.LeftAt is not null)
							return NoContent();

						if (activeCount == 1)
						{
							member.LeftAt = DateTime.UtcNow;
							await conversations.DbContext.SaveChangesAsync();

							await conversations.HardDeleteConversationAsync(conv.ConversationID);

							tx.Commit();

							await NotifyGroupDeletedAsync(Me, conversationID);
							return NoContent();
						}

						if (member.Role == MemberRole.Owner)
						{
							if (req is null || string.IsNullOrWhiteSpace(req.NewOwnerMemberId))
								return BadRequest(new { error = "Owner must appoint a new admin before leaving." });

							var target = activeMembers.FirstOrDefault(m =>
								m.MemberID == req.NewOwnerMemberId && m.MemberID != member.MemberID);

							if (target is null)
								return BadRequest(new { error = "Selected member not found or is the current owner." });

							target.Role = MemberRole.Owner;
							member.LeftAt = DateTime.UtcNow;
							await conversations.DbContext.SaveChangesAsync();

							tx.Commit();

							await NotifyOwnerLeftAsync(conversationID, Me, target.UserID, member.User?.DisplayName ?? "A member");
							return NoContent();
						}

						member.LeftAt = DateTime.UtcNow;
						await conversations.DbContext.SaveChangesAsync();

						tx.Commit();

						await NotifyMemberLeftAsync(conversationID, Me, member.User?.DisplayName ?? "A member");
						return NoContent();
					}
					catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
					{
						await Task.Delay(100 * attempt);
						continue;
					}
					catch (DbUpdateConcurrencyException)
					{
						return Conflict(new { error = "The group was modified concurrently. Please try again." });
					}
				}

				return StatusCode(StatusCodes.Status500InternalServerError);
			});
		}

		// ─── SignalR notification helpers (called after commit) ─────────

		private async Task NotifyGroupDeletedAsync(string leavingUserId, string conversationId)
		{
			try { await hubContext.Clients.User(leavingUserId).SendAsync("ConversationDeleted", conversationId); }
			catch { /* best-effort */ }
		}

		private async Task NotifyOwnerLeftAsync(string conversationId, string leavingUserId, string newOwnerUserId, string displayName)
		{
			try { await hubContext.Clients.User(leavingUserId).SendAsync("ConversationDeleted", conversationId); }
			catch { /* best-effort */ }

			try
			{
				var sysMsg = new Message
				{
					MessageID      = NewID(),
					ConversationID = conversationId,
					Type           = MessageType.SystemNotification,
					Content        = $"{displayName} has left the group",
					SentAt         = DateTime.UtcNow,
					SenderID       = null
				};
				var created = await messages.CreateAsync(sysMsg);
				var msgResponse = SecureChat.DTOs.MessageResponse.From(created);
				await hubContext.Clients.Group(conversationId).SendAsync("MessageReceived", msgResponse);

				var remaining = await conversations.GetActiveMembersAsync(conversationId);
				foreach (var m in remaining)
				{
					if (m.UserID != leavingUserId)
						await hubContext.Clients.User(m.UserID).SendAsync("MemberRemoved", conversationId, leavingUserId);
				}
			}
			catch { /* best-effort */ }
		}

		private async Task NotifyMemberLeftAsync(string conversationId, string leavingUserId, string displayName)
		{
			try { await hubContext.Clients.User(leavingUserId).SendAsync("ConversationDeleted", conversationId); }
			catch { /* best-effort */ }

			try
			{
				var sysMsg = new Message
				{
					MessageID      = NewID(),
					ConversationID = conversationId,
					Type           = MessageType.SystemNotification,
					Content        = $"{displayName} has left the group",
					SentAt         = DateTime.UtcNow,
					SenderID       = null
				};
				var created = await messages.CreateAsync(sysMsg);
				var msgResponse = SecureChat.DTOs.MessageResponse.From(created);
				await hubContext.Clients.Group(conversationId).SendAsync("MessageReceived", msgResponse);

				var remaining = await conversations.GetActiveMembersAsync(conversationId);
				foreach (var m in remaining)
				{
					if (m.UserID != leavingUserId)
						await hubContext.Clients.User(m.UserID).SendAsync("MemberRemoved", conversationId, leavingUserId);
				}
			}
			catch { /* best-effort */ }
		}

		[HttpGet("{conversationID}/members/me")]
		public async Task<IActionResult> GetMyMembership(string conversationID)
		{
			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null)
				return NotFound();
			return Ok(MemberResponse.From(member));
		}

		[HttpGet("{conversationID}/members")]
		public async Task<IActionResult> GetConversationMembers(string conversationID)
		{
			// Verify user is member of conversation
			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.LeftAt is not null)
				return Forbid();

			var conv = await conversations.GetByIdWithMembersAsync(conversationID);
			if (conv is null)
				return NotFound();

			// Return active members with their user info (including public key)
			var activeMembers = conv.Members
				.Where(m => m.LeftAt == null)
				.ToList();

			return Ok(activeMembers.Select(m => MemberResponse.From(m, presence.IsOnline(m.UserID))));
		}
	}
}
