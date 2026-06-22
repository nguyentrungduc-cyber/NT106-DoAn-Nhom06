using System.Drawing.Drawing2D;
using System.Drawing.Text;
using SecureChat.Client.Services;

namespace SecureChat.Client.Components.Group
{
    /// <summary>
    /// Chip nhỏ hiển thị người dùng đã được chọn ở phần đầu hộp thoại Add Members.
    ///
    ///   ┌──────┐
    ///   │  ●  x│   Avatar tròn (44px) + nút X nhỏ góc trên phải
    ///   │ Name │   Tên rút gọn, căn giữa
    ///   └──────┘
    ///
    /// Nhấn X → fire RemoveClicked → parent sẽ bỏ chọn user đó.
    /// </summary>
    public partial class ucSelectedUser : UserControl
    {
        // ── Colors (paint-time from TG) ───────────────────
        private static readonly Color C_BTN_HOVER = Color.FromArgb(0xFF, 0x50, 0x50);

        // ── State ─────────────────────────────────────────
        private string _displayName = "";
        private Color _avatarColor = Color.Gray;
        private bool _xHovered = false;

        // ── Events ────────────────────────────────────────
        /// <summary>Fired khi nhấn X để bỏ chọn người này.</summary>
        public event Action RemoveClicked;

        // ── Properties ────────────────────────────────────
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value ?? ""; Invalidate(); }
        }

        public Color AvatarColor
        {
            get => _avatarColor;
            set { _avatarColor = value; Invalidate(); }
        }

        // ── Layout ────────────────────────────────────────
        private const int AVA_SZ = 44;
        private const int AVA_TOP = 12;
        private const int BTN_SZ = 18;

        private int AvaLeft => (Width - AVA_SZ) / 2;
        private Rectangle XBtnRect =>
            new Rectangle(AvaLeft + AVA_SZ - 10, AVA_TOP - 5, BTN_SZ, BTN_SZ);

        // ═══════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════
        public ucSelectedUser()
        {
            InitializeComponent();
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            BackColor = TG.WindowBg;
            Size = new Size(66, 76);
            Cursor = Cursors.Hand;
            Margin = new Padding(2, 4, 2, 4);
        }

        public ucSelectedUser(string displayName, Color avatarColor) : this()
        {
            _displayName = displayName;
            _avatarColor = avatarColor;
        }

        // ═══════════════════════════════════════════════════
        //  MOUSE
        // ═══════════════════════════════════════════════════
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool was = _xHovered;
            _xHovered = XBtnRect.Contains(e.Location);
            if (was != _xHovered) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_xHovered) { _xHovered = false; Invalidate(); }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_xHovered) RemoveClicked?.Invoke();
        }

        // ═══════════════════════════════════════════════════
        //  PAINT
        // ═══════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(TG.WindowBg);

            int avaL = AvaLeft;

            // ── Avatar ────────────────────────────────────
            var avaRect = new Rectangle(avaL, AVA_TOP, AVA_SZ, AVA_SZ);
            using (var b = new SolidBrush(_avatarColor))
                g.FillEllipse(b, avaRect);

            string letter = string.IsNullOrEmpty(_displayName)
                ? "?"
                : _displayName[0].ToString().ToUpper();
            using var fnt = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel);
            var sfC = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(letter, fnt, Brushes.White, avaRect, sfC);

            // ── X button ─────────────────────────────────
            var xR = XBtnRect;
            using (var btnBrush = new SolidBrush(_xHovered ? C_BTN_HOVER : TG.TextSecondary))
                g.FillEllipse(btnBrush, xR);

            // Viền trắng để tách khỏi avatar
            using (var borderPen = new Pen(TG.WindowBg, 2f))
                g.DrawEllipse(borderPen, xR);

            // Dấu X
            using var xPen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int mx = xR.X + xR.Width / 2, my = xR.Y + xR.Height / 2, d = 3;
            g.DrawLine(xPen, mx - d, my - d, mx + d, my + d);
            g.DrawLine(xPen, mx + d, my - d, mx - d, my + d);

            // ── Name ─────────────────────────────────────
            using var fntName = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var nameBrush = new SolidBrush(TG.TextPrimary);
            g.DrawString(_displayName, fntName, nameBrush,
                new RectangleF(0f, AVA_TOP + AVA_SZ + 5f, Width, 18f),
                new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap,
                });
        }
    }
}