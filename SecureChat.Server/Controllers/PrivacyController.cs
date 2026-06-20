using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureChat.DTOs;
using SecureChat.Repositories;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	[Route("api/privacy")]
	public class PrivacyController(PrivacyRepository privacy, UserRepository users) : BaseController
	{
		string Me => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

		[HttpGet("settings")]
		public async Task<IActionResult> GetSettings()
		{
			var settings = await privacy.GetOrCreateSettingsAsync(Me);
			return Ok(settings);
		}

		[HttpPut("settings")]
		public async Task<IActionResult> UpdateSettings([FromBody] UpdatePrivacySettingsRequest req)
		{
			var settings = await privacy.UpdateSettingsAsync(Me, req);
			return Ok(settings);
		}
	}
}
