using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SecureChat.Client
{
    // ─────────────────────────────────────────────
    // ROUNDED PANEL
    // ─────────────────────────────────────────────
    public class RoundedPanel : Panel
    {
        public int Radius { get; set; } = TG.RadiusMedium;
        public Color BorderColor { get; set; } = Color.Transparent;
        public int BorderWidth { get; set; } = 0;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = GetRoundedPath(rect, Radius);
            using var brush = new SolidBrush(BackColor);
            e.Graphics.FillPath(brush, path);
            if (BorderWidth > 0 && BorderColor != Color.Transparent)
            {
                using var pen = new Pen(BorderColor, BorderWidth);
                e.Graphics.DrawPath(pen, path);
            }
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            int maxRadius = Math.Min(rect.Width / 2, rect.Height / 2);
            int r = Math.Clamp(radius, 0, maxRadius);

            if (r <= 0)
            {
                path.AddRectangle(rect);
                path.CloseFigure();
                return path;
            }

            int d = r * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // ─────────────────────────────────────────────
    // TELEGRAM BUTTON
    // ─────────────────────────────────────────────
    public class TelegramButton : Button
    {
        private bool _isHovered, _isPressed;
        public int Radius { get; set; } = TG.RadiusLarge;
        public Color NormalColor { get; set; } = TG.Blue;
        public Color HoverColor { get; set; } = TG.BlueHover;
        public Color PressColor { get; set; } = TG.BlueActive;
        public Color TextColor { get; set; } = Color.White;
        public bool IsOutlined { get; set; } = false;

        public TelegramButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = TG.FontSemiBold(10f);
            ForeColor = Color.White;
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _isPressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _isPressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // --- DÒNG FIX QUAN TRỌNG ---
            // Xóa sạch dấu vết cũ bằng màu của Panel cha để tránh bị "xanh phủ"
            if (this.Parent != null)
            {
                using var parentBrush = new SolidBrush(this.Parent.BackColor);
                e.Graphics.FillRectangle(parentBrush, this.ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedPanel.GetRoundedPath(rect, Radius);

            Color bg = _isPressed ? PressColor : _isHovered ? HoverColor : NormalColor;

            if (IsOutlined)
            {
                // e.Graphics.FillPath(new SolidBrush(Color.White), path);
                // e.Graphics.DrawPath(new Pen(NormalColor, 1.5f), path);
                // Nếu dùng NormalColor = Transparent, hãy vẽ màu trắng làm nền
                e.Graphics.FillPath(Brushes.White, path);
                e.Graphics.DrawPath(new Pen(NormalColor, 1.5f), path);


                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(Text, Font, new SolidBrush(NormalColor), rect, sf);
            }
            else
            {
                /*e.Graphics.FillPath(new SolidBrush(bg), path);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(Text, Font, new SolidBrush(TextColor), rect, sf);*/
                // Nếu bg là Transparent, WinForms sẽ bị rác, nên ta kiểm tra:
                if (bg != Color.Transparent)
                {
                    using var brush = new SolidBrush(bg);
                    e.Graphics.FillPath(brush, path);
                }

                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var textBrush = new SolidBrush(TextColor);
                e.Graphics.DrawString(Text, Font, textBrush, rect, sf);

            }
        }
    }

    // ─────────────────────────────────────────────
    // AVATAR CONTROL
    // ─────────────────────────────────────────────
    public class AvatarControl : Control
    {
        public string DisplayName { get; set; } = "";
        public Image Photo { get; set; } = null;
        public bool ShowOnline { get; set; } = false;
        private Color _avatarColor;

        public AvatarControl()
        {
            // Giữ các style cũ và thêm các style tối ưu mới
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.AllPaintingInWmPaint | // Quan trọng: Bỏ qua tin nhắn vẽ nền của Windows để giảm nháy
                     ControlStyles.OptimizedDoubleBuffer, // Quan trọng: Vẽ trong bộ nhớ trước khi đẩy ra màn hình
                     true);

            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        public void SetName(string name)
        {
            DisplayName = name;
            _avatarColor = TG.GetAvatarColor(name);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;


            // --- DÒNG FIX QUAN TRỌNG ---
            if (this.Parent != null)
            {
                using var parentBrush = new SolidBrush(this.Parent.BackColor);
                e.Graphics.FillRectangle(parentBrush, this.ClientRectangle);
            }



            int size = Math.Min(Width, Height);
            var rect = new Rectangle(0, 0, size - 1, size - 1);

            if (Photo != null)
            {
                using var path = new GraphicsPath();
                path.AddEllipse(rect);
                e.Graphics.SetClip(path);
                e.Graphics.DrawImage(Photo, rect);
                e.Graphics.ResetClip();
            }
            else
            {
                e.Graphics.FillEllipse(new SolidBrush(_avatarColor == default ? TG.Blue : _avatarColor), rect);
                string initials = GetInitials(DisplayName);

                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                /*
                float fontSizee = size * 0.32f;

                // e.Graphics.DrawString(initials, TG.FontSemiBold(fontSize), Brushes.White, rect, sf);
                // TẠO RECT RIÊNG CHO CHỮ: Đẩy Y xuống 2 pixel để bù trừ độ lệch của Font
                RectangleF textRect = new RectangleF(rect.X, rect.Y + 2, rect.Width, rect.Height);

                e.Graphics.DrawString(initials, TG.FontSemiBold(fontSizee), Brushes.White, textRect, sf);
                */

                // Draw initials as strict single-line text to avoid wrap/clipping artifacts.
                float fontSize = Math.Max(10f, size * 0.34f);
                using var textFont = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                TextRenderer.DrawText(
                    e.Graphics,
                    initials,
                    textFont,
                    rect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);
            }

            if (ShowOnline)
            {
                int dotSize = Math.Max(8, size / 5);
                int dotX = size - dotSize;
                int dotY = size - dotSize;
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(0xFF, 0xFF, 0xFF)), dotX - 1, dotY - 1, dotSize + 2, dotSize + 2);
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(0x4D, 0xD9, 0x64)), dotX, dotY, dotSize, dotSize);
            }
        }

        private string GetInitials(string name)
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
    }

    // ─────────────────────────────────────────────
    // TELEGRAM TEXTBOX
    // ─────────────────────────────────────────────
    public class TelegramTextBox : Panel
    {
        private TextBox _tb = null!;
        private string? _placeholder;
        private bool _isFocused;
        private Label _placeholderLabel = null!;
        private Button _btnTogglePassword = null!;
        private bool _isPasswordVisible = false;
        private char _defaultPasswordChar = '●';

        public new string Text { get => _tb.Text; set => _tb.Text = value; }
        public char PasswordChar { get => _tb.PasswordChar; set => _tb.PasswordChar = value; }
        public bool Multiline { get => _tb.Multiline; set => _tb.Multiline = value; }
        public new event EventHandler TextChanged { add => _tb.TextChanged += value; remove => _tb.TextChanged -= value; }

        public TelegramTextBox()
        {
            Height = 44;
            BackColor = TG.InputBg;
            DoubleBuffered = true;
            Padding = new Padding(12, 0, 12, 0);

            _tb = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = TG.FontRegular(10f),
                ForeColor = TG.TextPrimary,
                BackColor = TG.InputBg,
                Dock = DockStyle.Fill,
                TabStop = true,
            };

            _placeholderLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = TG.FontRegular(10f),
                ForeColor = TG.TextHint,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0),
                Cursor = Cursors.IBeam,
            };
            _placeholderLabel.Click += (s, e) => _tb.Focus();

            // Nút toggle hiện/ẩn mật khẩu
            _btnTogglePassword = new Button
            {
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance =
                {
                    BorderSize = 0,
                    MouseOverBackColor = Color.Transparent, 
                    MouseDownBackColor = Color.Transparent  
                },
                Cursor = Cursors.Hand,
                Font = TG.FontRegular(11f),
                ForeColor = TG.TextHint,
                Width = 40,
                Height = 44,
                Dock = DockStyle.Right,
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _btnTogglePassword.Click += (s, e) => TogglePasswordVisibility();
            _btnTogglePassword.MouseEnter += (s, e) => _btnTogglePassword.ForeColor = TG.TextPrimary;
            _btnTogglePassword.MouseLeave += (s, e) => _btnTogglePassword.ForeColor = TG.TextHint;

            Controls.Add(_tb);
            Controls.Add(_placeholderLabel);
            Controls.Add(_btnTogglePassword);
            _placeholderLabel.BringToFront();

            _tb.GotFocus += (s, e) => { _isFocused = true; Invalidate(); UpdatePlaceholder(); };
            _tb.LostFocus += (s, e) => { _isFocused = false; Invalidate(); UpdatePlaceholder(); };
            _tb.TextChanged += (s, e) => UpdatePlaceholder();

            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void SetPlaceholder(string text)
        {
            _placeholder = text;
            _placeholderLabel.Text = text;
        }

        private void UpdatePlaceholder()
        {
            _placeholderLabel.Visible = string.IsNullOrEmpty(_tb.Text);
        }

        private void TogglePasswordVisibility()
        {
            _isPasswordVisible = !_isPasswordVisible;
            // SỬA DÒNG NÀY: Lấy _defaultPasswordChar ra dùng thay vì _tb.PasswordChar
            _tb.PasswordChar = _isPasswordVisible ? '\0' : _defaultPasswordChar;
            UpdatePasswordToggleButton();
        }

        private void UpdatePasswordToggleButton()
        {
            _btnTogglePassword.Text = _isPasswordVisible ? "👁" : "👁‍🗨";
        }

        // Ghi đè PasswordChar để tự động hiển thị/ẩn nút mắt
        public char PasswordCharValue
        {
            get => _tb.PasswordChar;
            set
            {
                // THÊM DÒNG NÀY: Nếu value khác \0 thì lưu lại làm ký tự gốc
                if (value != '\0') _defaultPasswordChar = value;

                _tb.PasswordChar = value;
                _btnTogglePassword.Visible = (value != '\0');
                _isPasswordVisible = (value == '\0');
                UpdatePasswordToggleButton();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedPanel.GetRoundedPath(rect, TG.RadiusSmall);
            e.Graphics.FillPath(new SolidBrush(TG.InputBg), path);
            Color borderColor = _isFocused ? TG.InputFocused : TG.InputBorder;
            float borderWidth = _isFocused ? 2f : 1f;
            e.Graphics.DrawPath(new Pen(borderColor, borderWidth), path);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_tb == null || _placeholderLabel == null) return;

            int pad = 12;
            int tbHeight = _tb.PreferredHeight;
            int textboxWidth = _btnTogglePassword.Visible ? Width - pad * 2 - 40 : Width - pad * 2;
            _tb.SetBounds(pad, (Height - tbHeight) / 2, textboxWidth, tbHeight);
            _placeholderLabel.SetBounds(0, 0, Width, Height);
        }
    }


    public class TelegramHeader : Panel
    {
        private Label _lblTitle;
        private Label _lblSubtitle;
        private Button _btnBack;
        private AvatarControl _avatar;
        private Panel _rightPanel;

        // Dùng biến này để theo dõi trạng thái, tránh bẫy Control.Visible của WinForms
        private bool _showBack = false;

        public string Title { get => _lblTitle.Text; set => _lblTitle.Text = value; }
        public string Subtitle
        {
            get => _lblSubtitle.Text;
            set { _lblSubtitle.Text = value; _lblSubtitle.Visible = !string.IsNullOrEmpty(value); UpdateLayout(); }
        }

        public bool ShowBack
        {
            get => _showBack;
            set
            {
                _showBack = value;
                _btnBack.Visible = value;
                if (value) _btnBack.BringToFront();
                UpdateLayout();
            }
        }
        public event EventHandler BackClicked { add => _btnBack.Click += value; remove => _btnBack.Click -= value; }

        public TelegramHeader()
        {
            Height = 52;
            BackColor = TG.TitleBarBg;
            Dock = DockStyle.Top;

            _btnBack = new Button
            {
                Text = "←",
                Font = TG.FontRegular(18f),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(52, 52),
                Location = new Point(0, 0),
                Cursor = Cursors.Hand,
                Visible = false,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            _btnBack.FlatAppearance.BorderSize = 0;

            _avatar = new AvatarControl { Size = new Size(36, 36), Location = new Point(52, 8), Visible = false };

            _lblTitle = new Label
            {
                AutoSize = false,
                Font = TG.FontSemiBold(11.5f), // Font vừa phải để không choán chỗ
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
            };

            _lblSubtitle = new Label
            {
                AutoSize = false,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TitleBarSub,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft,
                Visible = false,
            };

            _rightPanel = new Panel { BackColor = Color.Transparent, Width = 100 };

            Controls.AddRange(new Control[] { _btnBack, _avatar, _lblTitle, _lblSubtitle, _rightPanel });
            UpdateLayout();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateLayout();
        }

        // Tách hàm update layout để gọi an toàn mọi lúc
        private void UpdateLayout()
        {
            if (_btnBack == null || _lblTitle == null) return;

            // Tọa độ X: 52 là vừa sát mép nút Back. 
            // Nếu bạn muốn chữ "Danh bạ" xích lại gần mũi tên ← hơn một chút nữa, có thể đổi 52 thành 48 hoặc 44.
            int leftX = _showBack ? 52 : 16;

            if (_avatar.Visible)
            {
                _avatar.Location = new Point(leftX, 8);
                leftX += 44; // Width 36 + Margin 8
            }

            _lblTitle.BringToFront();

            int availableWidth = Math.Max(0, Width - leftX - 100);

            // CHỈNH Y Ở ĐÂY:
            // Thêm "- 4" (hoặc - 6) vào phép tính để kéo chữ nhích lên trên cho ngang hàng với mũi tên.
            // Nếu có subtitle thì đổi từ 6 thành 4 để kéo cụm chữ lên.
            int titleY = string.IsNullOrEmpty(_lblSubtitle.Text) ? ((Height - 24) / 2) - 4 : 4;

            // Tăng Height của Label lên 28 (thay vì 24) để đảm bảo phần chân chữ (như chữ p, y, g) không bị cắt lẹm
            _lblTitle.SetBounds(leftX, titleY, availableWidth, 28);
            _lblSubtitle.SetBounds(leftX, 30, availableWidth, 18);
            _rightPanel.SetBounds(Math.Max(0, Width - 100), 0, 100, Height);
        }

        public void SetAvatar(string name)
        {
            _avatar.SetName(name);
            _avatar.Visible = true;
            UpdateLayout();
        }

        public void AddRightButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Font = TG.FontRegular(14f),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 36),
                Location = new Point(_rightPanel.Width - 40, 8),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            _rightPanel.Controls.Add(btn);
        }
    }

    // ─────────────────────────────────────────────
    // UNREAD BADGE
    // ─────────────────────────────────────────────
    public class UnreadBadge : Control
    {
        private int _count;
        public int Count { get => _count; set { _count = value; Invalidate(); Visible = value > 0; } }
        public bool IsMuted { get; set; } = false;

        public UnreadBadge()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(22, 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            string text = _count > 99 ? "99+" : _count.ToString();
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            int w = Math.Max(22, text.Length * 8 + 8);
            var rect = new Rectangle(Width - w, 0, w, 20);
            Color bg = IsMuted ? TG.BadgeMuted : TG.BadgeBg;
            e.Graphics.FillEllipse(new SolidBrush(bg), rect);
            e.Graphics.DrawString(text, TG.FontSemiBold(7.5f), Brushes.White, rect, sf);
        }
    }
}