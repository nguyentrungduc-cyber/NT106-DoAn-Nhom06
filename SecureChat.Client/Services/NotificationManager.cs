using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio.Wave;

namespace SecureChat.Client.Services
{
    public static class NotificationManager
    {
        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        private static readonly object _soundLock = new();
        private static readonly object _popupLock = new();
        private static WaveOutEvent? _waveOut;
        private static int _soundGen;

        private static readonly List<NotificationPopup> _activePopups = new();
        private const int MaxVisiblePopups = 3;
        private const int PopupSpacing = 8;
        private const int PopupWidth = 380;
        private const int PopupHeight = 88;
        private const int CornerRadius = 10;

        // Colors read from TG at paint time

        public static void FlashWindow(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }

        public static void StopFlash(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = handle,
                dwFlags = 0,
                uCount = 0,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }

        public static void PlayNotificationSound(int volumePercent)
        {
            int gen;
            lock (_soundLock)
            {
                _soundGen = (_soundGen == int.MaxValue) ? 1 : _soundGen + 1;
                gen = _soundGen;
                StopSoundUnderLock();
            }

            float volume = Math.Clamp(volumePercent / 100f, 0f, 1f);
            int sampleRate = 44100;

            var samples = new List<short>();
            void AddTone(double freq, int durationMs, double amplitude)
            {
                int count = sampleRate * durationMs / 1000;
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / sampleRate;
                    double env = Math.Min(1.0, (i + 1) / (double)(sampleRate * 10 / 1000));
                    double val = Math.Sin(2 * Math.PI * freq * t) * amplitude * env;
                    samples.Add((short)(val * 30000));
                }
            }

            AddTone(523, 100, 0.7);
            AddTone(659, 120, 0.5);

            int totalSamples = samples.Count;
            var bytes = new byte[totalSamples * 2];
            for (int i = 0; i < totalSamples; i++)
            {
                bytes[i * 2] = (byte)(samples[i] & 0xFF);
                bytes[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
            }

            var ms = new MemoryStream(bytes);
            var waveStream = new RawSourceWaveStream(ms, new WaveFormat(sampleRate, 16, 1));
            var waveOut = new WaveOutEvent { Volume = volume };
            waveOut.Init(waveStream);

            EventHandler<StoppedEventArgs>? handler = null;
            handler = (_, __) =>
            {
                lock (_soundLock)
                {
                    if (_soundGen == gen)
                        _waveOut = null;
                }
                waveOut.PlaybackStopped -= handler;
                waveOut.Dispose();
                waveStream.Dispose();
                ms.Dispose();
            };
            waveOut.PlaybackStopped += handler;

            lock (_soundLock)
            {
                if (_soundGen == gen)
                {
                    _waveOut = waveOut;
                    waveOut.Play();
                }
                else
                {
                    waveOut.PlaybackStopped -= handler;
                    waveOut.Dispose();
                    waveStream.Dispose();
                    ms.Dispose();
                }
            }
        }

        private static void StopSoundUnderLock()
        {
            if (_waveOut != null)
            {
                try { _waveOut.Stop(); } catch { }
                try { _waveOut.Dispose(); } catch { }
                _waveOut = null;
            }
        }

        public static void StopSound()
        {
            lock (_soundLock)
            {
                _soundGen++;
                StopSoundUnderLock();
            }
        }

        public static void ShowDesktopNotification(string title, string body, Action? onClick = null)
        {
            ShowDesktopNotification(title, body, null, title, false, onClick);
        }

        public static void ShowDesktopNotification(string title, string body,
            Image? avatarImage, string displayNameForInitials,
            bool isGroup, Action? onClick = null)
        {
            var popup = new NotificationPopup(title, body, avatarImage, displayNameForInitials, onClick);
            popup.FormClosed += (_, __) =>
            {
                lock (_popupLock)
                {
                    _activePopups.Remove(popup);
                    RepositionPopups();
                }
            };

            // Show() TRƯỚC khi add vào _activePopups/Reposition — Form cần có
            // Handle (window handle) đã tạo thì BeginInvoke mới hoạt động được.
            // Gọi sau cùng từng gây InvalidOperationException khi tạo nhiều
            // popup liên tiếp (vd nhiều thông báo realtime khi tạo nhóm mới).
            popup.Show();

            lock (_popupLock)
            {
                _activePopups.Add(popup);
                while (_activePopups.Count > MaxVisiblePopups)
                {
                    var oldest = _activePopups[0];
                    try { oldest.Close(); }
                    finally { _activePopups.RemoveAt(0); }
                }
                RepositionPopups();
            }
        }

