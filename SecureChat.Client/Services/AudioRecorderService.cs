using System;
using System.IO;
using NAudio.Wave;

namespace SecureChat.Client.Services
{
    // Minimal local audio recorder using NAudio WaveInEvent -> WAV file
    public sealed class AudioRecorderService
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private EventHandler<WaveInEventArgs>? _dataAvailableHandler;
        private string? _outputPath;
        private readonly object _lock = new();

        public bool IsRecording { get; private set; }

        public void StartRecording()
        {
            lock (_lock)
            {
                if (IsRecording) return;

                _waveIn = new WaveInEvent
                {
                    // 16 kHz mono is a reasonable default for short voice notes
                    WaveFormat = new WaveFormat(16000, 1),
                    BufferMilliseconds = 100
                };

                _outputPath = Path.Combine(Path.GetTempPath(), $"record_{Guid.NewGuid():N}.wav");

                _writer = new WaveFileWriter(_outputPath, _waveIn.WaveFormat);

                _dataAvailableHandler = (s, a) =>
                {
                    try
                    {
                        _writer?.Write(a.Buffer, 0, a.BytesRecorded);
                        _writer?.Flush();
                    }
                    catch
                    {
                        // swallow - best effort write
                    }
                };
                _waveIn.DataAvailable += _dataAvailableHandler;

                // Ensure writer is disposed when recording stops (the StopRecording method will handle disposal)
                _waveIn.StartRecording();
                IsRecording = true;
            }
        }

        public string StopRecording()
        {
            lock (_lock)
            {
                if (!IsRecording) return string.Empty;

                try
                {
                    _waveIn?.StopRecording();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    if (_waveIn != null && _dataAvailableHandler != null)
                        _waveIn.DataAvailable -= _dataAvailableHandler;
                }
                catch { }

                try { _waveIn?.Dispose(); } catch { }
                _waveIn = null;

                try { _writer?.Dispose(); } catch { }
                _writer = null;

                _dataAvailableHandler = null;

                IsRecording = false;

                return _outputPath ?? string.Empty;
            }
        }
        public static int GetDurationSeconds(string wavPath)
        {
            try
            {
                using var wav = new WaveFileReader(wavPath);
                return (int)wav.TotalTime.TotalSeconds;
            }
            catch
            {
                return 0;
            }
        }
    }
}
