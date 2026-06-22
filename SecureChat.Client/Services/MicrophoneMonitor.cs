using System;
using NAudio.Wave;

namespace SecureChat.Client.Services
{
    public sealed class MicrophoneMonitor : IDisposable
    {
        private WaveInEvent? _waveIn;
        private float _smoothedLevel;
        private float _displayLevel;
        private float _peakLevel;
        private bool _disposed;
        private int _previousDeviceNumber = -1;

        private const float Gain = 5.0f;
        private const float PowerCurve = 0.60f;
        private const float SmoothAttack = 0.68f;
        private const float SmoothDecay = 0.90f;
        private const float DisplayDecay = 0.92f;
        private const float PeakDecay = 0.96f;

        public float Level => _displayLevel;
        public float PeakLevel => _peakLevel;

        public event Action<float>? LevelChanged;
        public event Action<float>? PeakChanged;

        public void Start(int deviceNumber = 0)
        {
            if (deviceNumber == _previousDeviceNumber && _waveIn != null)
                return;

            Stop();

            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = new WaveFormat(48000, 16, 1),
                    BufferMilliseconds = 30
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveIn.StartRecording();
                _previousDeviceNumber = deviceNumber;
            }
            catch
            {
                _waveIn = null;
                _smoothedLevel = 0f;
                _displayLevel = 0f;
                _peakLevel = 0f;
            }
        }

        public void Stop()
        {
            if (_waveIn != null)
            {
                try
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.RecordingStopped -= OnRecordingStopped;
                    _waveIn.StopRecording();
                }
                catch { }
                try { _waveIn.Dispose(); } catch { }
                _waveIn = null;
            }
            _smoothedLevel = 0f;
            _displayLevel = 0f;
            _peakLevel = 0f;
            _previousDeviceNumber = -1;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_disposed || e.BytesRecorded == 0) return;

            int sampleCount = e.BytesRecorded / 2;
            double sumSquares = 0;

            unsafe
            {
                fixed (byte* ptr = e.Buffer)
                {
                    short* samples = (short*)ptr;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        float normalized = samples[i] / 32768f;
                        sumSquares += normalized * normalized;
                    }
                }
            }

            float rms = (float)Math.Sqrt(sumSquares / sampleCount);

            // Step 1: Apply gain to boost quiet signals
            float gained = Math.Min(1f, rms * Gain);

            // Step 2: Perceptual power curve (spreads low-mid range for visible movement)
            float perceptual = (float)Math.Pow(gained, PowerCurve);

            // Step 3: EMA smoothing (faster attack, slower decay for the underlying average)
            float smoothed = perceptual > _smoothedLevel
                ? (_smoothedLevel * (1f - SmoothAttack)) + (perceptual * SmoothAttack)
                : (_smoothedLevel * (1f - SmoothDecay)) + (perceptual * SmoothDecay);
            _smoothedLevel = smoothed;

            // Step 4: Peak decay (instant attack, slow falloff) — Telegram-style hold
            if (smoothed > _displayLevel)
                _displayLevel = smoothed;
            else
                _displayLevel = Math.Max(smoothed, _displayLevel * DisplayDecay);

            // Step 5: Peak hold — instant rise, very slow decay
            if (_displayLevel > _peakLevel)
                _peakLevel = _displayLevel;
            else
                _peakLevel = Math.Max(_displayLevel, _peakLevel * PeakDecay);

            LevelChanged?.Invoke(_displayLevel);
            PeakChanged?.Invoke(_peakLevel);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _smoothedLevel = 0f;
            _displayLevel = 0f;
            _peakLevel = 0f;
            LevelChanged?.Invoke(0f);
            PeakChanged?.Invoke(0f);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
