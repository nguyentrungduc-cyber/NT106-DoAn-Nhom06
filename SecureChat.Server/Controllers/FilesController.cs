using System;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SecureChat.Models;

namespace SecureChat.Controllers
{
    public sealed class UploadFileRequest
    {
        public IFormFile File { get; set; } = default!;
    }

    [ApiController]
    [Route("api/files")]
    [Authorize]
    public class FilesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FilesController> _logger;
        private readonly AppDbContext _db;

        public FilesController(IWebHostEnvironment env, ILogger<FilesController> logger, AppDbContext db)
        {
            _env = env;
            _logger = logger;
            _db = db;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(long.MaxValue)]
        public async Task<IActionResult> Upload([FromForm] UploadFileRequest request)
        {
            var file = request?.File;
            if (file is null)
                return BadRequest(new { error = "No file provided." });

            if (file.Length == 0)
                return BadRequest(new { error = "File is empty." });

            var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads");
            _logger.LogInformation("File upload: ContentRootPath={Root}, uploadsDir={Dir}, WebRootPath={Web}",
                _env.ContentRootPath, uploadsDir, _env.WebRootPath);
            try
            {
                Directory.CreateDirectory(uploadsDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create uploads directory");
                return StatusCode(500, new { error = "Unable to prepare upload storage." });
            }

            var origName = Path.GetFileName(file.FileName ?? "file");
            var ext = Path.GetExtension(origName) ?? string.Empty;
            var storedName = $"{Guid.NewGuid():N}{ext}";
            var storedPath = Path.Combine(uploadsDir, storedName);

            long total = 0;
            // compute SHA-256 while streaming to disk
            try
            {
                using var hasher = System.Security.Cryptography.IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                const int bufferSize = 81920;
                var buffer = new byte[bufferSize];

                using (var inStream = file.OpenReadStream())
                using (var outFs = new FileStream(storedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
                {
                    int read;
                    while ((read = await inStream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
                    {
                        hasher.AppendData(buffer, 0, read);
                        await outFs.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        total += read;
                    }
                }

                var hash = hasher.GetHashAndReset();
                var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();

                // Persist to database so files survive Railway deploys
                try
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(storedPath);
                    using var sqlCmd = _db.Database.GetDbConnection().CreateCommand();
                    sqlCmd.CommandText = "INSERT IGNORE INTO StoredFiles (file_name, file_data, original_name, file_size, created_at) VALUES (@n, @d, @o, @s, NOW(6))";
                    var pn = sqlCmd.CreateParameter(); pn.ParameterName = "n"; pn.Value = storedName; sqlCmd.Parameters.Add(pn);
                    var pd = sqlCmd.CreateParameter(); pd.ParameterName = "d"; pd.Value = fileBytes; sqlCmd.Parameters.Add(pd);
                    var po = sqlCmd.CreateParameter(); po.ParameterName = "o"; po.Value = origName; sqlCmd.Parameters.Add(po);
                    var ps = sqlCmd.CreateParameter(); ps.ParameterName = "s"; ps.Value = total; sqlCmd.Parameters.Add(ps);
                    await _db.Database.GetDbConnection().OpenAsync();
                    await sqlCmd.ExecuteNonQueryAsync();
                }
                catch { /* best-effort DB persistence */ }
                finally { _db.Database.GetDbConnection().Close(); }

                var url = $"/uploads/{storedName}";
                return Ok(new { url, fileName = origName, fileSize = total, sha256 = hex });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File upload failed");
                // Try to remove partial file
                try { if (System.IO.File.Exists(storedPath)) System.IO.File.Delete(storedPath); } catch { }
                return StatusCode(500, new { error = "File upload failed." });
            }
        }
    }
}
