using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SecureChat.Client.Services
{
    public sealed class VoicePlaybackService
    {
        private readonly FileTransferService _fileTransfer = new();
        private bool _isPlaying;
        private readonly object _playLock = new();

        public async Task PlayAsync(string url, string expectedSha256, byte[] key, byte[] iv)
        {
            lock (_playLock)
            {
                if (_isPlaying)
                    throw new InvalidOperationException("Playback already in progress.");
                _isPlaying = true;
            }

            string encryptedTemp = string.Empty;
            string decryptedTemp = string.Empty;
            WaveOutEvent? outputDevice = null;
            AudioFileReader? audioReader = null;

            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("Invalid voice URL.", nameof(url));
                if (string.IsNullOrWhiteSpace(expectedSha256))
                    throw new ArgumentException("Missing voice hash.", nameof(expectedSha256));

                encryptedTemp = Path.Combine(Path.GetTempPath(), $"voice_dl_{Guid.NewGuid():N}.dat");

                await _fileTransfer.DownloadAsync(url, encryptedTemp, null, default).ConfigureAwait(false);

                var okHash = await _fileTransfer.VerifyAsync(encryptedTemp, expectedSha256).ConfigureAwait(false);
                if (!okHash)
                    throw new InvalidOperationException("Voice file hash mismatch.");

                decryptedTemp = await VoiceEncryptionService.DecryptAsync(encryptedTemp, key, iv).ConfigureAwait(false);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                outputDevice = new WaveOutEvent();
                audioReader = new AudioFileReader(decryptedTemp);
                outputDevice.Init(audioReader);
                outputDevice.PlaybackStopped += (_, __) => tcs.TrySetResult(true);
                outputDevice.Play();

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                try { outputDevice?.Stop(); } catch { }
                try { outputDevice?.Dispose(); } catch { }
                try { audioReader?.Dispose(); } catch { }

                if (!string.IsNullOrWhiteSpace(decryptedTemp) && File.Exists(decryptedTemp))
                {
                    try { File.Delete(decryptedTemp); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(encryptedTemp) && File.Exists(encryptedTemp))
                {
                    try { File.Delete(encryptedTemp); } catch { }
                }

                lock (_playLock)
                {
                    _isPlaying = false;
                }
            }
        }
    }
}
