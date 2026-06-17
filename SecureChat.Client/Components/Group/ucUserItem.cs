using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SecureChat.Client.Components.Group
{
    /// <summary>
    /// Hiển thị một người dùng trong danh sách "Add Members".
    /// ┌────────────────────────────────────────────────────┐
    /// │  [●]  DisplayName                         [  ○  ] │
    /// │       last seen a long time ago                    │
    /// └────────────────────────────────────────────────────┘
    /// - Avatar tròn có chữ cái đầu + màu ngẫu nhiên
    /// - Tên hiển thị + trạng thái online
    /// - Checkbox tròn custom phía phải (giống Telegram)
    /// - Hover highlight + ripple animation khi click
    /// </summary>
    public partial class ucUserItem : UserControl
    {
        // ═══════════════════════════════════════════════════
        //  TELEGRAM LIGHT PALETTE
        // ═══════════════════════════════════════════════════
        private static readonly Color C_BG_NORMAL = Color.White;
        private static readonly Color C_BG_HOVER = Color.FromArgb(0xF4, 0xF7, 0xFB);
        private static readonly Color C_BG_PRESSED = Color.FromArgb(0xE8, 0xEE, 0xF5);
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x2D, 0x3D);
        private static readonly Color C_SUBTEXT = Color.FromArgb(0x8A, 0x98, 0xA6);
        private static readonly Color C_ACCENT = Color.FromArgb(0x2A, 0xAB, 0xEE);
        private static readonly Color C_SEPARATOR = Color.FromArgb(0xE8, 0xEC, 0xF1);
        private static readonly Color C_CHECK_EMPTY = Color.FromArgb(0xAA, 0xB4, 0xBE);

        // ═══════════════════════════════════════════════════
        //  STATE
        // ═══════════════════════════════════════════════════
        private bool _isChecked = false;
        private bool _hovered = false;
        private bool _pressed = false;
        private string _displayName = "";
        private string _subText = "last seen a long time ago";
        private Color _avatarColor = Color.Gray;

        // Ripple animation
        private Point _rippleCenter;
        private float _rippleRadius = 0f;
        private int _rippleAlpha = 0;
        private readonly System.Windows.Forms.Timer _rippleTimer = new System.Windows.Forms.Timer { Interval = 16 };

        // ═══════════════════════════════════════════════════
        //  EVENTS
        // ═══════════════════════════════════════════════════
        /// <summary>Fired khi người dùng check/uncheck item.</summary>
        public event Action<ucUserItem, bool> SelectionChanged;

        // ═══════════════════════════════════════════════════
        //  PROPERTIES
        // ═══════════════════════════════════════════════════
        public string UserId { get; set; } = string.Empty;

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value ?? ""; Invalidate(); }
        }

        public string SubText
        {
            get => _subText;
            set { _subText = value ?? ""; Invalidate(); }
        }

        public Color AvatarColor
        {
            get => _avatarColor;
            set { _avatarColor = value; Invalidate(); }
        }

        /// <summary>Trạng thái được chọn. Gán = true/false từ bên ngoài cũng fire event.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                Invalidate();
                SelectionChanged?.Invoke(this, _isChecked);
            }
        }

        // ═══════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════
        public ucUserItem()
        {
            InitializeComponent();
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            BackColor = C_BG_NORMAL;
            Height = 64;
            Cursor = Cursors.Hand;

            // Ripple tick
            _rippleTimer.Tick += (s, e) =>
            {
                _rippleRadius += 20f;
                _rippleAlpha = Math.Max(0, _rippleAlpha - 12);
                Invalidate();
                if (_rippleAlpha <= 0) _rippleTimer.Stop();
            };
        }

        /// <summary>Constructor tiện lợi để tạo nhanh từ dữ liệu người dùng.</summary>
        public ucUserItem(string displayName, string subText, Color avatarColor) : this()
        {
            _displayName = displayName;
            _subText = subText;
            _avatarColor = avatarColor;
        }

        // ═══════════════════════════════════════════════════
        //  MOUSE EVENTS
        // ═══════════════════════════════════════════════════
        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _pressed = true;
            _rippleCenter = e.Location;
            _rippleRadius = 0f;
            _rippleAlpha = 70;
            _rippleTimer.Start();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            IsChecked = !_isChecked;
        }

        // ═══════════════════════════════════════════════════
        //  PAINT
        // ═══════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // ── Background ────────────────────────────────
            Color bg = _pressed ? C_BG_PRESSED
                     : _hovered ? C_BG_HOVER
                     : C_BG_NORMAL;
            g.Clear(bg);

            // ── Layout constants ──────────────────────────
            const int PAD = 14;    // left/right padding
            const int AVA_SZ = 46;    // avatar diameter
            const int AVA_Y = (64 - AVA_SZ) / 2;
            const int CB_SZ = 22;    // checkbox diameter

            // ── Avatar circle ─────────────────────────────
            var avaRect = new Rectangle(PAD, AVA_Y, AVA_SZ, AVA_SZ);
            using (var avaBrush = new SolidBrush(_avatarColor))
                g.FillEllipse(avaBrush, avaRect);

            // Chữ cái đầu
            string letter = string.IsNullOrEmpty(_displayName)
                ? "?"
                : _displayName[0].ToString().ToUpper();
            using var fntInit = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel);
            var sfCenter = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(letter, fntInit, Brushes.White, avaRect, sfCenter);

            // ── Text ──────────────────────────────────────
            int cbX = Width - CB_SZ - PAD;
            int textX = PAD + AVA_SZ + 12;
            int textW = cbX - textX - 8;

            var sfTrim = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };

            // Tên người dùng
            using var fntName = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var nameBrush = new SolidBrush(C_TEXT);
            g.DrawString(_displayName, fntName, nameBrush,
                new RectangleF(textX, 12f, textW, 22f), sfTrim);

            // Trạng thái
            using var fntSub = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var subBrush = new SolidBrush(C_SUBTEXT);
            g.DrawString(_subText, fntSub, subBrush,
                new RectangleF(textX, 37f, textW, 18f), sfTrim);

            // ── Ripple effect ─────────────────────────────
            if (_rippleAlpha > 0)
            {
                using var rippleBrush = new SolidBrush(Color.FromArgb(_rippleAlpha, C_ACCENT));
                g.FillEllipse(rippleBrush,
                    _rippleCenter.X - _rippleRadius,
                    _rippleCenter.Y - _rippleRadius,
                    _rippleRadius * 2,
                    _rippleRadius * 2);
            }

            // ── Custom Checkbox (tròn như Telegram) ───────
            int cbY = (Height - CB_SZ) / 2;
            var cbRect = new Rectangle(cbX, cbY, CB_SZ, CB_SZ);

            if (_isChecked)
            {
                // Circle accent fill
                using var ckFill = new SolidBrush(C_ACCENT);
                g.FillEllipse(ckFill, cbRect);

                // Checkmark ✓
                using var ckPen = new Pen(Color.White, 2.2f)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round,
                };
                g.DrawLines(ckPen, new PointF[]
                {
                    new PointF(cbX + 5f,  cbY + 11f),
                    new PointF(cbX + 9f,  cbY + 15f),
                    new PointF(cbX + 17f, cbY + 7f),
                });
            }
            else
            {
                // Empty circle border
                using var emptyPen = new Pen(C_CHECK_EMPTY, 1.8f);
                g.DrawEllipse(emptyPen, cbRect);
            }

            // ── Separator ─────────────────────────────────
            using var sepPen = new Pen(C_SEPARATOR, 1f);
            g.DrawLine(sepPen, textX, Height - 1, Width, Height - 1);
        }
    }
}