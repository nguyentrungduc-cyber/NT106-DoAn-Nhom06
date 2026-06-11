using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace SecureChat.Client.Forms.Call
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  frmVideoCall  –  Telegram-style video call UI (C# / WinForms / OpenCvSharp)
    //  Layout:  full-screen remote feed → draggable local preview (bottom-right)
    //           → avatar overlay while no remote video → gradient bottom bar
    // ─────────────────────────────────────────────────────────────────────────────
    public sealed class frmVideoCall : Form
    {
        // ── Controls ─────────────────────────────────────────────────────────────
        private PictureBox picRemoteVideo = null!;   // full-screen remote feed
        private Panel pnlGradientTop = null!;   // subtle top fade (caller name)
        private Panel pnlGradientBot = null!;   // bottom fade + action bar
        private Panel pnlActionDock = null!;
        private Panel pnlAvatar = null!;   // shown while waiting / no remote
        private Label lblInitials = null!;
        private Label lblCallerName = null!;
        private Label lblStatus = null!;
        private Label lblTimer = null!;

        // local preview (drag-able)
        private Panel pnlLocalWrap = null!;
        private PictureBox picLocal = null!;
        private Label lblCamOff = null!;

        // action buttons  (drawn with GDI – no emoji, no font issues)
        private CallButton btnMic = null!;
        private CallButton btnCamera = null!;
        private CallButton btnHangUp = null!;

        // title-bar emulation
        private Button btnSysClose = null!;
        private Button btnSysMax = null!;
        private Button btnSysMin = null!;

        // ── State ─────────────────────────────────────────────────────────────────
        private bool isMuted;
        private bool isCameraOn = true;

        private VideoCapture? localCapture;
        private readonly object captureLock = new();
        private readonly object remoteLock = new();
        private readonly object latestLocalFrameLock = new();
        private readonly CancellationTokenSource cts = new();

        private Bitmap? latestLocalFrame;
        private Task? localCaptureLoopTask;
        private bool hasRemoteVideo;
        private DateTime lastRemoteFrameUtc = DateTime.MinValue;
        private bool isFormActive = true;
        private bool allowCapture = true;

        // drag state for local preview
        private bool isDragging;
        private bool localPreviewMovedByUser;
        private Point dragOriginMouse;
        private Point dragOriginCtrl;

        // timers
        private System.Windows.Forms.Timer frameTimer = null!;
        private System.Windows.Forms.Timer clockTimer = null!;
        private System.Windows.Forms.Timer overlayTimer = null!;
        private System.Windows.Forms.Timer previewSnapTimer = null!;
        private DateTime callStart;
        private DateTime lastInteractionUtc = DateTime.UtcNow;
        private bool overlaysVisible = true;
        private int overlayAlphaCurrent = 255;
        private int overlayAlphaTarget = 255;

        private Point snapFrom;
        private Point snapTo;
        private DateTime snapStartedUtc;
        private bool snappingPreview;

        // ── Telegram brand colours ────────────────────────────────────────────────
        private static readonly Color TgBlue = Color.FromArgb(0x2C, 0xA5, 0xE0); // #2CA5E0
        private static readonly Color TgBg = Color.White;
        private static readonly Color TgBgLight = Color.FromArgb(0xF4, 0xF6, 0xF8);
        private static readonly Color TgRed = Color.FromArgb(0xE5, 0x35, 0x3B);
        private static readonly Color TgGreen = Color.FromArgb(0x21, 0xA1, 0x66);
        private static readonly Color TgTextMain = Color.FromArgb(0x1F, 0x2D, 0x3D);
        private static readonly Color TgTextSub = Color.FromArgb(0x7D, 0x8B, 0x98);

        // ─────────────────────────────────────────────────────────────────────────
        public frmVideoCall(string friendName = "Friend")
        {
            InitializeComponent();
            ApplyFriendInfo(friendName);

            callStart = DateTime.UtcNow;
            clockTimer.Start();

            Shown += (_, __) => _ = StartLocalCameraAsync();
            FormClosed += (_, __) => Cleanup();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ═════════════════════════════════════════════════════════════════════════
        private void InitializeComponent()
        {
            SuspendLayout();

            // ── Form ─────────────────────────────────────────────────────────────
            Text = "Video Call";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = TgBg;
            ClientSize = new Size(1180, 720);
            MinimumSize = new Size(800, 520);
            KeyPreview = true;
            Font = SystemFont(10f);

            EnableDoubleBuffer(this);

            // ── 1. Remote video background ────────────────────────────────────────
            picRemoteVideo = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = TgBg,
                SizeMode = PictureBoxSizeMode.Zoom   // letterbox (keeps aspect ratio)
            };
            Controls.Add(picRemoteVideo);

            // ── 2. Avatar overlay (shown while no remote feed) ─────────────────────
            pnlAvatar = new Panel
            {
                Size = new Size(120, 120),
                BackColor = TgBlue,
                Cursor = Cursors.Default
            };
            pnlAvatar.Paint += PnlAvatar_Paint;

            lblInitials = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = SystemFont(36f, FontStyle.Bold),
                Text = "F",
                BackColor = Color.Transparent
            };
            pnlAvatar.Controls.Add(lblInitials);
            Controls.Add(pnlAvatar);

            // ── 3. Top gradient bar (caller name + timer) ─────────────────────────
            pnlGradientTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.Transparent
            };
            pnlGradientTop.Paint += (s, e) => FillGradient(e.Graphics,
                pnlGradientTop.ClientRectangle,
                Color.FromArgb((int)(180f * overlayAlphaCurrent / 255f), 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0), 90f);

            lblCallerName = new Label
            {
                AutoSize = true,
                ForeColor = TgTextMain,
                Font = SystemFont(18f, FontStyle.Bold),
                Text = "Friend",
                BackColor = Color.Transparent
            };

            lblStatus = new Label
            {
                AutoSize = true,
                ForeColor = TgTextSub,
                Font = SystemFont(11f),
                Text = "Calling...",
                BackColor = Color.Transparent
            };

            lblTimer = new Label
            {
                AutoSize = true,
                ForeColor = TgTextSub,
                Font = SystemFont(11f),
                Text = "00:00",
                BackColor = Color.Transparent
            };

            // Custom title bar (minimize / maximize / close)
            btnSysMin = BuildSysBtn("—");
            btnSysMax = BuildSysBtn("❑");
            btnSysClose = BuildSysBtn("✕");
            btnSysMin.Click += (_, __) => WindowState = FormWindowState.Minimized;
            btnSysMax.Click += (_, __) => WindowState = WindowState == FormWindowState.Maximized
                                            ? FormWindowState.Normal : FormWindowState.Maximized;
            btnSysClose.Click += (_, __) => Close();

            pnlGradientTop.Controls.AddRange(new Control[]
            { lblCallerName, lblStatus, lblTimer, btnSysClose, btnSysMax, btnSysMin });

            pnlGradientTop.Resize += (_, __) => LayoutTopBar();
            Controls.Add(pnlGradientTop);

            // Drag form via top bar
            pnlGradientTop.MouseDown += FormDrag_MouseDown;
            lblCallerName.MouseDown += FormDrag_MouseDown;
            lblStatus.MouseDown += FormDrag_MouseDown;

            // ── 4. Bottom gradient bar (action buttons) ───────────────────────────
            pnlGradientBot = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 154,
                BackColor = Color.Transparent
            };
            pnlGradientBot.Paint += (s, e) => FillGradient(e.Graphics,
                pnlGradientBot.ClientRectangle,
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb((int)(200f * overlayAlphaCurrent / 255f), 0, 0, 0), 90f);

            pnlActionDock = new Panel
            {
                Size = new Size(340, 118),
                BackColor = Color.Transparent
            };
            pnlActionDock.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, pnlActionDock.Width - 1, pnlActionDock.Height - 1), 28);
                using var bg = new SolidBrush(Color.FromArgb(68, 16, 23, 32));
                e.Graphics.FillPath(bg, path);

                // subtle top highlight only (no square border frame)
                using var hiPen = new Pen(Color.FromArgb(50, 255, 255, 255), 1f);
                e.Graphics.DrawArc(hiPen, 12, 8, pnlActionDock.Width - 24, 20, 200, 140);
            };
            ApplyRoundedRegion(pnlActionDock, 28);
            pnlGradientBot.Controls.Add(pnlActionDock);

            // Five buttons: Mic | Camera | HangUp | Speaker | Switch
            btnMic = new CallButton(CallIconType.Mic, TgBgLight) { Size = new Size(66, 66) };
            btnCamera = new CallButton(CallIconType.Camera, TgBgLight) { Size = new Size(66, 66) };
            btnHangUp = new CallButton(CallIconType.HangUp, TgRed) { Size = new Size(78, 78) };

            var lblMic = BotLabel("Mute");
            var lblCamera = BotLabel("Camera");
            var lblHangUp = BotLabel("End");

            btnMic.Click += (_, __) => ToggleMic();
            btnCamera.Click += (_, __) => ToggleCamera();
            btnHangUp.Click += (_, __) => Close();

            pnlActionDock.Controls.AddRange(new Control[]
            { btnMic, btnCamera, btnHangUp,
              lblMic, lblCamera, lblHangUp });

            pnlGradientBot.Resize += (_, __) =>
            {
                pnlActionDock.Location = new Point((pnlGradientBot.Width - pnlActionDock.Width) / 2, 18);
                LayoutBottomBar(lblMic, lblCamera, lblHangUp);
            };

            Controls.Add(pnlGradientBot);

            // ── 5. Local preview (drag-able, bottom-right corner) ─────────────────
            pnlLocalWrap = new Panel
            {
                Size = new Size(224, 126),
                BackColor = Color.Black,
                Cursor = Cursors.SizeAll
            };
            pnlLocalWrap.Paint += PnlLocalWrap_Paint;

            picLocal = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            lblCamOff = new Label
            {
                Text = "Camera off",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = TgTextSub,
                Font = SystemFont(10f),
                BackColor = Color.Transparent,
                Visible = false
            };

            pnlLocalWrap.Controls.Add(picLocal);
            pnlLocalWrap.Controls.Add(lblCamOff);
            Controls.Add(pnlLocalWrap);

            AttachDrag(pnlLocalWrap);
            AttachDrag(picLocal);
            AttachDrag(lblCamOff);

            // ── Timers ────────────────────────────────────────────────────────────
            frameTimer = new System.Windows.Forms.Timer { Interval = 33 };   // smooth UI ~30 fps
            frameTimer.Tick += FrameTimer_Tick;

            clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += ClockTimer_Tick;

            overlayTimer = new System.Windows.Forms.Timer { Interval = 250 };
            overlayTimer.Tick += (_, __) => UpdateOverlayVisibilityByInactivity();
            overlayTimer.Start();

            previewSnapTimer = new System.Windows.Forms.Timer { Interval = 16 };
            previewSnapTimer.Tick += (_, __) => TickPreviewSnap();

            // ── Layout wiring ─────────────────────────────────────────────────────
            Load += (_, __) =>
            {
                EnableDoubleBufferRecursive(this);
                RepositionOverlays();
                NotifyUserInteraction();
            };
            Resize += (_, __) => RepositionOverlays();

            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                if (e.KeyCode == Keys.M) ToggleMic();
                if (e.KeyCode == Keys.V) ToggleCamera();
            };

            Activated += (_, __) =>
            {
                isFormActive = true;
                allowCapture = isCameraOn && WindowState != FormWindowState.Minimized;
                if (allowCapture && localCapture?.IsOpened() == true)
                    frameTimer.Start();
            };
            Deactivate += (_, __) =>
            {
                isFormActive = false;
                allowCapture = false;
                frameTimer.Stop();
            };
            SizeChanged += (_, __) =>
            {
                allowCapture = isCameraOn && isFormActive && WindowState != FormWindowState.Minimized;
                if (!allowCapture) frameTimer.Stop();
                else if (localCapture?.IsOpened() == true) frameTimer.Start();
            };

            MouseMove += (_, __) => NotifyUserInteraction();
            picRemoteVideo.MouseMove += (_, __) => NotifyUserInteraction();
            pnlGradientTop.MouseMove += (_, __) => NotifyUserInteraction();
            pnlGradientBot.MouseMove += (_, __) => NotifyUserInteraction();
            pnlLocalWrap.MouseMove += (_, __) => NotifyUserInteraction();

            ResumeLayout(false);
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  LAYOUT HELPERS
        // ═════════════════════════════════════════════════════════════════════════
        private void LayoutTopBar()
        {
            int h = pnlGradientTop.Height;
            int w = pnlGradientTop.Width;

            // Sys buttons (top-right)
            btnSysClose.Location = new Point(w - 38, 6);
            btnSysMax.Location = new Point(w - 74, 6);
            btnSysMin.Location = new Point(w - 110, 6);

            // Caller info centred
            int mid = w / 2;
            lblCallerName.Location = new Point(mid - lblCallerName.Width / 2, 14);
            lblStatus.Location = new Point(mid - lblStatus.Width / 2, lblCallerName.Bottom + 2);
            lblTimer.Location = new Point(mid - lblTimer.Width / 2, lblStatus.Bottom + 1);
        }

        private void LayoutBottomBar(Label lMic, Label lCam, Label lEnd)
        {
            int cx = pnlActionDock.Width / 2;
            int yB = 10;
            int gap = 92;

            // Order: camera | hangup | mic
            btnCamera.Location = new Point(cx - gap - btnCamera.Width / 2, yB + 4);
            btnHangUp.Location = new Point(cx - btnHangUp.Width / 2, yB);
            btnMic.Location = new Point(cx + gap - btnMic.Width / 2, yB + 4);

            PlaceLabel(lMic, btnMic);
            PlaceLabel(lCam, btnCamera);
            PlaceLabel(lEnd, btnHangUp);
        }

        private static void PlaceLabel(Label lbl, Control btn) =>
            lbl.Location = new Point(
                btn.Left + (btn.Width - lbl.Width) / 2,
                btn.Bottom + 6);

        private void RepositionOverlays()
        {
            // Avatar centred in the client area (above bottom bar)
            int usableH = ClientSize.Height - pnlGradientBot.Height - pnlGradientTop.Height;
            int avY = pnlGradientTop.Height + (usableH - pnlAvatar.Height) / 2 - 40;
            pnlAvatar.Location = new Point(
                (ClientSize.Width - pnlAvatar.Width) / 2,
                Math.Max(pnlGradientTop.Height + 8, avY));
            ApplyCircle(pnlAvatar);

            // Local preview – snap to bottom-right if user has not moved it
            if (!localPreviewMovedByUser && !isDragging)
                SnapLocalPreview();

            ClampLocalPreview();
            pnlGradientTop.BringToFront();
            pnlGradientBot.BringToFront();
            pnlLocalWrap.BringToFront();
        }

        private void SnapLocalPreview()
        {
            pnlLocalWrap.Location = new Point(
                ClientSize.Width - pnlLocalWrap.Width - 16,
                ClientSize.Height - pnlGradientBot.Height - pnlLocalWrap.Height - 16);
        }

        private void ClampLocalPreview()
        {
            int minX = 8, minY = pnlGradientTop.Height + 8;
            int maxX = ClientSize.Width - pnlLocalWrap.Width - 8;
            int maxY = ClientSize.Height - pnlGradientBot.Height - pnlLocalWrap.Height - 8;
            pnlLocalWrap.Location = new Point(
                Math.Clamp(pnlLocalWrap.Left, minX, Math.Max(minX, maxX)),
                Math.Clamp(pnlLocalWrap.Top, minY, Math.Max(minY, maxY)));
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  PAINT HANDLERS
        // ═════════════════════════════════════════════════════════════════════════
        private void PnlAvatar_Paint(object? s, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var br = new SolidBrush(TgBlue);
            e.Graphics.FillEllipse(br, 0, 0, pnlAvatar.Width - 1, pnlAvatar.Height - 1);
        }

        private void PnlLocalWrap_Paint(object? s, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0)))
            using (var shadowPath = RoundedRect(new Rectangle(2, 3, pnlLocalWrap.Width - 6, pnlLocalWrap.Height - 6), 14))
            {
                e.Graphics.FillPath(shadow, shadowPath);
            }

            using var borderPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.5f);
            using var path = RoundedRect(new Rectangle(0, 0, pnlLocalWrap.Width - 1, pnlLocalWrap.Height - 1), 14);
            e.Graphics.DrawPath(borderPen, path);
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  CAMERA / FRAMES
        // ═════════════════════════════════════════════════════════════════════════
        private async Task StartLocalCameraAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    if (cts.IsCancellationRequested) return;
                    var cap = new VideoCapture(0);
                    if (!cap.Open(0) || !cap.IsOpened()) { cap.Dispose(); return; }
                    cap.Set(VideoCaptureProperties.FrameWidth, 480);
                    cap.Set(VideoCaptureProperties.FrameHeight, 270);
                    cap.Set(VideoCaptureProperties.Fps, 24);
                    lock (captureLock)
                    {
                        localCapture?.Release();
                        localCapture?.Dispose();
                        localCapture = cap;
                    }
                }, cts.Token).ConfigureAwait(false);

                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(() =>
                    {
                        if (isCameraOn)
                        {
                            allowCapture = isFormActive && WindowState != FormWindowState.Minimized;
                            if (localCaptureLoopTask == null || localCaptureLoopTask.IsCompleted)
                                localCaptureLoopTask = Task.Run(LocalCaptureLoop, cts.Token);
                            frameTimer.Start();
                        }
                        ApplyRoundedRegion(pnlLocalWrap, 14);
                    }));
            }
            catch
            {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke(new Action(() =>
                    {
                        lblCamOff.Visible = true;
                        picLocal.Visible = false;
                    }));
            }
        }

        private async Task LocalCaptureLoop()
        {
            while (!cts.IsCancellationRequested)
            {
                if (!isCameraOn || !allowCapture)
                {
                    await Task.Delay(80, cts.Token).ConfigureAwait(false);
                    continue;
                }

                var frame = GrabLocalFrame();
                if (frame != null)
                {
                    lock (latestLocalFrameLock)
                    {
                        latestLocalFrame?.Dispose();
                        latestLocalFrame = frame;
                    }
                }

                await Task.Delay(33, cts.Token).ConfigureAwait(false);
            }
        }

        private void FrameTimer_Tick(object? s, EventArgs e)
        {
            if (!isCameraOn || !isFormActive || WindowState == FormWindowState.Minimized) return;

            Bitmap? next = null;
            lock (latestLocalFrameLock)
            {
                if (latestLocalFrame != null)
                {
                    next = latestLocalFrame;
                    latestLocalFrame = null;
                }
            }

            if (next == null) return;

            var old = picLocal.Image;
            picLocal.Image = next;
            old?.Dispose();
            lblCamOff.Visible = false;
        }

        private Bitmap? GrabLocalFrame()
        {
            try
            {
                using var mat = new Mat();
                lock (captureLock)
                {
                    if (localCapture == null || !localCapture.IsOpened()) return null;
                    if (!localCapture.Read(mat) || mat.Empty()) return null;
                }
                using var rgb = new Mat();
                // Keep native BGR pipeline when encoding to BMP to avoid color tint.
                mat.CopyTo(rgb);
                using var ms = new MemoryStream(rgb.ToBytes(".bmp"));
                using var tmp = new Bitmap(ms);
                return new Bitmap(tmp);         // detach from MemoryStream
            }
            catch { return null; }
        }

        // ── Public API ────────────────────────────────────────────────────────────
        public void UpdateRemoteFrame(Bitmap bmp)
        {
            if (bmp == null || IsDisposed) return;
            void Apply()
            {
                if (IsDisposed) return;
                lock (remoteLock)
                {
                    var old = picRemoteVideo.Image;
                    picRemoteVideo.Image = new Bitmap(bmp);
                    old?.Dispose();
                }
                if (!hasRemoteVideo)
                {
                    hasRemoteVideo = true;
                    pnlAvatar.Visible = false;
                    picRemoteVideo.SizeMode = PictureBoxSizeMode.Zoom;
                }

                lastRemoteFrameUtc = DateTime.UtcNow;
                lblStatus.Text = "Video call";
            }
            if (InvokeRequired) BeginInvoke((Action)Apply);
            else Apply();
        }

        public void UpdateRemoteFrame(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            try
            {
                using var ms = new MemoryStream(data);
                using var bmp = new Bitmap(ms);
                UpdateRemoteFrame(bmp);
            }
            catch { }
        }

        public void UpdateRemoteFrameBase64(string b64)
        {
            if (string.IsNullOrWhiteSpace(b64)) return;
            try { UpdateRemoteFrame(Convert.FromBase64String(b64)); } catch { }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  ACTION TOGGLES
        // ═════════════════════════════════════════════════════════════════════════
        private void ToggleMic()
        {
            NotifyUserInteraction();
            isMuted = !isMuted;
            btnMic.IsActive = isMuted;
            btnMic.Invalidate();
        }

        private void ToggleCamera()
        {
            NotifyUserInteraction();
            isCameraOn = !isCameraOn;
            btnCamera.IsActive = !isCameraOn;
            btnCamera.Invalidate();
            allowCapture = isCameraOn && isFormActive && WindowState != FormWindowState.Minimized;

            if (!isCameraOn)
            {
                frameTimer.Stop();
                var old = picLocal.Image;
                picLocal.Image = null;
                old?.Dispose();
                lblCamOff.Visible = true;
                picLocal.Visible = false;
            }
            else
            {
                lblCamOff.Visible = false;
                picLocal.Visible = true;
                if (localCapture?.IsOpened() == true)
                    frameTimer.Start();
                else
                    _ = StartLocalCameraAsync();
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  CLOCK
        // ═════════════════════════════════════════════════════════════════════════
        private void ClockTimer_Tick(object? s, EventArgs e)
        {
            var el = DateTime.UtcNow - callStart;
            lblTimer.Text = el.TotalHours >= 1
                ? el.ToString(@"hh\:mm\:ss")
                : el.ToString(@"mm\:ss");

            if (!hasRemoteVideo)
            {
                lblStatus.Text = el.TotalSeconds < 6 ? "Connecting..." : "Waiting for video...";
                pnlAvatar.Visible = true;
            }
            else
            {
                var stale = (DateTime.UtcNow - lastRemoteFrameUtc).TotalSeconds > 3;
                if (stale)
                {
                    hasRemoteVideo = false;
                    lblStatus.Text = "Reconnecting...";
                    pnlAvatar.Visible = true;
                }
                else
                {
                    lblStatus.Text = "Video call";
                    pnlAvatar.Visible = false;
                }
            }

            LayoutTopBar();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  DRAG – LOCAL PREVIEW
        // ═════════════════════════════════════════════════════════════════════════
        private void AttachDrag(Control c)
        {
            c.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                NotifyUserInteraction();
                isDragging = true;
                dragOriginMouse = Cursor.Position;
                dragOriginCtrl = pnlLocalWrap.Location;
            };
            c.MouseMove += (_, e) =>
            {
                if (!isDragging) return;
                var cur = Cursor.Position;
                pnlLocalWrap.Location = new Point(
                    dragOriginCtrl.X + cur.X - dragOriginMouse.X,
                    dragOriginCtrl.Y + cur.Y - dragOriginMouse.Y);
                ClampLocalPreview();
                localPreviewMovedByUser = true;
            };
            c.MouseUp += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = false;
                    StartPreviewSnapToNearestCorner();
                }
            };
        }

        private void StartPreviewSnapToNearestCorner()
        {
            int margin = 8;
            int topY = pnlGradientTop.Height + margin;
            int bottomY = ClientSize.Height - pnlGradientBot.Height - pnlLocalWrap.Height - margin;
            int leftX = margin;
            int rightX = ClientSize.Width - pnlLocalWrap.Width - margin;

            var candidates = new[]
            {
                new Point(leftX, topY),
                new Point(rightX, topY),
                new Point(leftX, bottomY),
                new Point(rightX, bottomY)
            };

            Point current = pnlLocalWrap.Location;
            Point best = candidates[0];
            int bestDist = DistanceSquared(current, best);
            for (int i = 1; i < candidates.Length; i++)
            {
                int d = DistanceSquared(current, candidates[i]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidates[i];
                }
            }

            snapFrom = pnlLocalWrap.Location;
            snapTo = best;
            snapStartedUtc = DateTime.UtcNow;
            snappingPreview = true;
            previewSnapTimer.Start();
        }

        private void TickPreviewSnap()
        {
            if (!snappingPreview)
            {
                previewSnapTimer.Stop();
                return;
            }

            double t = (DateTime.UtcNow - snapStartedUtc).TotalMilliseconds / 180.0;
            if (t >= 1.0)
            {
                pnlLocalWrap.Location = snapTo;
                snappingPreview = false;
                previewSnapTimer.Stop();
                return;
            }

            // Ease-out cubic
            double k = 1 - Math.Pow(1 - t, 3);
            int nx = snapFrom.X + (int)((snapTo.X - snapFrom.X) * k);
            int ny = snapFrom.Y + (int)((snapTo.Y - snapFrom.Y) * k);
            pnlLocalWrap.Location = new Point(nx, ny);
        }

        private static int DistanceSquared(Point a, Point b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  FORM DRAG (title bar)
        // ═════════════════════════════════════════════════════════════════════════
        private void FormDrag_MouseDown(object? s, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            // release capture and send WM_NCLBUTTONDOWN to let Windows drag the form
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, 0x2 /*HTCAPTION*/, IntPtr.Zero);
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  FRIEND INFO
        // ═════════════════════════════════════════════════════════════════════════
        private void ApplyFriendInfo(string name)
        {
            lblCallerName.Text = name;
            lblInitials.Text = Initials(name);
            pnlAvatar.Visible = !hasRemoteVideo;
            LayoutTopBar();
        }

        private static string Initials(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "?";
            var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpperInvariant()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  CLEANUP
        // ═════════════════════════════════════════════════════════════════════════
        private void Cleanup()
        {
            cts.Cancel();
            frameTimer.Stop();
            clockTimer.Stop();
            overlayTimer.Stop();

            try { localCaptureLoopTask?.Wait(120); } catch { }

            lock (captureLock)
            {
                localCapture?.Release();
                localCapture?.Dispose();
                localCapture = null;
            }

            lock (latestLocalFrameLock)
            {
                latestLocalFrame?.Dispose();
                latestLocalFrame = null;
            }

            picLocal.Image?.Dispose();
            picLocal.Image = null;
            picRemoteVideo.Image?.Dispose();
            picRemoteVideo.Image = null;

            frameTimer.Dispose();
            clockTimer.Dispose();
            overlayTimer.Dispose();
            previewSnapTimer.Dispose();
            cts.Dispose();
        }

        private void NotifyUserInteraction()
        {
            lastInteractionUtc = DateTime.UtcNow;
            SetOverlaysVisible(true);
        }

        private void UpdateOverlayVisibilityByInactivity()
        {
            if (isDragging || !hasRemoteVideo)
            {
                SetOverlaysVisible(true);
                return;
            }

            bool shouldShow = (DateTime.UtcNow - lastInteractionUtc).TotalSeconds < 3.2;
            SetOverlaysVisible(shouldShow);

            // Smooth alpha animation
            const int step = 22;
            if (overlayAlphaCurrent < overlayAlphaTarget)
                overlayAlphaCurrent = Math.Min(overlayAlphaTarget, overlayAlphaCurrent + step);
            else if (overlayAlphaCurrent > overlayAlphaTarget)
                overlayAlphaCurrent = Math.Max(overlayAlphaTarget, overlayAlphaCurrent - step);

            pnlGradientTop.Invalidate();
            pnlGradientBot.Invalidate();
        }

        private void SetOverlaysVisible(bool visible)
        {
            overlaysVisible = visible;
            overlayAlphaTarget = visible ? 255 : 0;
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  DRAWING / REGION HELPERS
        // ═════════════════════════════════════════════════════════════════════════
        private static void FillGradient(Graphics g, Rectangle r, Color top, Color bot, float angle)
        {
            if (r.Width == 0 || r.Height == 0) return;
            using var br = new LinearGradientBrush(r, top, bot, angle);
            g.FillRectangle(br, r);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius) * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static void ApplyRoundedRegion(Control c, int r)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using var path = RoundedRect(new Rectangle(0, 0, c.Width, c.Height), r);
            c.Region = new Region(path);
        }

        private static void ApplyCircle(Control c)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, c.Width, c.Height);
            c.Region = new Region(path);
        }

        // ═════════════════════════════════════════════════════════════════════════
        //  FONT HELPERS  (Segoe UI – always safe on Windows; no emoji needed)
        // ═════════════════════════════════════════════════════════════════════════
        private static Font SystemFont(float size, FontStyle style = FontStyle.Regular) =>
            new Font("Segoe UI", size, style, GraphicsUnit.Point);

        // ═════════════════════════════════════════════════════════════════════════
        //  CONTROL BUILDERS
        // ═════════════════════════════════════════════════════════════════════════
        private Button BuildSysBtn(string text)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(32, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                TabStop = false,
                Cursor = Cursors.Hand,
                Font = SystemFont(11f)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 255, 255, 255);
            return b;
        }

        private static Label BotLabel(string text) => new Label
        {
            AutoSize = true,
            Text = text,
            ForeColor = Color.FromArgb(210, 255, 255, 255),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point)
        };

        // ═════════════════════════════════════════════════════════════════════════
        //  DOUBLE-BUFFER
        // ═════════════════════════════════════════════════════════════════════════
        private static void EnableDoubleBuffer(Control c)
        {
            var prop = typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(c, true, null);
        }
        private static void EnableDoubleBufferRecursive(Control root)
        {
            EnableDoubleBuffer(root);
            foreach (Control child in root.Controls)
                EnableDoubleBufferRecursive(child);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  CallButton  –  custom round button drawn entirely with GDI (no font/emoji)
    // ─────────────────────────────────────────────────────────────────────────────
    internal enum CallIconType { Mic, Camera, HangUp, Speaker, Switch }

    internal sealed class CallButton : Control
    {
        public CallIconType Icon { get; }
        public Color BaseColor { get; }
        public bool IsActive { get; set; }   // "active" = warning state (red tint)

        private bool _hover;
        private bool _pressed;

        public CallButton(CallIconType icon, Color baseColor)
        {
            Icon = icon;
            BaseColor = baseColor;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = false;
                Invalidate();
                if (ClientRectangle.Contains(e.Location))
                    OnClick(EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }

        protected override void OnResize(EventArgs e) { Invalidate(); base.OnResize(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            var r = ClientRectangle;
            int w = r.Width;
            int h = r.Height;
            int cx = w / 2;
            int cy = h / 2;

            // Background
            Color bg = IsActive ? Color.FromArgb(0xE5, 0x35, 0x3B) : BaseColor;
            if (_pressed) bg = ControlPaint.Dark(bg, 0.10f);
            else if (_hover) bg = ControlPaint.Light(bg, 0.08f);

            using (var br = new SolidBrush(bg))
                g.FillEllipse(br, 1, 1, w - 2, h - 2);

            using (var edge = new Pen(Color.FromArgb(110, 255, 255, 255), 1.1f))
                g.DrawEllipse(edge, 1, 1, w - 3, h - 3);

            // Icon (slightly bolder and cleaner)
            using var pen = new Pen(Color.White, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var penW = new Pen(Color.White, 3.0f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

            switch (Icon)
            {
                case CallIconType.Mic:
                    DrawRoundedRectangle(g, pen, cx - 6, cy - 12, 12, 16, 6);
                    g.DrawLine(pen, cx, cy + 4, cx, cy + 12);
                    g.DrawArc(pen, cx - 10, cy + 0, 20, 14, 15, 150);
                    g.DrawLine(pen, cx - 6, cy + 12, cx + 6, cy + 12);
                    if (IsActive) DrawSlash(g, penW, cx, cy);
                    break;

                case CallIconType.Camera:
                    DrawRoundedRectangle(g, pen, cx - 14, cy - 8, 18, 14, 4);
                    g.DrawEllipse(pen, cx - 9, cy - 4, 8, 8);
                    g.FillPolygon(Brushes.White, new[]
                    {
                        new PointF(cx + 4,  cy - 4),
                        new PointF(cx + 13, cy - 8),
                        new PointF(cx + 13, cy + 7),
                        new PointF(cx + 4,  cy + 3)
                    });
                    if (IsActive) DrawSlash(g, penW, cx, cy);
                    break;

                case CallIconType.HangUp:
                    using (var p2 = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawArc(p2, cx - 16, cy - 10, 32, 22, 200, 140);
                        g.DrawLine(p2, cx - 12, cy + 5, cx - 6, cy + 1);
                        g.DrawLine(p2, cx + 12, cy + 5, cx + 6, cy + 1);
                    }
                    break;

                case CallIconType.Speaker:
                    // speaker cone
                    g.DrawPolygon(pen, new[]
                    {
                        new Point(cx - 9,  cy - 6),
                        new Point(cx - 3,  cy - 6),
                        new Point(cx + 4,  cy - 12),
                        new Point(cx + 4,  cy + 12),
                        new Point(cx - 3,  cy + 6),
                        new Point(cx - 9,  cy + 6)
                    });
                    if (!IsActive)
                    {
                        g.DrawArc(pen, cx + 4, cy - 7, 8, 14, -60, 120);   // inner arc
                        g.DrawArc(pen, cx + 6, cy - 11, 12, 22, -60, 120);  // outer arc
                    }
                    else
                        DrawSlash(g, penW, cx, cy);
                    break;

                case CallIconType.Switch:
                    // two circular arrows (flip camera icon)
                    g.DrawArc(pen, cx - 11, cy - 9, 14, 14, 180, 270);
                    g.DrawArc(pen, cx - 3, cy - 5, 14, 14, 0, 270);
                    // arrowheads
                    g.DrawLine(pen, cx - 11, cy - 2, cx - 7, cy + 2);
                    g.DrawLine(pen, cx - 11, cy - 2, cx - 15, cy + 2);
                    g.DrawLine(pen, cx + 11, cy + 2, cx + 7, cy - 2);
                    g.DrawLine(pen, cx + 11, cy + 2, cx + 15, cy - 2);
                    break;
            }
        }

        private static void DrawSlash(Graphics g, Pen pen, int cx, int cy)
        {
            using var slash = new Pen(Color.White, 2.8f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(slash, cx - 13, cy + 11, cx + 13, cy - 11);
        }

        private static void DrawRoundedRectangle(Graphics g, Pen p, int x, int y, int w, int h, int radius)
        {
            using var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            g.DrawPath(p, path);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  P/Invoke for borderless form drag
    // ─────────────────────────────────────────────────────────────────────────────
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, IntPtr lParam);
    }
}