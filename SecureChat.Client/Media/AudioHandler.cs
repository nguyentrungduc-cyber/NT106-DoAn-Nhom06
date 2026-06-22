using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private uint _sendSequenceNumber;
        private uint _lastReceivedSeq;
        private int _outputDeviceNumber;

        // Jitter buffer: holds out-of-order packets for reordering
        private readonly ConcurrentDictionary<uint, byte[]> _jitterBuffer = new();
        private const int JitterWindow = 5;
        private const int AudioSampleRate = 16000;

        // Pre-allocated buffer pool for audio chunks
        private static readonly int ChunkSize = AudioSampleRate * 1 / 20; // 50ms at 16kHz 8-bit = 800 bytes
        private const int ExpectedChunkBytes = 804; // 800 + 4 bytes seq num

        public event Action<byte[], uint>? AudioDataAvailable;
        public event EventHandler<Exception>? AudioError;

        public bool IsMuted => _muted;
        public bool IsCapturing => _capturing;

        public Task StartAsync(int? inputDeviceNumber = null, int? outputDeviceNumber = null)
        {
            lock (_lock)
            {
                if (_disposed || _capturing) return Task.CompletedTask;

                _sendSequenceNumber = 0;
                _lastReceivedSeq = 0;
                _jitterBuffer.Clear();

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(AudioSampleRate, 16, 1),
                    BufferMilliseconds = 50,
                    DeviceNumber = inputDeviceNumber ?? 0
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveIn.StartRecording();
                _capturing = true;

                _outputDeviceNumber = outputDeviceNumber ?? 0;
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

        /// <summary>
        /// Receive audio data with 4-byte big-endian sequence number prefix.
        /// Uses jitter buffer for reordering.
        /// </summary>
        public void PlayAudio(byte[] dataWithSeq)
        {
            if (_disposed || dataWithSeq == null || dataWithSeq.Length < 4) return;

            uint seqNum = (uint)(
                (dataWithSeq[0] << 24) |
                (dataWithSeq[1] << 16) |
                (dataWithSeq[2] << 8) |
                dataWithSeq[3]);

            byte[] audioData;
            if (dataWithSeq.Length > 4)
            {
                audioData = new byte[dataWithSeq.Length - 4];
                Buffer.BlockCopy(dataWithSeq, 4, audioData, 0, audioData.Length);
            }
            else
            {
                return;
            }

            lock (_lock)
            {
                // Place into jitter buffer
                _jitterBuffer[seqNum] = audioData;

                // Drain in-order packets from jitter buffer
                DrainJitterBuffer();
            }
        }

        private void DrainJitterBuffer()
        {
            while (_jitterBuffer.TryRemove(_lastReceivedSeq + 1, out var nextChunk))
            {
                _lastReceivedSeq++;
                PlayRawAudio(nextChunk);
            }
        }

        private void PlayRawAudio(byte[] audioData)
        {
            if (_disposed || audioData == null || audioData.Length == 0) return;

            if (_waveOut == null)
            {
                _waveOut = new WaveOutEvent { DesiredLatency = 150, DeviceNumber = _outputDeviceNumber };
                _waveProvider = new BufferedWaveProvider(new WaveFormat(AudioSampleRate, 8, 1))
                {
                    BufferDuration = TimeSpan.FromMilliseconds(800),
                    DiscardOnBufferOverflow = true
                };
                _waveOut.Init(_waveProvider);
                _waveOut.Play();
            }

            _waveProvider?.AddSamples(audioData, 0, audioData.Length);
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_disposed || _muted || e.BytesRecorded == 0) return;

            uint seq = _sendSequenceNumber++;

            // Step 1: Convert 16-bit PCM to 8-bit mu-law
            int sampleCount = e.BytesRecorded / 2;
            byte[] encoded = new byte[sampleCount + 4]; // +4 for seq num prefix

            // Sequence number prefix (big-endian)
            encoded[0] = (byte)(seq >> 24);
            encoded[1] = (byte)(seq >> 16);
            encoded[2] = (byte)(seq >> 8);
            encoded[3] = (byte)seq;

            unsafe
            {
                fixed (byte* srcPtr = e.Buffer)
                fixed (byte* dstPtr = encoded)
                {
                    short* samples = (short*)srcPtr;
                    for (int i = 0; i < sampleCount; i++)
                    {
                        dstPtr[i + 4] = LinearToMuLaw(samples[i]);
                    }
                }
            }

            AudioDataAvailable?.Invoke(encoded, seq);
        }

        /// <summary>
        /// G.711 mu-law encoding: 16-bit PCM → 8-bit mu-law
        /// </summary>
        private static byte LinearToMuLaw(short sample)
        {
            const int BIAS = 0x84;
            const int CLIP = 32635;

            int sign = (sample >> 8) & 0x80;
            if (sign != 0)
                sample = (short)-sample;
            if (sample > CLIP)
                sample = CLIP;

            sample = (short)(sample + BIAS);
            int exponent = 7;
            for (int expMask = 0x4000; (sample & expMask) == 0 && exponent > 0; expMask >>= 1)
                exponent--;
            int mantissa = (sample >> (exponent + 3)) & 0x0F;
            int muLaw = (sign | (exponent << 4) | mantissa);
            return (byte)(~muLaw);
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
            _jitterBuffer.Clear();
        }
    }
}
