using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Playback service với hỗ trợ Play / Pause / Stop và báo cáo tiến độ.
    /// </summary>
    public sealed class VoicePlaybackService : IDisposable
    {
        private readonly FileTransferService _fileTransfer = new();
        private readonly object _stateLock = new();

        // NAudio objects — chỉ hợp lệ khi _state != Idle
        private WaveOutEvent?    _outputDevice;
        private AudioFileReader? _audioReader;

        // Temp file paths để cleanup sau
        private string _encryptedTemp = string.Empty;
        private string _decryptedTemp = string.Empty;

        // TCS để await cho đến khi hết bài hoặc stop
        private TaskCompletionSource<bool>? _tcs;

        // Timer cập nhật UI
        private System.Windows.Forms.Timer? _positionTimer;

        // Track messageId đang phát (để bubble biết có phải mình đang phát không)
        private string _currentMessageId = string.Empty;

        public enum PlaybackState { Idle, Loading, Playing, Paused }
        private PlaybackState _state = PlaybackState.Idle;

        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Phát ra trong quá trình phát: (currentSeconds, totalSeconds, messageId)</summary>
        public event Action<double, double, string>? PositionChanged;

        /// <summary>Trạng thái thay đổi (Idle/Loading/Playing/Paused), kèm messageId</summary>
        public event Action<PlaybackState, string>? StateChanged;

        // ── Public API ──────────────────────────────────────────────────────────

        public PlaybackState State => _state;
        public string CurrentMessageId => _currentMessageId;

        /// <summary>
        /// Bắt đầu phát một voice message. Nếu đang phát cùng messageId thì toggle Pause/Resume.
        /// Nếu đang phát bài khác thì stop bài cũ rồi phát bài mới.
        /// </summary>
        public async Task PlayOrToggleAsync(string messageId, string url, string expectedSha256, byte[] key, byte[] iv)
        {
            lock (_stateLock)
            {
                // Cùng bài — toggle pause/resume
                if (_currentMessageId == messageId && _state == PlaybackState.Playing)
                {
                    _outputDevice?.Pause();
                    _state = PlaybackState.Paused;
                    RaiseStateChanged();
                    _positionTimer?.Stop();
                    return;
                }

                if (_currentMessageId == messageId && _state == PlaybackState.Paused)
                {
                    _outputDevice?.Play();
                    _state = PlaybackState.Playing;
                    RaiseStateChanged();
                    _positionTimer?.Start();
                    return;
                }
            }

            // Bài khác hoặc idle — stop bài cũ
            StopInternal(signalTcs: true);

            // Phát bài mới
            await PlayCoreAsync(messageId, url, expectedSha256, key, iv).ConfigureAwait(false);
        }

        /// <summary>Stop và reset về Idle.</summary>
        public void Stop()
        {
            StopInternal(signalTcs: true);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private async Task PlayCoreAsync(string messageId, string url, string expectedSha256, byte[] key, byte[] iv)
        {
            // Set Loading
            lock (_stateLock)
            {
                _currentMessageId = messageId;
                _state = PlaybackState.Loading;
            }
            RaiseStateChanged();

            string encryptedTemp = string.Empty;
            string decryptedTemp = string.Empty;

            try
            {
                encryptedTemp = Path.Combine(Path.GetTempPath(), $"voice_dl_{Guid.NewGuid():N}.dat");
                await _fileTransfer.DownloadAsync(url, encryptedTemp, null, default).ConfigureAwait(false);

                var okHash = await _fileTransfer.VerifyAsync(encryptedTemp, expectedSha256).ConfigureAwait(false);
                if (!okHash)
                    throw new InvalidOperationException("Voice file hash mismatch.");

                decryptedTemp = await VoiceEncryptionService.DecryptAsync(encryptedTemp, key, iv).ConfigureAwait(false);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var outputDevice = new WaveOutEvent();
                var audioReader  = new AudioFileReader(decryptedTemp);
                outputDevice.Init(audioReader);
                outputDevice.PlaybackStopped += (_, __) => tcs.TrySetResult(true);

                lock (_stateLock)
                {
                    _outputDevice    = outputDevice;
                    _audioReader     = audioReader;
                    _tcs             = tcs;
                    _encryptedTemp   = encryptedTemp;
                    _decryptedTemp   = decryptedTemp;
                    _state           = PlaybackState.Playing;
                }
                RaiseStateChanged();
                StartPositionTimer();
                outputDevice.Play();

                await tcs.Task.ConfigureAwait(false);
            }
            catch
            {
                // cleanup on error
                CleanupFiles(encryptedTemp, decryptedTemp);
                throw;
            }
            finally
            {
                // Đến đây hoặc là bài hết, hoặc Stop() đã được gọi
                lock (_stateLock)
                {
                    // Chỉ reset nếu vẫn đang là bài này
                    if (_currentMessageId == messageId)
                    {
                        DisposePlaybackObjects();
                        _state            = PlaybackState.Idle;
                        _currentMessageId = string.Empty;
                    }
                }
                RaiseStateChanged();
                StopPositionTimer();
            }
        }

        private void StopInternal(bool signalTcs)
        {
            lock (_stateLock)
            {
                if (_state == PlaybackState.Idle) return;

                try { _outputDevice?.Stop(); } catch { }
                if (signalTcs) _tcs?.TrySetResult(true);

                DisposePlaybackObjects();
                _state            = PlaybackState.Idle;
                _currentMessageId = string.Empty;
            }
            RaiseStateChanged();
            StopPositionTimer();
        }

        private void DisposePlaybackObjects()
        {
            var od = _outputDevice;
            var ar = _audioReader;
            _outputDevice = null;
            _audioReader  = null;

            try { od?.Dispose(); } catch { }
            try { ar?.Dispose(); } catch { }

            CleanupFiles(_encryptedTemp, _decryptedTemp);
            _encryptedTemp = string.Empty;
            _decryptedTemp = string.Empty;
        }

        private static void CleanupFiles(string enc, string dec)
        {
            if (!string.IsNullOrWhiteSpace(dec) && File.Exists(dec))
                try { File.Delete(dec); } catch { }
            if (!string.IsNullOrWhiteSpace(enc) && File.Exists(enc))
                try { File.Delete(enc); } catch { }
        }

        // ── Timer để báo vị trí ────────────────────────────────────────────────

        private void StartPositionTimer()
        {
            StopPositionTimer();
            _positionTimer          = new System.Windows.Forms.Timer { Interval = 250 };
            _positionTimer.Tick    += OnPositionTick;
            _positionTimer.Start();
        }

        private void StopPositionTimer()
        {
            _positionTimer?.Stop();
            _positionTimer?.Dispose();
            _positionTimer = null;
        }

        private void OnPositionTick(object? sender, EventArgs e)
        {
            double current = 0, total = 0;
            string msgId;
            lock (_stateLock)
            {
                if (_audioReader == null) return;
                current = _audioReader.CurrentTime.TotalSeconds;
                total   = _audioReader.TotalTime.TotalSeconds;
                msgId   = _currentMessageId;
            }
            PositionChanged?.Invoke(current, total, msgId);
        }

        private void RaiseStateChanged()
        {
            PlaybackState st;
            string msgId;
            lock (_stateLock) { st = _state; msgId = _currentMessageId; }
            StateChanged?.Invoke(st, msgId);
        }

        // ── Seek ───────────────────────────────────────────────────────────────

        /// <summary>Seek đến vị trí (giây). Chỉ có tác dụng khi đang Playing hoặc Paused.</summary>
        public void SeekTo(double seconds)
        {
            lock (_stateLock)
            {
                if (_audioReader == null) return;
                var ts = TimeSpan.FromSeconds(Math.Clamp(seconds, 0, _audioReader.TotalTime.TotalSeconds));
                _audioReader.CurrentTime = ts;
            }
        }

        public void Dispose()
        {
            StopInternal(signalTcs: true);
        }
    }
}