        private static void RepositionPopups()
        {
            var screen = Screen.PrimaryScreen!.WorkingArea;
            int baseX = screen.Right - PopupWidth - 16;
            int baseY = screen.Bottom - PopupHeight - 16;

            for (int i = 0; i < _activePopups.Count; i++)
            {
                var p = _activePopups[i];
                if (!p.IsHandleCreated) continue; // tránh InvalidOperationException nếu Form chưa tạo handle
                int offset = (_activePopups.Count - 1 - i) * (PopupHeight + PopupSpacing);
                p.BeginInvoke(new Action(() =>
                {
                    try { p.Location = new Point(baseX, baseY - offset); } catch { }
                }));
            }
        }

        private static Color GetAvatarColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return Color.FromArgb(0x33, 0x99, 0xFF);
            var colors = new[]
            {
                Color.FromArgb(0xFF, 0x61, 0x6A), Color.FromArgb(0xFF, 0xA8, 0x43),
                Color.FromArgb(0xA0, 0xDE, 0x7E), Color.FromArgb(0x72, 0xD5, 0xFD),
                Color.FromArgb(0x2A, 0xAB, 0xEE), Color.FromArgb(0xE0, 0x71, 0x7D),
                Color.FromArgb(0xA9, 0x5D, 0xD8)
            };
            return colors[Math.Abs(name.GetHashCode()) % colors.Length];
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpperInvariant();
            var single = parts[0];
            return single.Length >= 2
                ? single[..2].ToUpperInvariant()
                : single.ToUpperInvariant();
        }

        private class NotificationPopup : Form
        {
            private readonly Action? _onClick;
            private System.Windows.Forms.Timer? _closeTimer;
            private System.Windows.Forms.Timer? _fadeTimer;
            private bool _closing;
            private bool _fadingOut;
            private const int FadeSteps = 20;
            private const int FadeInterval = 10;
            private int _fadeStep;
            private Image? _avatarImage;
            private Font? _popupFont;
            private readonly EventHandler _clickHandler;
            private PictureBox? _avatarCtrl;
            private Panel? _contentPanel;

            public NotificationPopup(string title, string body,
                Image? avatarImage, string displayNameForInitials,
                Action? onClick)
            {
                _onClick = onClick;
                _avatarImage = avatarImage;
                FormBorderStyle = FormBorderStyle.None;
                StartPosition = FormStartPosition.Manual;
                ShowInTaskbar = false;
                TopMost = true;
                Width = PopupWidth;
                Height = PopupHeight;
                BackColor = TG.WindowBg;
                Opacity = 0;

                _popupFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                Font = _popupFont;
                DoubleBuffered = true;

                var screen = Screen.PrimaryScreen!.WorkingArea;
                Location = new Point(screen.Right + 20, screen.Bottom - PopupHeight - 16);

                var avatarColor = GetAvatarColor(displayNameForInitials);
                var initials = GetInitials(displayNameForInitials);

                var borderPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = TG.CAccent,
                    Padding = new Padding(0, 0, 0, 0)
                };

                var leftAccent = new Panel
                {
                    Width = 4,
                    Dock = DockStyle.Left,
                    BackColor = TG.CAccent
                };

                _contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = TG.WindowBg,
                    Cursor = Cursors.Hand,
                    Padding = new Padding(12, 10, 12, 10)
                };

