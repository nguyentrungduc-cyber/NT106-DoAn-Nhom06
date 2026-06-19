using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using SecureChat.DTOs;
using SecureChat.Models;
using SecureChat.Repositories;
using SecureChat.Server.Security;
using SecureChat.Services;

namespace SecureChat.Controllers
{
	[ApiController]
	[Route("api/auth")]
  public class AuthController(UserRepository users, JwtTokenService tokens, IConfiguration config, ForgotPasswordService forgotPasswordService, OtpService otpService, EmailService emailService, ILogger<AuthController> logger, ConversationRepository conversations) : BaseController
	{
		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequest req)
		{
			if (await users.ExistsByUsernameAsync(req.Username))
				return Conflict(new { error = "Tên người dùng đã được sử dụng." });
			if (await users.ExistsByEmailAsync(req.Email))
				return Conflict(new { error = "Email đã được sử dụng." });

            var (hash, salt) = PasswordHasher.HashPassword(req.HashedPassword);
            var userID = NewID();

            await users.CreateAsync(new User
            {
                UserID = userID,
                Username = req.Username,
                DisplayName = req.DisplayName,
                Email = req.Email,
                HashedPassword = hash, // Chỉ lưu hash
                HashedBKey = req.HashedBKey,
                KeySalt = salt,        // Lưu salt thật vào đây
                PublicKey = req.PublicKey
            });

            // Auto-create Saved Messages conversation
            await conversations.GetOrCreateSavedMessagesConversationAsync(userID);

			return Ok(new { message = "Đăng ký thành công." });
		}

		public record ResendLoginOtpRequest(string Identifier);

		[HttpPost("resend-login-otp")]
		public async Task<IActionResult> ResendLoginOtp([FromBody] ResendLoginOtpRequest req)
		{
			if (string.IsNullOrWhiteSpace(req.Identifier))
				return BadRequest(new { message = "Invalid identifier.", errorCode = "INVALID_IDENTIFIER" });

			var identifier = req.Identifier.Trim();
			User? user = null;
			if (identifier.Contains('@')) user = await users.GetByEmailAsync(identifier);
			else user = await users.GetByUsernameAsync(identifier);

			if (user is null)
				return NotFound(new { message = "User not found.", errorCode = "USER_NOT_FOUND" });

			var otp = await otpService.GenerateOtpAsync(user.Email, "login-2fa");
			try
			{
				await emailService.SendOtpEmailAsync(user.Email, otp);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to resend login OTP to {Email}", user.Email);
			}

			return Ok(new { message = "OTP resent" });
		}


		public record VerifyLoginOtpRequest(string Identifier, string Otp, string? DeviceName);

