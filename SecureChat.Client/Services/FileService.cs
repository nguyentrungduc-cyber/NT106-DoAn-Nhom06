// Prepared for future upload/download integrity verification.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

namespace SecureChat.Client.Services
{
    public static class FileService
    {
        /// <summary>
        /// Compute SHA-256 of a file by streaming it asynchronously. Returns lowercase hex string.
        /// </summary>
        public static async Task<string> ComputeSha256Async(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is required", nameof(filePath));

            // Use IncrementalHash to avoid loading whole file into memory
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            // Buffer size chosen to match common copy buffer sizes
            const int bufferSize = 81920;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
            var buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                hasher.AppendData(buffer, 0, bytesRead);
            }

            var hash = hasher.GetHashAndReset();
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// Download a file from the given URL to destinationPath, reporting progress and honoring cancellation.
        /// Prepared for reuse from UI code.
        /// </summary>
        public static async Task DownloadAsync(string url, string destinationPath, IProgress<int>? progress, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("Empty URL", nameof(url));

            using var client = SecureChat.Client.Services.ApiClient.Create();
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var contentLength = resp.Content.Headers.ContentLength ?? -1L;
            using var stream = await resp.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            const int bufferSize = 81920;
            using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);

            var buffer = new byte[bufferSize];
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false)) > 0)
            {
                token.ThrowIfCancellationRequested();
                await fs.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                totalRead += read;
                if (contentLength > 0)
                {
                    int percent = (int)(totalRead * 100L / contentLength);
                    progress?.Report(percent);
                }
                else
                {
                    var coarse = Math.Min(99, (int)Math.Min(99, totalRead / 100_000)); // every ~100KB yields +1
                    progress?.Report(coarse);
                }
            }

            progress?.Report(100);
        }
    }
}
