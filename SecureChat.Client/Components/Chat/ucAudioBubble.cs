using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client.Components.Chat
{
    /// <summary>
    /// Bong bóng tin nhắn voice với nút Play/Pause, seekbar tuỳ chỉnh và nhãn thời gian.
    /// Không dùng TrackBar (không hỗ trợ Transparent) — seek bar tự vẽ bằng Panel+Paint.
    /// </summary>
    public sealed class ucAudioBubble : UserControl
    {
        // ── Controls ──────────────────────────────────────────────────────────
        private Panel  _pnlBubble;
        private Button _btnPlayPause;
        private Panel  _pnlSeekTrack;   // nền xám
        private Panel  _pnlSeekFill;    // fill xanh
        private Panel  _pnlSeekThumb;   // chấm tròn
        private Label  _lblTime;
        private Label  _lblTitle;

        // ── Data ──────────────────────────────────────────────────────────────
        private string _messageId    = string.Empty;
        private string _url          = string.Empty;
        private string _sha256       = string.Empty;
        private byte[] _key          = Array.Empty<byte>();
        private byte[] _iv           = Array.Empty<byte>();
        private double _totalSeconds = 1;
        private bool   _seekDragging;

        private readonly VoicePlaybackService _svc;

        // ── Colors (paint-time reads from TG) ─────────────────────────────────

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
            _totalSeconds = totalSeconds > 0 ? totalSeconds : 1;
            IsOutgoing    = isOutgoing;

            SafeInvoke(() =>
            {
                _lblTime.Text  = $"0:00 / {FormatTime(_totalSeconds)}";
                _lblTitle.Text = "Voice message";
                ResetSeek();
                SetPlayPauseIcon(false);
                _pnlBubble.Invalidate();
            });
        }

        public void OnNightModeChanged()
        {
            SafeInvoke(() =>
            {
                _lblTitle.ForeColor = TG.TextPrimary;
                _lblTime.ForeColor = TG.TextSecondary;
                _pnlSeekTrack.BackColor = TG.SeekBg;
                _pnlSeekFill.BackColor = TG.AccentGreen;
                _pnlSeekThumb.BackColor = TG.WindowBg; // thumb màu nền để đồng bộ dark/light
                Invalidate(true);
            });
        }

        // ── Layout ────────────────────────────────────────────────────────────
        private void BuildLayout()
        {
            const int W = 300, H = 64;
            Size      = new Size(W + 8, H + 8);
            BackColor = Color.Transparent;

            // Bubble panel
            _pnlBubble = new Panel
            {
                Location  = new Point(4, 4),
                Size      = new Size(W, H),
                BackColor = Color.Transparent,
            };
            _pnlBubble.Paint += PnlBubble_Paint;

            // Play/Pause button — circle, drawn manually, căn giữa theo chiều dọc
            _btnPlayPause = new Button
            {
                Location  = new Point(12, (H - 38) / 2),
                Size      = new Size(38, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text      = "▶",
                Cursor    = Cursors.Hand,
                TabStop   = false,
            };
            _btnPlayPause.FlatAppearance.BorderSize         = 0;
            _btnPlayPause.FlatAppearance.MouseOverBackColor = Color.Transparent;
            _btnPlayPause.FlatAppearance.MouseDownBackColor = Color.Transparent;
            _btnPlayPause.Paint += BtnPlayPause_Paint;
            _btnPlayPause.Click += BtnPlayPause_Click;

            // Title — căn đều phía trên nội dung bên phải nút play
            _lblTitle = new Label
            {
                Location  = new Point(60, 10),
                Size      = new Size(228, 18),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = TG.TextPrimary,
                Text      = "Voice message",
                BackColor = Color.Transparent,
            };

            // Seek track (nền) — Panel tuỳ chỉnh, không dùng TrackBar
            _pnlSeekTrack = new Panel
            {
                Location  = new Point(60, 32),
                Size      = new Size(228, 6),
                BackColor = TG.SeekBg,
                Cursor    = Cursors.Hand,
            };
            _pnlSeekTrack.MouseDown += SeekTrack_MouseDown;
            _pnlSeekTrack.MouseMove += SeekTrack_MouseMove;

            // Seek fill (progress xanh)
            _pnlSeekFill = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(0, 6),
                BackColor = TG.AccentGreen,
            };

            // Seek thumb (chấm tròn trắng)
            _pnlSeekThumb = new Panel
            {
                Size      = new Size(14, 14),
                Location  = new Point(-7, -4),
                BackColor = TG.WindowBg,
                Cursor    = Cursors.Hand,
            };
            _pnlSeekThumb.Paint    += SeekThumb_Paint;
            _pnlSeekThumb.MouseDown += SeekThumb_MouseDown;
            _pnlSeekThumb.MouseMove += SeekThumb_MouseMove;
            _pnlSeekThumb.MouseUp   += SeekThumb_MouseUp;

            _pnlSeekFill.Controls.Add(_pnlSeekThumb);
            _pnlSeekTrack.Controls.Add(_pnlSeekFill);

            // Time label — sát ngay dưới seek track, không để khoảng trắng thừa
            _lblTime = new Label
            {
                Location  = new Point(60, 42),
                Size      = new Size(228, 16),
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = TG.TextSecondary,
                Text      = "0:00 / 0:00",
                BackColor = Color.Transparent,
            };

            _pnlBubble.Controls.Add(_btnPlayPause);
            _pnlBubble.Controls.Add(_lblTitle);
            _pnlBubble.Controls.Add(_pnlSeekTrack);
            _pnlBubble.Controls.Add(_lblTime);
            Controls.Add(_pnlBubble);
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        private void PnlBubble_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _pnlBubble.Width - 1, _pnlBubble.Height - 1);
            using var path  = RoundedRect(rect, 14);

            Color bg = IsOutgoing ? TG.MsgOutBg : TG.MsgInBg;

            using var brush = new SolidBrush(bg);
            g.FillPath(brush, path);

            // Viền nhẹ
            using var pen = new Pen(Color.FromArgb(30, 0, 0, 0), 1f);
            g.DrawPath(pen, path);
        }

        private void BtnPlayPause_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, _btnPlayPause.Width - 2, _btnPlayPause.Height - 2);

            // Circle fill
            using var brush = new SolidBrush(TG.AccentGreen);
            g.FillEllipse(brush, rect);

            // Icon ▶ hoặc ⏸
            string icon = _btnPlayPause.Text;
            using var font = new Font("Segoe UI", icon == "⏸" ? 9f : 10f, FontStyle.Bold);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(icon, font, Brushes.White, rect, sf);
        }

        private void SeekThumb_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, _pnlSeekThumb.Width - 2, _pnlSeekThumb.Height - 2);
            using var brush = new SolidBrush(TG.AccentGreen);
            g.FillEllipse(brush, rect);
        }

        // ── Seek interaction ──────────────────────────────────────────────────
        private void SeekTrack_MouseDown(object? sender, MouseEventArgs e)
            => ApplySeekX(e.X);

        private void SeekTrack_MouseMove(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ApplySeekX(e.X);
        }

        private void SeekThumb_MouseDown(object? sender, MouseEventArgs e)
            => _seekDragging = true;

        private void SeekThumb_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_seekDragging || e.Button != MouseButtons.Left) return;
            var pt = _pnlSeekTrack.PointToClient(_pnlSeekThumb.PointToScreen(e.Location));
            ApplySeekX(pt.X);
        }

        private void SeekThumb_MouseUp(object? sender, MouseEventArgs e)
        {
            _seekDragging = false;
            double fraction = (double)_pnlSeekFill.Width / _pnlSeekTrack.Width;
            _svc.SeekTo(fraction * _totalSeconds);
        }

        private void ApplySeekX(int x)
        {
            double fraction = Math.Max(0, Math.Min(1.0, (double)x / _pnlSeekTrack.Width));
            UpdateSeekVisual(fraction);
            _svc.SeekTo(fraction * _totalSeconds);
            _lblTime.Text = $"{FormatTime(fraction * _totalSeconds)} / {FormatTime(_totalSeconds)}";
        }

        private void UpdateSeekVisual(double fraction)
        {
            int fillW = (int)(fraction * _pnlSeekTrack.Width);
            _pnlSeekFill.Width   = fillW;
            _pnlSeekThumb.Left   = fillW - 7;
            _pnlSeekThumb.Top    = -4;
        }

        private void ResetSeek()
        {
            _pnlSeekFill.Width  = 0;
            _pnlSeekThumb.Left  = -7;
        }

        // ── Play/Pause button ─────────────────────────────────────────────────
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

        // ── Events from service ───────────────────────────────────────────────
        private void OnServiceStateChanged(VoicePlaybackService.PlaybackState state, string msgId)
        {
            if (IsDisposed) return;
            bool isMe = msgId == _messageId;
            SafeInvoke(() =>
            {
                if (isMe)
                {
                    switch (state)
                    {
                        case VoicePlaybackService.PlaybackState.Playing:
                            SetPlayPauseIcon(true);
                            _lblTitle.Text = "▶ Đang phát...";
                            break;
                        case VoicePlaybackService.PlaybackState.Paused:
                            SetPlayPauseIcon(false);
                            _lblTitle.Text = "⏸ Tạm dừng";
                            break;
                        case VoicePlaybackService.PlaybackState.Loading:
                            _btnPlayPause.Text    = "…";
                            _btnPlayPause.Enabled = false;
                            _lblTitle.Text        = "Đang tải...";
                            break;
                        case VoicePlaybackService.PlaybackState.Idle:
                            SetPlayPauseIcon(false);
                            _lblTitle.Text = "Voice message";
                            ResetSeek();
                            _lblTime.Text  = $"0:00 / {FormatTime(_totalSeconds)}";
                            break;
                    }
                }
                else
                {
                    // Bài khác đang phát — reset về idle
                    SetPlayPauseIcon(false);
                    _lblTitle.Text = "Voice message";
                    ResetSeek();
                    _lblTime.Text  = $"0:00 / {FormatTime(_totalSeconds)}";
                }
            });
        }

        private void OnPositionChanged(double current, double total, string msgId)
        {
            if (IsDisposed || msgId != _messageId || _seekDragging) return;
            SafeInvoke(() =>
            {
                _totalSeconds = Math.Max(1, total);
                double fraction = _totalSeconds > 0 ? current / _totalSeconds : 0;
                UpdateSeekVisual(fraction);
                _lblTime.Text = $"{FormatTime(current)} / {FormatTime(_totalSeconds)}";
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void SetPlayPauseIcon(bool isPlaying)
        {
            _btnPlayPause.Text    = isPlaying ? "⏸" : "▶";
            _btnPlayPause.Enabled = true;
            _btnPlayPause.Invalidate();
        }

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1
                ? ts.ToString(@"h\:mm\:ss")
                : ts.ToString(@"m\:ss");
        }

        private static GraphicsPath RoundedRect(Rectangle b, int r)
        {
            int d    = r * 2;
            var path = new GraphicsPath();
            path.AddArc(b.X, b.Y, d, d, 180, 90);
            path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
            path.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
            path.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void SafeInvoke(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }

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
