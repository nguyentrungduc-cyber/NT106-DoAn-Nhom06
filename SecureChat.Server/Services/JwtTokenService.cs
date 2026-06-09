using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace SecureChat.Services
{
	public class JwtTokenService(IConfiguration config)
	{
		static readonly JwtSecurityTokenHandler Handler = new();

		public string GenerateAccessToken(string userID, string sessionID)
		{
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
			var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

			var claims = new[] {
				new Claim(JwtRegisteredClaimNames.Sub, userID),
                new Claim(ClaimTypes.NameIdentifier, userID),
                new Claim(JwtRegisteredClaimNames.Jti, sessionID),
			};

			var expiry = DateTime.UtcNow.AddMinutes(double.Parse(config["Jwt:AccessTokenMinutes"] ?? "15"));

			var token = new JwtSecurityToken(
				issuer: config["Jwt:Issuer"],
				audience: config["Jwt:Audience"],
				claims: claims,
				expires: expiry,
				signingCredentials: cred
			);

			return Handler.WriteToken(token);
		}

		public static string GenerateRefreshToken()
		{
			var bytes = RandomNumberGenerator.GetBytes(64);
			return Convert.ToBase64String(bytes);
		}

		public static DateTime RefreshTokenExpiry(IConfiguration cfg)
			=> DateTime.UtcNow.AddDays(double.Parse(cfg["Jwt:RefreshTokenDays"] ?? "30"));
	}
}
