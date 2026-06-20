using System.Drawing.Drawing2D;

namespace SecureChat.Client.Components.Group
{
    public class ucGroupMemberItem : UserControl
    {
        private const int AVATAR_SIZE = 48;
        private const int LEFT_PAD = 12;
        private const int RIGHT_PAD = 10;
        private const int TEXT_LEFT = LEFT_PAD + AVATAR_SIZE + 12;
        private const int ITEM_HEIGHT = 78;
        private static readonly Color C_BG_HOVER = Color.FromArgb(0xF4, 0xF7, 0xFB);
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x2D, 0x3D);
        private static readonly Color C_SUBTEXT = Color.FromArgb(0x8A, 0x98, 0xA6);
        private static readonly Color C_ROLE = Color.FromArgb(0x7D, 0x5F, 0xC9);

        private PictureBox _avatar = null!;
        private Label _lblInitial = null!;
        private Label _lblName = null!;
        private Label _lblStatus = null!;
        private Label _lblRole = null!;
        private Panel _badge = null!;

        public string DisplayName
        {
            get => _lblName.Text;
            set => _lblName.Text = value;
        }

        public string Status
        {
            get => _lblStatus.Text;
            set => _lblStatus.Text = value;
        }

        public string Role
        {
            get => _lblRole.Text;
            set
            {
                _lblRole.Text = value;
                LayoutDynamic(); // phải co lại Width của tên, không chỉ định vị lại badge
                Invalidate();
            }
        }

        public Image AvatarImage
        {
            get => _avatar.Image;
            set
            {
                if (_avatar.Image != value)
                {
                    var old = _avatar.Image;
                    _avatar.Image = value;
                    old?.Dispose();
                }
                _lblInitial.Visible = value == null;
                _avatar.Invalidate();
            }
        }

        private Color _avatarColor = Color.FromArgb(0xFF, 0x6B, 0x81);
        public Color AvatarColor
        {
            get => _avatarColor;
            set
            {
                _avatarColor = value;
                _avatar.BackColor = value;
            }
        }

        public ucGroupMemberItem()
        {
            Height = ITEM_HEIGHT;
            Dock = DockStyle.Top;
            BackColor = Color.Transparent;
            DoubleBuffered = true;
            BuildUI();
        }

        private void BuildUI()
        {
            _avatar = new PictureBox
            {
                Size = new Size(AVATAR_SIZE, AVATAR_SIZE),
                Location = new Point(LEFT_PAD, 14),
                BackColor = _avatarColor,
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            _avatar.SizeChanged += (_, __) => ClipCircle(_avatar);
            _avatar.Paint += (_, __) => ClipCircle(_avatar);

            _lblInitial = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 16f),
                BackColor = Color.Transparent,
            };
            _avatar.Controls.Add(_lblInitial);

            _lblName = new Label
            {
                AutoSize = false,
                Location = new Point(TEXT_LEFT, 14),
                Size = new Size(240, 26),
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = C_TEXT,
                Text = "Name",
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

            _lblStatus = new Label
            {
                AutoSize = false,
                Location = new Point(TEXT_LEFT, 40),
                Size = new Size(240, 30), // 24 vẫn sát quá, bị cắt nhẹ đáy chữ - tăng lên 30
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = C_SUBTEXT,
                Text = "last seen...",
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

            _badge = new Panel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Visible = false,
            };
            _badge.Paint += (_, __) => { /* no background */ };

            _lblRole = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = C_ROLE,
                Text = string.Empty,
                BackColor = Color.Transparent,
            };
            _badge.Controls.Add(_lblRole);

            Controls.AddRange(new Control[] { _avatar, _lblName, _lblStatus, _badge });
            _badge.BringToFront();

            Resize += (_, __) => { LayoutDynamic(); };
            LayoutDynamic();

            MouseEnter += (_, __) => BackColor = C_BG_HOVER;
            MouseLeave += (_, __) => BackColor = Color.Transparent;
        }

        private void LayoutDynamic()
        {
            int fullWidth = Width - TEXT_LEFT - RIGHT_PAD;
            if (fullWidth < 80) fullWidth = 80;

            int nameWidth = fullWidth;

            // Badge nằm cùng dòng với TÊN (không phải dòng status, status ở dòng dưới)
            // -> chỉ tên cần nhường chỗ, status dùng full width.
            bool hasRole = !string.IsNullOrWhiteSpace(_lblRole?.Text);
            if (hasRole && _badge != null)
            {
                var textSize = TextRenderer.MeasureText(_lblRole.Text, _lblRole.Font);
                int badgeW = textSize.Width + _badge.Padding.Horizontal + 8; // gap tách tên và badge
                nameWidth = Math.Max(40, fullWidth - badgeW);
            }

            _lblName.Width = nameWidth;
            _lblStatus.Width = fullWidth;
            UpdateBadgeLayout();
        }

        private void UpdateBadgeLayout()
        {
            bool hasRole = !string.IsNullOrWhiteSpace(_lblRole.Text);
            _badge.Visible = hasRole;
            if (!hasRole) return;

            var textSize = TextRenderer.MeasureText(_lblRole.Text, _lblRole.Font);
            int paddingH = _badge.Padding.Horizontal;
            int paddingV = _badge.Padding.Vertical;
            _badge.Size = new Size(textSize.Width + paddingH, textSize.Height + paddingV);

            _badge.Left = Width - _badge.Width - RIGHT_PAD;
            _badge.Top = 18;
            _badge.BringToFront();
            _badge.Invalidate();
        }

        public void SetInitial(string text)
        {
            _lblInitial.Text = text;
        }

        /// <summary>
        /// Force tính lại layout (Width tên/status co theo badge). Gọi method này
        /// sau khi đã set xong Role + Width thật, để không phụ thuộc vào việc event
        /// Resize có fire đúng thứ tự hay không.
        /// </summary>
        public void RefreshLayout() => LayoutDynamic();

        private static void ClipCircle(PictureBox pb)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, pb.Width, pb.Height);
            pb.Region = new Region(path);
        }

        private static void DrawBadge(Graphics g, Rectangle bounds)
        {
            // No background drawing; role is plain text now
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
