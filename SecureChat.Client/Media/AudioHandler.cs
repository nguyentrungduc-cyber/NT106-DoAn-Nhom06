using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SecureChat.Client.Media
{
    public sealed class AudioHandler : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveOutEvent? _waveOut;
        private BufferedWaveProvider? _waveProvider;
        private readonly object _lock = new();
        private bool _muted;
        private bool _disposed;
        private bool _capturing;

        public event Action<byte[]>? AudioDataAvailable;
        public event EventHandler<Exception>? AudioError;

        public bool IsMuted => _muted;
        public bool IsCapturing => _capturing;

        public Task StartAsync()
        {
            lock (_lock)
            {
                if (_disposed || _capturing) return Task.CompletedTask;

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(48000, 16, 1),
                    BufferMilliseconds = 50
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveIn.StartRecording();
                _capturing = true;
            }
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                if (!_capturing) return Task.CompletedTask;
                try { _waveIn?.StopRecording(); } catch { }
                _capturing = false;
            }
            return Task.CompletedTask;
        }

        public Task SetMutedAsync(bool muted)
        {
            _muted = muted;
            return Task.CompletedTask;
        }

        public void PlayAudio(byte[] audioData)
        {
            if (_disposed || audioData == null || audioData.Length == 0) return;

            lock (_lock)
            {
                if (_waveOut == null)
                {
                    _waveOut = new WaveOutEvent { DesiredLatency = 100 };
                    _waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                    {
                        BufferDuration = TimeSpan.FromSeconds(2),
                        DiscardOnBufferOverflow = true
                    };
                    _waveOut.Init(_waveProvider);
                    _waveOut.Play();
                }

                _waveProvider?.AddSamples(audioData, 0, audioData.Length);
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_disposed || _muted || e.BytesRecorded == 0) return;

            var chunk = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, chunk, 0, e.BytesRecorded);
            AudioDataAvailable?.Invoke(chunk);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            lock (_lock)
            {
                _capturing = false;
                if (e.Exception != null)
                    AudioError?.Invoke(this, e.Exception);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_waveIn != null)
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.RecordingStopped -= OnRecordingStopped;
                    _waveIn.StopRecording();
                    _waveIn.Dispose();
                }
            }
            catch { }
            _waveIn = null;

            try { _waveOut?.Stop(); } catch { }
            try { _waveOut?.Dispose(); } catch { }
            _waveOut = null;
            _waveProvider = null;
            _capturing = false;
        }
    }
}