		[HttpPost("verify-login-otp")]
		public async Task<IActionResult> VerifyLoginOtp([FromBody] VerifyLoginOtpRequest req)
		{
			if (string.IsNullOrWhiteSpace(req.Identifier))
				return BadRequest(new { message = "Invalid identifier.", errorCode = "INVALID_IDENTIFIER" });

			if (string.IsNullOrWhiteSpace(req.Otp) || req.Otp.Length != 6)
				return BadRequest(new { message = "Invalid OTP.", errorCode = "INVALID_OTP" });

			// Resolve identifier to user (username or email)
			var identifier = req.Identifier.Trim();
			User? user = null;
			if (identifier.Contains('@')) user = await users.GetByEmailAsync(identifier);
			else user = await users.GetByUsernameAsync(identifier);

			if (user is null)
				return NotFound(new { message = "User not found.", errorCode = "USER_NOT_FOUND" });

			// Verify using the user's email and the specific purpose for login 2FA
			var status = await otpService.VerifyOtpAsync(user.Email, req.Otp, "login-2fa");
			if (status == OtpVerifyStatus.Expired)
				return BadRequest(new { message = "OTP expired.", errorCode = "EXPIRED_OTP" });
			if (status != OtpVerifyStatus.Success)
				return BadRequest(new { message = "Invalid OTP.", errorCode = "INVALID_OTP" });

			// OTP valid - create session and issue tokens
			var sessionID = NewID();
			var accessToken = tokens.GenerateAccessToken(user.UserID, sessionID);
			var refreshToken = JwtTokenService.GenerateRefreshToken();
			var expiry = JwtTokenService.RefreshTokenExpiry(config);

			await users.CreateSessionAsync(new UserSession {
				SessionID = sessionID,
				UserID = user.UserID,
				DeviceName = req.DeviceName ?? "Unknown",
				RefreshToken = refreshToken,
				ExpiresAt = expiry,
				LastUsedAt = DateTime.UtcNow
			});

			return Ok(new { token = accessToken, refreshToken = refreshToken, expiresIn = (int)TimeSpan.FromMinutes(double.Parse(config["Jwt:AccessTokenMinutes"] ?? "15")).TotalSeconds });
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequest req)
		{
			var user = req.UsernameOrEmail.Contains('@')
				? await users.GetByEmailAsync(req.UsernameOrEmail)
				: await users.GetByUsernameAsync(req.UsernameOrEmail);

            if (user is null || !PasswordHasher.Verify(req.HashedPassword, user.HashedPassword, user.KeySalt))
                return Unauthorized(new { error = "Thông tin đăng nhập không hợp lệ." });

			// Generate OTP for login 2FA using OtpService and send via EmailService
            var otp = await otpService.GenerateOtpAsync(user.Email, "login-2fa");
			try
			{
				await emailService.SendOtpEmailAsync(user.Email, otp);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to send login OTP to {Email}", user.Email);
			}

			// Return response indicating 2FA required. Mask email for display.
			string MaskEmail(string e)
			{
				var at = e.IndexOf('@');
				if (at <= 1) return "***";
				return $"{e[0]}***{e[(at - 1)..]}";
			}

			return Ok(new { requires2FA = true, email = MaskEmail(user.Email) });
		}

		[HttpPost("refresh")]
		public async Task<IActionResult> RefreshAccessToken([FromBody] RefreshRequest req)
		{
			var session = await users.GetSessionByRefreshTokenAsync(req.RefreshToken);
			if (session is null)
				return Unauthorized(new { error = "Refresh token không hợp lệ." });

			if (session.ExpiresAt < DateTime.UtcNow) {
				await users.DeleteSessionAsync(session.SessionID);
				return Unauthorized(new { error = "Refresh token đã hết hạn." });
			}

			var newAccess  = tokens.GenerateAccessToken(session.UserID, session.SessionID);
			var newExpiry  = JwtTokenService.RefreshTokenExpiry(config);

			await users.UpdateSessionAsync(session.SessionID, req.RefreshToken, newExpiry);
			return Ok(new AuthResponse(newAccess, req.RefreshToken, newExpiry, UserResponse.From(session.User)));
		}

		[HttpPost("forgot-password/request-otp")]
		public async Task<IActionResult> RequestForgotPasswordOtp([FromBody] ForgotPasswordRequestOtpRequest req)
		{
          if (string.IsNullOrWhiteSpace(req.Email))
			{
				return BadRequest(new { message = "Invalid email format.", errorCode = "INVALID_EMAIL" });
			}

			var email = req.Email.Trim();
			var user = await users.GetByEmailAsync(email);
			if (user is not null)
			{
              await forgotPasswordService.CreateOtpAsync(email);
			}

			return Ok(new ForgotPasswordRequestOtpResponse("If the email is registered, an OTP has been sent."));
		}

		[HttpPost("forgot-password/verify-otp")]
		public async Task<IActionResult> VerifyForgotPasswordOtp([FromBody] ForgotPasswordVerifyOtpRequest req)
		{
          if (string.IsNullOrWhiteSpace(req.Email))
			{
				return BadRequest(new { message = "Invalid email format.", errorCode = "INVALID_EMAIL" });
			}

			if (string.IsNullOrWhiteSpace(req.Otp) || req.Otp.Length != 6 || !req.Otp.All(char.IsDigit))
			{
				return BadRequest(new { message = "Invalid OTP.", errorCode = "INVALID_OTP" });
			}

			var email = req.Email.Trim();
			var otp = req.Otp.Trim();

			var user = await users.GetByEmailAsync(email);
			if (user is null)
			{
				return BadRequest(new { message = "Invalid OTP.", errorCode = "INVALID_OTP" });
			}

            var result = await forgotPasswordService.VerifyOtpAsync(email, otp);
			if (result.Status == OtpVerifyStatus.Expired)
			{
				return BadRequest(new { message = "OTP expired.", errorCode = "EXPIRED_OTP" });
			}

			if (result.Status != OtpVerifyStatus.Success || string.IsNullOrWhiteSpace(result.ResetToken))
			{
				return BadRequest(new { message = "Invalid OTP.", errorCode = "INVALID_OTP" });
			}

			return Ok(new ForgotPasswordVerifyOtpResponse("OTP verified.", result.ResetToken));
		}

