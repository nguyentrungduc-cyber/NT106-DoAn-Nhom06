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
	[Route("api/conversations")]
	public class ConversationController(ConversationRepository conversations, UserRepository users) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<IActionResult> GetMyConversations()
        {
            var list = await conversations.GetByUserAsync(Me);
            var result = list.Select(c =>
            {
                var res = ConversationResponse.From(c);

                // Với Direct conversation: dùng tên người kia làm tên hội thoại
                if (c.Type == ConversationType.Direct && string.IsNullOrEmpty(c.Name))
                {
                    var other = c.Members.FirstOrDefault(m => m.UserID != Me && m.LeftAt == null);
                    if (other?.User != null)
                        res = res with { Name = other.User.DisplayName };
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
			return Ok(ConversationResponse.From(conv));
		}

		[HttpPost]
		public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest req)
		{
			foreach (var entry in req.Members)
				if (!await users.ExistsByIdAsync(entry.UserID))
					return BadRequest(new { error = $"User '{entry.UserID}' không tồn tại." });

			if (req.Type == ConversationType.Direct)
			{
				var otherID = req.Members.FirstOrDefault(m => m.UserID != Me)?.UserID;
				if (otherID is not null) {
					var existing = await conversations.GetDirectConversationAsync(Me, otherID);
					if (existing is not null)
						return Ok(ConversationResponse.From(existing));
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
				await conversations.AddMemberAsync(new ConversationMember {
					MemberID       = NewID(),
					ConversationID = conv.ConversationID,
					UserID         = entry.UserID,
					EncryptedKey   = entry.EncryptedKey,
					Role           = MemberRole.Member
				});

			var loaded = await conversations.GetByIdWithMembersAsync(conv.ConversationID);
			return CreatedAtAction(nameof(GetConversation), new { conversationID = conv.ConversationID }, ConversationResponse.From(loaded!));
		}

		[HttpPatch("{conversationID}")]
		public async Task<IActionResult> UpdateConversation(string conversationID, [FromBody] UpdateConversationRequest req)
		{
			var conv = await conversations.GetByIdAsync(conversationID);
			if (conv is null)
				return NotFound();

			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.LeftAt is not null)
				return Forbid();
			if (member.Role < MemberRole.Moderator)
				return Forbid();
			if (req.Name is not null)
				conv.Name = req.Name;
			if (req.AvatarUrl is not null)
				conv.AvatarURL = req.AvatarUrl;

			await conversations.UpdateAsync(conv);
			return Ok(ConversationResponse.From(conv));
		}

		[HttpDelete("{conversationID}")]
		public async Task<IActionResult> DeleteConversation(string conversationID)
		{
			var conv = await conversations.GetByIdAsync(conversationID);
			if (conv is null)
				return NotFound();

			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.Role != MemberRole.Owner)
				return Forbid();
			await conversations.DeleteAsync(conversationID);

			return NoContent();
		}

		[HttpGet("{conversationID}/members")]
		public async Task<IActionResult> GetMembers(string conversationID)
		{
			var myMember = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (myMember is null || myMember.LeftAt is not null)
				return Forbid();

			var members = await conversations.GetActiveMembersAsync(conversationID);
			return Ok(members.Select(MemberResponse.From));
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

			var newMember = await conversations.AddMemberAsync(new ConversationMember {
				MemberID       = NewID(),
				ConversationID = conversationID,
				UserID         = req.UserID,
				EncryptedKey   = req.EncryptedKey,
				Role           = req.Role
			});

			var loaded = await conversations.GetMemberByIdAsync(newMember.MemberID);
			return CreatedAtAction(nameof(GetMembers), new { conversationID }, MemberResponse.From(loaded!));
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
			if (req.Role.HasValue) {
				if (myMember.Role != MemberRole.Owner)
					return Forbid();
				await conversations.UpdateRoleAsync(memberID, req.Role.Value);
			}
			if (req.Nickname is not null)
				await conversations.UpdateNicknameAsync(memberID, req.Nickname);
			if (req.ShowNotifications.HasValue)
				await conversations.UpdateNotificationModeAsync(memberID, req.ShowNotifications.Value);
			if (req.BannedUntil.HasValue && myMember.Role >= MemberRole.Moderator)
				await conversations.SetBanAsync(memberID, req.BannedUntil.Value);

			var updated = await conversations.GetMemberByIdAsync(memberID);
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

			await conversations.RemoveMemberAsync(memberID);
			return NoContent();
		}

		[HttpPost("{conversationID}/leave")]
		public async Task<IActionResult> LeaveConversation(string conversationID)
		{
			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null || member.LeftAt is not null)
				return NotFound();

			await conversations.LeaveMemberAsync(member.MemberID);
			return NoContent();
		}

		[HttpGet("{conversationID}/members/me")]
		public async Task<IActionResult> GetMyMembership(string conversationID)
		{
			var member = await conversations.GetMemberByConversationAndUserAsync(conversationID, Me);
			if (member is null)
				return NotFound();
			return Ok(MemberResponse.From(member));
		}
	}
}