                _avatarCtrl = new PictureBox
                {
                    Size = new Size(38, 38),
                    Location = new Point(12, 10),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                var avatarBmp = new Bitmap(38, 38);
                using (var g = Graphics.FromImage(avatarBmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    var rect = new Rectangle(0, 0, 37, 37);
                    if (avatarImage != null)
                    {
                        using var path = new GraphicsPath();
                        path.AddEllipse(rect);
                        g.SetClip(path);
                        g.DrawImage(avatarImage, rect);
                        g.ResetClip();
                    }
                    else
                    {
                        g.FillEllipse(new SolidBrush(avatarColor), rect);
                        float fontSize = 14f;
                        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                        TextRenderer.DrawText(g, initials, font, rect, Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    }
                    using var borderPen = new Pen(TG.Divider, 1f);
                    g.DrawEllipse(borderPen, rect);
                }
                _avatarCtrl.Image = avatarBmp;

                var lblTitle = new Label
                {
                    Text = title,
                    ForeColor = TG.TextPrimary,
                    Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular),
                    AutoSize = true,
                    Location = new Point(56, 10),
                    MaximumSize = new Size(PopupWidth - 80, 0),
                    Cursor = Cursors.Hand
                };

                string timeStr = "now";
                var lblTime = new Label
                {
                    Text = timeStr,
                    ForeColor = TG.TextSecondary,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                    AutoSize = true,
                    Location = new Point(PopupWidth - 74, 12),
                    Cursor = Cursors.Hand
                };

                string displayBody = body ?? "";
                if (displayBody.Length > 90)
                    displayBody = displayBody[..90] + "...";

                var lblBody = new Label
                {
                    Text = displayBody,
                    ForeColor = TG.TextSecondary,
                    Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                    AutoSize = false,
                    Size = new Size(PopupWidth - 80, 36),
                    Location = new Point(56, 30),
                    Cursor = Cursors.Hand
                };

                _clickHandler = (_, __) => { _onClick?.Invoke(); SafeClose(); };
                _contentPanel.Click += _clickHandler;
                _avatarCtrl.Click += _clickHandler;
                lblTitle.Click += _clickHandler;
                lblBody.Click += _clickHandler;
                lblTime.Click += _clickHandler;

                _contentPanel.Controls.Add(_avatarCtrl);
                _contentPanel.Controls.Add(lblTitle);
                _contentPanel.Controls.Add(lblTime);
                _contentPanel.Controls.Add(lblBody);
                borderPanel.Controls.Add(leftAccent);
                borderPanel.Controls.Add(_contentPanel);
                Controls.Add(borderPanel);

                Shown += (_, __) =>
                {
                    StartFadeIn();
                    _closeTimer = new System.Windows.Forms.Timer { Interval = 4000 };
                    _closeTimer.Tick += (___, ____) => { _closeTimer?.Stop(); StartFadeOut(); };
                    _closeTimer.Start();
                };

                FormClosed += (_, __) =>
                {
                    _closeTimer?.Stop();
                    _closeTimer?.Dispose();
                    _closeTimer = null;
                    _fadeTimer?.Stop();
                    _fadeTimer?.Dispose();
                    _fadeTimer = null;
                    if (_avatarCtrl != null)
                    {
                        _avatarCtrl.Image?.Dispose();
                        _avatarCtrl.Image = null;
                    }
                    _avatarImage?.Dispose();
                    _avatarImage = null;
                    _popupFont?.Dispose();
                    _popupFont = null;
                    if (_contentPanel != null)
                    {
                        _contentPanel.Click -= _clickHandler;
                        if (_avatarCtrl != null) _avatarCtrl.Click -= _clickHandler;
                        lblTitle.Click -= _clickHandler;
                        lblBody.Click -= _clickHandler;
                        lblTime.Click -= _clickHandler;
                    }
                };
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ClassStyle |= 0x00020000;
                    return cp;
                }
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                using var path = new GraphicsPath();
                int r = CornerRadius;
                int w = Width;
                int h = Height;
                path.AddArc(0, 0, r, r, 180, 90);
                path.AddArc(w - r - 1, 0, r, r, 270, 90);
                path.AddArc(w - r - 1, h - r - 1, r, r, 0, 90);
                path.AddArc(0, h - r - 1, r, r, 90, 90);
                path.CloseFigure();
                Region = new Region(path);
            }

            private void StartFadeIn()
            {
                _fadeStep = 0;
                _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeInterval };
                _fadeTimer.Tick += (_, __) =>
                {
                    _fadeStep++;
                    Opacity = Math.Min(1.0, _fadeStep / (double)FadeSteps);

                    int slideOffset = (int)((1.0 - Opacity) * 30);
                    var screen = Screen.PrimaryScreen!.WorkingArea;
                    int targetY = screen.Bottom - PopupHeight - 16;

                    int stackIndex = 0;
                    lock (_popupLock)
                    {
                        for (int i = 0; i < _activePopups.Count; i++)
                        {
                            var p = _activePopups[i];
                            if (p != this && p.Visible && p.Top < this.Top)
                                stackIndex++;
                        }
                    }
                    int stackOffset = stackIndex * (PopupHeight + PopupSpacing);
                    if (!IsDisposed)
                        Location = new Point(screen.Right - PopupWidth - 16 + slideOffset, targetY - stackOffset);

                    if (_fadeStep >= FadeSteps)
                    {
                        _fadeTimer.Stop();
                        if (!IsDisposed)
                        {
                            Opacity = 1.0;
                            Location = new Point(screen.Right - PopupWidth - 16, targetY - stackOffset);
                        }
                    }
                };
                _fadeTimer.Start();
            }

            private void StartFadeOut()
            {
                if (_fadingOut) return;
                _fadingOut = true;

                if (_fadeTimer != null)
                {
                    _fadeTimer.Stop();
                    _fadeTimer.Dispose();
                    _fadeTimer = null;
                }

                _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeInterval };
                _fadeTimer.Tick += (_, __) =>
                {
                    Opacity = Math.Max(0, Opacity - 0.08);
                    if (Opacity <= 0)
                    {
                        _fadeTimer.Stop();
                        SafeClose();
                    }
                };
                _fadeTimer.Start();
            }

            private void SafeClose()
            {
                if (_closing) return;
                _closing = true;
                try { Close(); } catch { }
            }
        }
    }
}
