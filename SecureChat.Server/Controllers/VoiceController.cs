using System;
using System.IO;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SecureChat.Controllers
{
    public sealed class UploadVoiceRequest
    {
        public IFormFile File { get; set; } = default!;
        public int? Duration { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/voice")]
    public class VoiceController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<VoiceController> _logger;

        public VoiceController(IWebHostEnvironment env, ILogger<VoiceController> logger)
        {
            _env = env;
            _logger = logger;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50 * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] UploadVoiceRequest request)
        {
            var file = request?.File;
            if (file is null)
                return BadRequest(new { error = "No voice file provided." });

            if (file.Length == 0)
                return BadRequest(new { error = "Voice file is empty." });

            var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (ext != ".dat" && ext != ".wav" && ext != ".mp3" && ext != ".ogg" && ext != ".oga")
                return BadRequest(new { error = "Invalid voice file format." });

            var voiceDir = Path.Combine(_env.ContentRootPath, "wwwroot", "voice");
            _logger.LogInformation("Voice upload: ContentRootPath={Root}, voiceDir={Dir}, WebRootPath={Web}",
                _env.ContentRootPath, voiceDir, _env.WebRootPath);
            try
            {
                Directory.CreateDirectory(voiceDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create voice directory");
                return StatusCode(500, new { error = "Unable to prepare voice storage." });
            }

            var storedName = $"{Guid.NewGuid():N}.dat";
            var storedPath = Path.Combine(voiceDir, storedName);

            try
            {
                using var hasher = System.Security.Cryptography.IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                const int bufferSize = 81920;
                var buffer = new byte[bufferSize];
                long total = 0;

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

                var url = $"/voice/{storedName}";
                return Ok(new
                {
                    url,
                    fileName = Path.GetFileName(file.FileName ?? "voice.dat"),
                    fileSize = total,
                    sha256 = hex,
                    duration = request?.Duration
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voice upload failed");
                try { if (System.IO.File.Exists(storedPath)) System.IO.File.Delete(storedPath); } catch { }
                return StatusCode(500, new { error = "Voice upload failed." });
            }
        }
    }
}