		[HttpPost("forgot-password/reset")]
		public async Task<IActionResult> ResetForgotPassword([FromBody] ForgotPasswordResetRequest req)
		{
          if (string.IsNullOrWhiteSpace(req.ResetToken))
			{
				return BadRequest(new { message = "Invalid reset token.", errorCode = "INVALID_TOKEN" });
			}

			if (!IsStrongPassword(req.NewPassword))
			{
				return BadRequest(new { message = "Password does not meet complexity requirements.", errorCode = "WEAK_PASSWORD" });
			}

			var tokenResult = await forgotPasswordService.ValidateResetTokenAsync(req.ResetToken);
			if (tokenResult.Status == ResetTokenStatus.Expired)
			{
				return BadRequest(new { message = "Reset token expired.", errorCode = "EXPIRED_TOKEN" });
			}

			if (tokenResult.Status != ResetTokenStatus.Valid || string.IsNullOrWhiteSpace(tokenResult.Email))
			{
				return BadRequest(new { message = "Invalid reset token.", errorCode = "INVALID_TOKEN" });
			}

			var user = await users.GetByEmailAsync(tokenResult.Email);
			if (user is null)
			{
				logger.LogWarning("Forgot-password reset skipped: user not found for email {Email}", tokenResult.Email);
				return BadRequest(new { message = "Invalid reset token.", errorCode = "INVALID_TOKEN" });
			}

            //await users.UpdateHashedPasswordOnlyAsync(user.UserID, PasswordHasher.NormalizeForStorage(req.NewPassword));
            var (newHash, newSalt) = PasswordHasher.HashPassword(req.NewPassword);
            // Lưu ý: Bạn cần vào file UserRepository.cs sửa lại hàm UpdateHashedPasswordOnlyAsync 
            // để nó nhận và cập nhật cả 2 tham số là newHash và newSalt nhé!
            await users.UpdatePasswordAsync(user.UserID, newHash, newSalt);


            logger.LogInformation("Forgot-password reset completed for user {UserID}", user.UserID);
			return Ok(new ForgotPasswordResetResponse("Password reset successful."));
		}

		private static bool IsStrongPassword(string? password)
		{
			if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
			{
				return false;
			}

			return Regex.IsMatch(password, "[A-Z]")
				&& Regex.IsMatch(password, "[a-z]")
				&& Regex.IsMatch(password, "[0-9]")
				&& Regex.IsMatch(password, "[^a-zA-Z0-9]");
		}

		[Authorize]
		[HttpDelete("logout")]
		public async Task<IActionResult> Logout()
		{
			var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
			if (jti is not null)
				await users.DeleteSessionAsync(jti);
			return NoContent();
		}


		[Authorize]
		[HttpGet("sessions")]
		public async Task<IActionResult> GetSessions()
		{
			var userID = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
			var sessions = await users.GetAllSessionsByUserAsync(userID);
			return Ok(sessions.Select(s => new SessionResponse(s.SessionID, s.DeviceName, s.CreatedAt, s.ExpiresAt)));
		}

		[Authorize]
		[HttpDelete("sessions/{sessionID}")]
		public async Task<IActionResult> RevokeSession(string sessionID)
		{
			var userID = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
			var session = await users.GetSessionByIdAsync(sessionID);
			if (session is null || session.UserID != userID)
				return NotFound();
			await users.DeleteSessionAsync(sessionID);
			return NoContent();
		}

		[Authorize]
		[HttpDelete("sessions")]
		public async Task<IActionResult> RevokeAllSessions()
		{
			var userID = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
			await users.RevokeAllSessionsAsync(userID);
			return NoContent();
		}
	}
}
