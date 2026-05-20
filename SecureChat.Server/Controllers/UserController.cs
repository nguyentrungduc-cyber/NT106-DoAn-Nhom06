using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureChat.DTOs;
using SecureChat.Repositories;
using SecureChat.Server.Security;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/users")]
	public class UserController(UserRepository users) : BaseController
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

			await users.UpdateAsync(user);
			return Ok(UserResponse.From(user));
		}

		[HttpPatch("me/avatar")]
		public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest req)
		{
			await users.UpdateAvatarAsync(Me, req.AvatarURL);
			var user = await users.GetByIdAsync(Me);
			return Ok(UserResponse.From(user!));
		}

		[HttpDelete("me/avatar")]
		public async Task<IActionResult> RemoveAvatar()
		{
			await users.UpdateAvatarAsync(Me, null);
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
			if (string.IsNullOrWhiteSpace(q))
				return BadRequest(new { error = "Thiếu từ khóa tìm kiếm." });

			var results = await users.SearchAsync(q);
			return Ok(results.Select(UserResponse.From));
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
