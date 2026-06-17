using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Components.Chat
{
    /// <summary>
    /// Bong bóng tin nhắn voice với nút Play/Pause, seekbar và nhãn thời gian.
    /// Cách dùng:
    ///   var bubble = new ucAudioBubble(playbackService);
    ///   bubble.SetVoiceInfo(messageId, url, sha256, key, iv, durationSeconds, isOutgoing);
    /// </summary>
    public sealed class ucAudioBubble : UserControl
    {
        // ── Controls ──────────────────────────────────────────────────────────
        private Panel      _pnlBubble;
        private Button     _btnPlayPause;
        private TrackBar   _trackBar;
        private Label      _lblTime;
        private Label      _lblTitle;

        // ── Data ──────────────────────────────────────────────────────────────
        private string _messageId   = string.Empty;
        private string _url         = string.Empty;
        private string _sha256      = string.Empty;
        private byte[] _key         = Array.Empty<byte>();
        private byte[] _iv          = Array.Empty<byte>();
        private double _totalSeconds = 0;
        private bool   _seekingByUser = false;

        private readonly VoicePlaybackService _svc;

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color OutgoingBg  = Color.FromArgb(225, 245, 234);
        private static readonly Color IncomingBg  = Color.FromArgb(245, 248, 250);
        private static readonly Color AccentGreen = Color.FromArgb(36, 170, 107);
        private static readonly Color TextDark    = Color.FromArgb(30,  30,  30);
        private static readonly Color TextGray    = Color.FromArgb(100, 100, 100);

        // ── Constructor ───────────────────────────────────────────────────────

        public ucAudioBubble(VoicePlaybackService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));

            DoubleBuffered = true;
            BuildLayout();

            _svc.StateChanged    += OnServiceStateChanged;
            _svc.PositionChanged += OnPositionChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public bool IsOutgoing { get; private set; }

        public void SetVoiceInfo(string messageId, string url, string sha256,
                                 byte[] key, byte[] iv, double totalSeconds, bool isOutgoing)
        {
            _messageId    = messageId;
            _url          = url;
            _sha256       = sha256;
            _key          = key;
            _iv           = iv;
            _totalSeconds = Math.Max(1, totalSeconds);
            IsOutgoing    = isOutgoing;

            // Init UI
            _trackBar.Maximum    = (int)(_totalSeconds * 10); // 0.1s resolution
            _trackBar.Value      = 0;
            _lblTime.Text        = $"0:00 / {FormatTime(_totalSeconds)}";
            _lblTitle.Text       = "Voice message";
            _pnlBubble.BackColor = Color.Transparent;

            _pnlBubble.Invalidate();
            SetPlayPauseIcon(isPlaying: false);
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Sizes
            const int W = 300, H = 72;
            Size = new Size(W + 8, H + 8);

            _pnlBubble = new Panel
            {
                Location    = new Point(4, 4),
                Size        = new Size(W, H),
                BackColor   = Color.Transparent,
            };
            _pnlBubble.Paint += PnlBubble_Paint;

            // ── Play/Pause button ─────────────────────────────────────────────
            _btnPlayPause = new Button
            {
                Location    = new Point(10, 16),
                Size        = new Size(38, 38),
                FlatStyle   = FlatStyle.Flat,
                BackColor   = AccentGreen,
                ForeColor   = Color.White,
                Text        = "▶",
                Font        = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor      = Cursors.Hand,
                TabStop     = false,
            };
            _btnPlayPause.FlatAppearance.BorderSize = 0;
            _btnPlayPause.Click += BtnPlayPause_Click;
            MakeCircle(_btnPlayPause);

            // ── Title ─────────────────────────────────────────────────────────
            _lblTitle = new Label
            {
                Location  = new Point(58, 8),
                Size      = new Size(230, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = TextDark,
                Text      = "Voice message",
                BackColor = Color.Transparent,
            };

            // ── Seekbar ───────────────────────────────────────────────────────
            _trackBar = new TrackBar
            {
                Location    = new Point(54, 24),
                Size        = new Size(236, 24),
                Minimum     = 0,
                Maximum     = 100,
                Value       = 0,
                TickStyle   = TickStyle.None,
            };
            _trackBar.MouseDown += (s, e) => { _seekingByUser = true; };
            _trackBar.MouseUp   += TrackBar_MouseUp;

            // ── Time label ────────────────────────────────────────────────────
            _lblTime = new Label
            {
                Location  = new Point(58, 50),
                Size      = new Size(220, 16),
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = TextGray,
                Text      = "0:00 / 0:00",
                BackColor = Color.Transparent,
            };

            _pnlBubble.Controls.Add(_btnPlayPause);
            _pnlBubble.Controls.Add(_lblTitle);
            _pnlBubble.Controls.Add(_trackBar);
            _pnlBubble.Controls.Add(_lblTime);
            Controls.Add(_pnlBubble);
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        private void PnlBubble_Paint(object? sender, PaintEventArgs e)
        {
            var g    = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _pnlBubble.Width - 1, _pnlBubble.Height - 1);
            using var path  = RoundedRect(rect, 14);
            using var brush = new SolidBrush(IsOutgoing ? OutgoingBg : IncomingBg);
            g.FillPath(brush, path);
        }

        private static void MakeCircle(Button btn)
        {
            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using var p = new GraphicsPath();
                p.AddEllipse(r);
                btn.Region = new Region(p);
                using var br = new SolidBrush(AccentGreen);
                g.FillEllipse(br, r);
                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using var f = new Font("Segoe UI", 12f, FontStyle.Bold);
                g.DrawString(btn.Text, f, Brushes.White, r, sf);
            };
        }

        private static GraphicsPath RoundedRect(Rectangle b, int r)
        {
            int d = r * 2;
            var path = new GraphicsPath();
            path.AddArc(b.X, b.Y, d, d, 180, 90);
            path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
            path.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            path.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Events from service ───────────────────────────────────────────────

        private void OnServiceStateChanged(VoicePlaybackService.PlaybackState state, string msgId)
        {
            if (IsDisposed) return;
            bool isMe = (msgId == _messageId);

            SafeInvoke(() =>
            {
                if (isMe)
                {
                    switch (state)
                    {
                        case VoicePlaybackService.PlaybackState.Playing:
                            SetPlayPauseIcon(isPlaying: true);
                            _lblTitle.Text = "▶ Đang phát...";
                            break;
                        case VoicePlaybackService.PlaybackState.Paused:
                            SetPlayPauseIcon(isPlaying: false);
                            _lblTitle.Text = "⏸ Tạm dừng";
                            break;
                        case VoicePlaybackService.PlaybackState.Loading:
                            _btnPlayPause.Text    = "…";
                            _btnPlayPause.Enabled = false;
                            _lblTitle.Text        = "Đang tải...";
                            break;
                        case VoicePlaybackService.PlaybackState.Idle:
                            SetPlayPauseIcon(isPlaying: false);
                            _lblTitle.Text = "Voice message";
                            _trackBar.Value = 0;
                            _lblTime.Text = $"0:00 / {FormatTime(_totalSeconds)}";
                            break;
                    }
                }
                else
                {
                    // Bài khác đang phát — reset về idle
                    SetPlayPauseIcon(isPlaying: false);
                    _lblTitle.Text  = "Voice message";
                    _trackBar.Value = 0;
                    _lblTime.Text   = $"0:00 / {FormatTime(_totalSeconds)}";
                }
            });
        }

        private void OnPositionChanged(double current, double total, string msgId)
        {
            if (IsDisposed || msgId != _messageId || _seekingByUser) return;
            SafeInvoke(() =>
            {
                _totalSeconds = Math.Max(1, total);
                int val = (int)(current * 10);
                int max = (int)(_totalSeconds * 10);
                _trackBar.Maximum = max;
                if (val >= 0 && val <= max) _trackBar.Value = val;
                _lblTime.Text = $"{FormatTime(current)} / {FormatTime(_totalSeconds)}";
            });
        }

        // ── User interaction ──────────────────────────────────────────────────

        private async void BtnPlayPause_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_messageId)) return;
            try
            {
                await _svc.PlayOrToggleAsync(_messageId, _url, _sha256, _key, _iv);
            }
            catch (Exception ex)
            {
                SafeInvoke(() => MessageBox.Show(
                    FindForm(), ex.Message, "Lỗi phát audio",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning));
            }
        }

        private void TrackBar_MouseUp(object? sender, MouseEventArgs e)
        {
            _seekingByUser = false;
            double seekTo  = _trackBar.Value / 10.0;
            _svc.SeekTo(seekTo);
            _lblTime.Text = $"{FormatTime(seekTo)} / {FormatTime(_totalSeconds)}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetPlayPauseIcon(bool isPlaying)
        {
            _btnPlayPause.Text    = isPlaying ? "⏸" : "▶";
            _btnPlayPause.Enabled = true;
            _btnPlayPause.Invalidate();
        }

        private static string FormatTime(double totalSeconds)
        {
            var ts = TimeSpan.FromSeconds(totalSeconds);
            return ts.TotalHours >= 1
                ? ts.ToString(@"h\:mm\:ss")
                : ts.ToString(@"m\:ss");
        }

        private void SafeInvoke(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _svc.StateChanged    -= OnServiceStateChanged;
                _svc.PositionChanged -= OnPositionChanged;
            }
            base.Dispose(disposing);
        }
    }
}
