using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Hubs;
using SecureChat.Server.Security;
using SecureChat.Server.Services;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/users")]
	public class UserController(UserRepository users, ConversationRepository conversations, IHubContext<ChatHub> hubContext, PresenceTracker presence) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

		[HttpGet("me")]
		public async Task<IActionResult> GetMe()
		{
			var user = await users.GetByIdAsync(Me);
			if (user is null)
				return NotFound();
			return Ok(UserResponse.From(user));
		}

		[HttpPatch("me")]
		public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
		{
			var user = await users.GetByIdAsync(Me);
			if (user is null)
				return NotFound();

			if (req.DisplayName is not null)
				user.DisplayName = req.DisplayName;
			if (req.BioText is not null)
				user.BioText     = req.BioText;
			if (req.Email is not null)
				user.Email       = req.Email;
			if (req.Username is not null && req.Username != user.Username)
			{
				if (await users.ExistsByUsernameAsync(req.Username))
					return Conflict(new { error = "Username đã được sử dụng." });
				user.Username = req.Username;
			}

			await users.UpdateAsync(user);

			// Notify direct conversation participants about the change
			try
			{
				var allConvs = await conversations.GetByUserAsync(Me);
				foreach (var conv in allConvs.Where(c => c.Type == ConversationType.Direct))
				{
					var activeMembers = await conversations.GetActiveMembersAsync(conv.ConversationID);
					foreach (var m in activeMembers)
						await hubContext.Clients.User(m.UserID).SendAsync("ConversationUpdated", conv.ConversationID);
				}
			}
			catch { /* best-effort */ }

			// Broadcast profile update to all connected users
			try
			{
				await hubContext.Clients.All.SendAsync("ProfileUpdated", Me, user.DisplayName, user.Username, user.AvatarURL);
			}
			catch { /* best-effort */ }

			return Ok(UserResponse.From(user));
		}

		[HttpPatch("me/avatar")]
		public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest req)
		{
			await users.UpdateAvatarAsync(Me, req.AvatarURL);
			var user = await users.GetByIdAsync(Me);

			// Notify all participants in direct conversations so their sidebar refreshes.
			// The AvatarURL is derived per-viewer in ConversationResponse (GetMyConversations / GetConversation),
			// so we do NOT overwrite conv.AvatarURL in the database — that would corrupt identity mapping.
			try
			{
				var allConvs = await conversations.GetByUserAsync(Me);
				foreach (var conv in allConvs.Where(c => c.Type == ConversationType.Direct))
				{
					var activeMembers = await conversations.GetActiveMembersAsync(conv.ConversationID);
					foreach (var m in activeMembers)
						await hubContext.Clients.User(m.UserID).SendAsync("ConversationUpdated", conv.ConversationID);
				}
			}
			catch { /* best-effort */ }

			// Broadcast profile update
			try
			{
				await hubContext.Clients.All.SendAsync("ProfileUpdated", Me, user!.DisplayName, user.Username, user.AvatarURL);
			}
			catch { /* best-effort */ }

			return Ok(UserResponse.From(user!));
		}

		[HttpDelete("me/avatar")]
		public async Task<IActionResult> RemoveAvatar()
		{
			await users.UpdateAvatarAsync(Me, null);
			var user = await users.GetByIdAsync(Me);

			// Notify direct conversation participants about avatar removal
			try
			{
				var allConvs = await conversations.GetByUserAsync(Me);
				foreach (var conv in allConvs.Where(c => c.Type == ConversationType.Direct))
				{
					var activeMembers = await conversations.GetActiveMembersAsync(conv.ConversationID);
					foreach (var m in activeMembers)
						await hubContext.Clients.User(m.UserID).SendAsync("ConversationUpdated", conv.ConversationID);
				}
			}
			catch { /* best-effort */ }

			// Broadcast profile update so other clients update their caches
			try
			{
				await hubContext.Clients.All.SendAsync("ProfileUpdated", Me, user!.DisplayName, user.Username, user!.AvatarURL);
			}
			catch { /* best-effort */ }

			return NoContent();
		}

        [HttpPut("me/password")]
        public async Task<IActionResult> ChangeHashedPassword([FromBody] UpdateHashedPasswordRequest req)
        {
            var user = await users.GetByIdAsync(Me);
            if (user is null)
                return NotFound();

            // SỬA LỖI 1: Truyền thêm user.KeySalt vào để Verify
            if (!PasswordHasher.Verify(req.OldHashedPassword, user.HashedPassword, user.KeySalt))
                return BadRequest(new { error = "Mật khẩu cũ không trùng khớp." });

            // SỬA LỖI 2: Dùng hàm HashPassword mới, tách riêng Hash và Salt
            var (newHash, newSalt) = PasswordHasher.HashPassword(req.NewHashedPassword);

            // Gọi hàm cập nhật, truyền newHash và newSalt (thay thế cho req.NewKeySalt cũ)
            await users.UpdateHashedPasswordAsync(Me, newHash, req.NewHashedBKey, newSalt);

            user.PublicKey = req.NewPublicKey;
            await users.UpdateAsync(user);

            return NoContent();
        }

        [HttpPatch("me/privacy")]
		public async Task<IActionResult> UpdatePrivacySettings([FromBody] UpdatePrivacyRequest req)
		{
			await users.UpdatePrivacySettingsAsync(Me, req.ShowReadStatus, req.ShowOnlineStatus);
			return NoContent();
		}

		[HttpPatch("me/public-key")]
		public async Task<IActionResult> UpdatePublicKey([FromBody] UpdatePublicKeyRequest req)
		{
			if (req is null || string.IsNullOrWhiteSpace(req.PublicKey))
				return BadRequest(new { error = "Public key is required." });

			await users.UpdatePublicKeyAsync(Me, req.PublicKey);
			return NoContent();
		}

		[HttpDelete("me")]
		public async Task<IActionResult> DeleteAccount()
		{
			await users.DeleteAsync(Me);
			return NoContent();
		}

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var results = await users.SearchAsync(q, currentUserId);
            return Ok(results.Select(u => UserResponse.From(u, presence.IsOnline(u.UserID))));
        }

        [HttpGet("{userID}")]
		public async Task<IActionResult> GetUser(string userID)
		{
			var user = await users.GetByIdAsync(userID);
			if (user is null)
				return NotFound();
			return Ok(UserResponse.From(user));
		}
	}
}
