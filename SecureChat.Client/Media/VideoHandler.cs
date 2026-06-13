using System.Drawing;
using OpenCvSharp;

namespace SecureChat.Client.Media
{
    public sealed class VideoHandler : IDisposable
    {
        private VideoCapture? _capture;
        private Task? _captureLoopTask;
        private CancellationTokenSource? _cts;
        private readonly object _captureLock = new();
        private readonly object _frameLock = new();
        private Bitmap? _latestFrame;
        private bool _enabled = true;
        private bool _disposed;
        private int _cameraIndex;
        private int _frameWidth = 480;
        private int _frameHeight = 270;
        private int _targetFps = 24;

        public event EventHandler<Bitmap>? FrameCaptured;
        public event EventHandler? CameraStarted;
        public event EventHandler<Exception>? CameraError;

        public bool IsRunning { get; private set; }
        public bool IsEnabled => _enabled;

        public VideoHandler(int cameraIndex = 0)
        {
            _cameraIndex = cameraIndex;
        }

        public void Configure(int? cameraIndex = null, int? width = null, int? height = null, int? fps = null)
        {
            if (cameraIndex.HasValue) _cameraIndex = cameraIndex.Value;
            if (width.HasValue) _frameWidth = width.Value;
            if (height.HasValue) _frameHeight = height.Value;
            if (fps.HasValue) _targetFps = fps.Value;
        }

        public Task StartAsync()
        {
            if (_disposed) return Task.CompletedTask;
            if (IsRunning) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _captureLoopTask = Task.Run(() => CaptureLoop(token), token);
            IsRunning = true;

            return Task.CompletedTask;
        }

        public Task StartCaptureAsync() => StartAsync();
        public Task StartVideoAsync() => StartAsync();

        public Task StopAsync()
        {
            if (!IsRunning || _disposed) return Task.CompletedTask;

            try { _cts?.Cancel(); }
            catch { }

            try { _captureLoopTask?.Wait(500); }
            catch { }

            lock (_captureLock)
            {
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }

            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }

            IsRunning = false;
            return Task.CompletedTask;
        }

        public Task StopCaptureAsync() => StopAsync();
        public Task StopVideoAsync() => StopAsync();

        public Task SetEnabledAsync(bool enabled)
        {
            if (_disposed) return Task.CompletedTask;

            _enabled = enabled;

            if (!enabled)
            {
                lock (_frameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = null;
                }
            }

            return Task.CompletedTask;
        }

        public Task EnableAsync() => SetEnabledAsync(true);
        public Task DisableAsync() => SetEnabledAsync(false);
        public Task SetVideoEnabledAsync(bool enabled) => SetEnabledAsync(enabled);
        public Task ResumeAsync() => SetEnabledAsync(true);
        public Task PauseAsync() => SetEnabledAsync(false);

        public Bitmap? GrabCurrentFrame()
        {
            lock (_frameLock)
            {
                if (_latestFrame == null) return null;
                return new Bitmap(_latestFrame);
            }
        }

        private void CaptureLoop(CancellationToken token)
        {
            try
            {
                VideoCapture? cap = null;

                try
                {
                    cap = new VideoCapture(_cameraIndex);
                    if (!cap.IsOpened())
                    {
                        cap.Dispose();
                        cap = new VideoCapture(0);
                    }

                    if (!cap.IsOpened())
                    {
                        cap?.Dispose();
                        OnError(new InvalidOperationException("No camera available"));
                        return;
                    }

                    cap.Set(VideoCaptureProperties.FrameWidth, _frameWidth);
                    cap.Set(VideoCaptureProperties.FrameHeight, _frameHeight);
                    cap.Set(VideoCaptureProperties.Fps, _targetFps);
                }
                catch (Exception ex)
                {
                    cap?.Dispose();
                    OnError(ex);
                    return;
                }

                lock (_captureLock)
                {
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = cap;
                }

                OnCameraStarted();

                int frameDelayMs = Math.Max(16, 1000 / _targetFps);

                while (!token.IsCancellationRequested)
                {
                    if (!_enabled)
                    {
                        Thread.Sleep(80);
                        continue;
                    }

                    Mat? mat = null;
                    bool readOk = false;

                    try
                    {
                        mat = new Mat();
                        lock (_captureLock)
                        {
                            if (_capture != null && _capture.IsOpened())
                            {
                                readOk = _capture.Read(mat);
                            }
                        }

                        if (token.IsCancellationRequested) break;

                        if (!readOk || mat.Empty())
                        {
                            mat?.Dispose();
                            Thread.Sleep(frameDelayMs);
                            continue;
                        }

                        using var rgb = new Mat();
                        using var flipped = new Mat();
                        Cv2.Flip(mat, flipped, FlipMode.Y);
                        flipped.CopyTo(rgb);

                        byte[] bytes = rgb.ToBytes(".bmp");
                        using var ms = new MemoryStream(bytes);
                        using var bmp = new Bitmap(ms);
                        var frame = new Bitmap(bmp);

                        lock (_frameLock)
                        {
                            _latestFrame?.Dispose();
                            _latestFrame = frame;
                        }

                        OnFrameCaptured(frame);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        OnError(ex);
                        break;
                    }
                    finally
                    {
                        mat?.Dispose();
                    }

                    try { Task.Delay(frameDelayMs, token).Wait(token); }
                    catch { break; }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                OnError(ex);
            }
            finally
            {
                lock (_captureLock)
                {
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = null;
                }

                IsRunning = false;
            }
        }

        private void OnFrameCaptured(Bitmap frame)
        {
            FrameCaptured?.Invoke(this, frame);
        }

        private void OnCameraStarted()
        {
            CameraStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnError(Exception ex)
        {
            CameraError?.Invoke(this, ex);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts?.Cancel(); }
            catch { }

            try { _captureLoopTask?.Wait(300); }
            catch { }

            lock (_captureLock)
            {
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }

            lock (_frameLock)
            {
                _latestFrame?.Dispose();
                _latestFrame = null;
            }

            _cts?.Dispose();
            _cts = null;
        }
    }
}
