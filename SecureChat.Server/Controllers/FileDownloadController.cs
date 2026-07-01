using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Controllers
{
	[Authorize]
	[ApiController]
	public class FileDownloadController : ControllerBase
	{
		private readonly AppDbContext _db;
		private readonly IWebHostEnvironment _env;

		public FileDownloadController(AppDbContext db, IWebHostEnvironment env)
		{
			_db = db;
			_env = env;
		}

		[AllowAnonymous]
		[HttpGet("/voice/{fileName}")]
		public async Task<IActionResult> GetVoice(string fileName)
		{
			return await ServeFile("voice", fileName);
		}

		[AllowAnonymous]
		[HttpGet("/uploads/{fileName}")]
		public async Task<IActionResult> GetUpload(string fileName)
		{
			return await ServeFile("uploads", fileName);
		}

		private async Task<IActionResult> ServeFile(string subDir, string fileName)
		{
			if (string.IsNullOrWhiteSpace(fileName))
				return BadRequest();

			// 1. Try filesystem first (fast path for recent uploads)
			var fsPath = Path.Combine(_env.ContentRootPath, "wwwroot", subDir, fileName);
			if (System.IO.File.Exists(fsPath))
				return PhysicalFile(fsPath, "application/octet-stream", enableRangeProcessing: true);

			// 2. Fallback: read from database (survives Railway deploys)
			try
			{
				using var cmd = _db.Database.GetDbConnection().CreateCommand();
				cmd.CommandText = "SELECT file_data, file_name FROM StoredFiles WHERE file_name = @name LIMIT 1";
				var p = cmd.CreateParameter();
				p.ParameterName = "name";
				p.Value = fileName;
				cmd.Parameters.Add(p);

				await _db.Database.GetDbConnection().OpenAsync();
				using var reader = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
				if (await reader.ReadAsync())
				{
					var bytes = reader.GetFieldValue<byte[]>(0);
					var actualName = reader.GetString(1);
					return File(bytes, "application/octet-stream", actualName, enableRangeProcessing: true);
				}
			}
			catch { /* DB read failed */ }
			finally { _db.Database.GetDbConnection().Close(); }

			return NotFound(new { error = "File not found." });
		}
	}
}
