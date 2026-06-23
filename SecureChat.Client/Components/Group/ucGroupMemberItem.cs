using System.Drawing.Drawing2D;

namespace SecureChat.Client.Components.Group
{
    public class ucGroupMemberItem : UserControl
    {
        private const int AVATAR_SIZE = 56;
        private const int LEFT_PAD = 12;
        private const int RIGHT_PAD = 10;
        private const int TEXT_LEFT = LEFT_PAD + AVATAR_SIZE + 12;
        public const int ITEM_HEIGHT = 100;

        private Panel _avatar = null!;
        private Image? _avatarImage;
        private string _initialText = "?";  // text để DrawCircleAvatar vẽ antialiased
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
            get => _avatarImage!;
            set
            {
                if (_avatarImage != value)
                {
                    var old = _avatarImage;
                    _avatarImage = value;
                    old?.Dispose();
                }
                _avatar.Invalidate(); // redraw với/không có ảnh
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
                _avatar.Invalidate();
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

        public void OnNightModeChanged()
        {
            _lblName.ForeColor = TG.TextPrimary;
            _lblStatus.ForeColor = TG.TextSecondary;
            _lblRole.ForeColor = TG.Blue;
            Invalidate();
        }

        private void BuildUI()
        {
            _avatar = new Panel
            {
                Size = new Size(AVATAR_SIZE, AVATAR_SIZE),
                Location = new Point(LEFT_PAD, 20),
                BackColor = Color.Transparent,
            };
            typeof(Panel).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_avatar, true);
            _avatar.Paint += (_, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode      = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode    = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                var rect = new Rectangle(1, 1, _avatar.Width - 2, _avatar.Height - 2);
                TG.DrawCircleAvatar(g, rect, _avatarImage, _initialText, _avatarColor, drawInitials: true);
            };

            _lblName = new Label
            {
                AutoSize = false,
                Location = new Point(TEXT_LEFT, 18),
                Size = new Size(280, 32),
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = TG.TextPrimary,
                Text = "Name",
                BackColor = Color.Transparent,
                AutoEllipsis = true,
            };

            _lblStatus = new Label
            {
                AutoSize = false,
                Location = new Point(TEXT_LEFT, 54),
                Size = new Size(280, 28),
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
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

            _lblRole = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = TG.Blue,
                Text = string.Empty,
                BackColor = Color.Transparent,
            };
            _badge.Controls.Add(_lblRole);

            Controls.AddRange(new Control[] { _avatar, _lblName, _lblStatus, _badge });
            _badge.BringToFront();

            Resize += (_, __) => { LayoutDynamic(); };
            LayoutDynamic();

            MouseEnter += (_, __) => BackColor = TG.SidebarHover;
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
            _initialText = text;
            _avatar.Invalidate();
        }

        /// <summary>
        /// Force tính lại layout (Width tên/status co theo badge). Gọi method này
        /// sau khi đã set xong Role + Width thật, để không phụ thuộc vào việc event
        /// Resize có fire đúng thứ tự hay không.
        /// </summary>
        public void RefreshLayout() => LayoutDynamic();
    }
}
