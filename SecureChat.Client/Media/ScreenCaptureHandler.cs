using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SecureChat.Client.Media
{
    public sealed class ScreenCaptureHandler : IDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private bool _disposed;
        private int _targetFps = 5;

        public event Action<Bitmap>? FrameCaptured;
        public event EventHandler<Exception>? CaptureError;

        public bool IsRunning { get; private set; }

        public Task StartAsync()
        {
            if (_disposed || IsRunning) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _captureTask = Task.Run(() => CaptureLoop(token), token);
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (!IsRunning || _disposed) return Task.CompletedTask;

            try { _cts?.Cancel(); } catch { }
            try { _captureTask?.Wait(500); } catch { }
            IsRunning = false;
            return Task.CompletedTask;
        }

        private void CaptureLoop(CancellationToken token)
        {
            int delayMs = 1000 / _targetFps;
            int screenW = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            int screenH = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

            // Scale down to 1280x720 max for bandwidth
            double scale = Math.Min(1280.0 / screenW, 720.0 / screenH);
            if (scale > 1.0) scale = 1.0;
            int outW = (int)(screenW * scale);
            int outH = (int)(screenH * scale);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var fullBmp = new Bitmap(screenW, screenH);
                    using (var g = Graphics.FromImage(fullBmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new Size(screenW, screenH));
                    }

                    using var scaled = new Bitmap(outW, outH);
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(fullBmp, 0, 0, outW, outH);
                    }

                    var frame = new Bitmap(scaled);
                    FrameCaptured?.Invoke(frame);
                }
                catch (Exception ex)
                {
                    CaptureError?.Invoke(this, ex);
                    break;
                }

                try { Task.Delay(delayMs, token).Wait(token); }
                catch { break; }
            }

            IsRunning = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts?.Cancel(); } catch { }
            try { _captureTask?.Wait(300); } catch { }
            _cts?.Dispose();
            _cts = null;
        }
    }
}
