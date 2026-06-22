using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using SecureChat.Client.Helpers;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Shared
{
    /// <summary>
    /// Modal dialog Telegram-style để hiển thị thông báo lỗi / thông tin /
    /// thành công thay cho <see cref="MessageBox"/> mặc định.
    ///
    /// Cách dùng nhanh:
    /// <code>
    /// frmError.ShowError(this, "Sai mật khẩu", "Vui lòng kiểm tra lại.");
    /// frmError.ShowApi(this, errorMessageFromServer);   // tự parse JSON
    /// frmError.ShowSuccess(this, "Đăng ký thành công");
    /// </code>
    /// </summary>
    public sealed class frmError : Form
    {
        public enum DialogKind
        {
            Error,
            Info,
            Success,
            Warning
        }

        private readonly DialogKind _kind;
        private readonly string _title;
        private readonly string _message;

        private frmError(DialogKind kind, string title, string message)
        {
            _kind = kind;
            _title = string.IsNullOrWhiteSpace(title) ? "Thông báo" : title;
            _message = message ?? string.Empty;
            BuildUi();
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
        }

        // ── Static helpers ─────────────────────────────────────────────

        public static DialogResult ShowError(IWin32Window? owner, string title, string message)
            => Show(owner, DialogKind.Error, title, message);

        public static DialogResult ShowInfo(IWin32Window? owner, string title, string message)
            => Show(owner, DialogKind.Info, title, message);

        public static DialogResult ShowSuccess(IWin32Window? owner, string title, string message)
            => Show(owner, DialogKind.Success, title, message);

        public static DialogResult ShowWarning(IWin32Window? owner, string title, string message)
            => Show(owner, DialogKind.Warning, title, message);

        /// <summary>
        /// Parse chuỗi error gốc (kể cả JSON từ server) rồi show dialog đỏ.
        /// </summary>
        public static DialogResult ShowApi(IWin32Window? owner, string? rawError, string fallback = "Đã xảy ra lỗi không xác định.")
        {
            var (title, message) = ApiErrorParser.Parse(rawError, fallback);
            return ShowError(owner, title, message);
        }

        private static DialogResult Show(IWin32Window? owner, DialogKind kind, string title, string message)
        {
            using var dlg = new frmError(kind, title, message);
            return owner is null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
        }

        // ── UI ─────────────────────────────────────────────────────────

        private void BuildUi()
        {
            Text = _title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = TG.WindowBg;
            Font = TG.FontRegular(9.5f);
            DoubleBuffered = true;
            Size = new Size(420, 240);
            Padding = new Padding(0);
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            var palette = GetPalette(_kind);

            // Vẽ viền bo tròn cho cả form
            Region = Region.FromHrgn(NativeRoundRect(0, 0, Width, Height, 14, 14));
            Resize += (s, e) => Region = Region.FromHrgn(NativeRoundRect(0, 0, Width, Height, 14, 14));
            Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var border = new Pen(Color.FromArgb(0xE5, 0xE9, 0xEF), 1f);
                var r = new Rectangle(0, 0, Width - 1, Height - 1);
                using var path = RoundedPanel.GetRoundedPath(r, 14);
                e.Graphics.DrawPath(border, path);
            };

            // Banner màu (Header)
            var header = new Panel
            {
                Height = 92,
                Dock = DockStyle.Top,
                BackColor = palette.HeaderBg,
            };

            var lblIcon = new Label
            {
                Text = palette.IconGlyph,
                Font = new Font("Segoe UI Emoji", 30f),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(56, 56),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };
            var lblTitle = new Label
            {
                Text = _title,
                Font = TG.FontSemiBold(13f),
                ForeColor = Color.White,
                AutoSize = false,
                Height = 28,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            header.Controls.Add(lblIcon);
            header.Controls.Add(lblTitle);
            header.Resize += (s, e) =>
            {
                lblIcon.Location = new Point(20, (header.Height - lblIcon.Height) / 2);
                lblTitle.SetBounds(lblIcon.Right + 12, (header.Height - 28) / 2, header.Width - lblIcon.Right - 24, 28);
            };

            // Nội dung
            var lblMessage = new Label
            {
                Text = _message,
                Font = TG.FontRegular(10f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                BackColor = TG.WindowBg,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(24, 12, 24, 12),
                Dock = DockStyle.Fill,
            };

            // Footer + nút OK
            var footer = new Panel
            {
                Height = 64,
                Dock = DockStyle.Bottom,
                BackColor = TG.WindowBg,
                Padding = new Padding(20, 8, 20, 16),
            };
            var btnOk = new TelegramButton
            {
                Text = "ĐÃ HIỂU",
                Height = 40,
                Font = TG.FontSemiBold(10.5f),
                Radius = TG.RadiusSmall,
                NormalColor = palette.AccentColor,
            };
            btnOk.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            footer.Controls.Add(btnOk);
            footer.Resize += (s, e) =>
            {
                btnOk.SetBounds(footer.Padding.Left, footer.Padding.Top,
                                footer.Width - footer.Padding.Horizontal,
                                footer.Height - footer.Padding.Vertical);
            };

            Controls.Add(lblMessage);
            Controls.Add(footer);
            Controls.Add(header);

            // Auto-fit chiều cao theo nội dung (giới hạn 380)
            using (var g = CreateGraphics())
            {
                var size = g.MeasureString(_message, lblMessage.Font, ClientSize.Width - 60);
                int desiredBody = (int)Math.Ceiling(size.Height) + 40;
                int totalHeight = header.Height + Math.Max(80, desiredBody) + footer.Height;
                Height = Math.Min(420, Math.Max(220, totalHeight));
            }

            AcceptButton = btnOk;
            CancelButton = btnOk;
            btnOk.Focus();
        }

        private static (Color HeaderBg, Color AccentColor, string IconGlyph) GetPalette(DialogKind kind)
            => kind switch
            {
                DialogKind.Error => (Color.FromArgb(0xE2, 0x4B, 0x4A), Color.FromArgb(0xE2, 0x4B, 0x4A), "⚠"),
                DialogKind.Warning => (Color.FromArgb(0xF1, 0xA8, 0x2C), Color.FromArgb(0xF1, 0xA8, 0x2C), "⚠"),
                DialogKind.Success => (Color.FromArgb(0x2E, 0x7D, 0x32), Color.FromArgb(0x2E, 0x7D, 0x32), "✓"),
                _ => (TG.Blue, TG.Blue, "ℹ"),
            };

        // P/Invoke để bo tròn cho FormBorderStyle.None
        [System.Runtime.InteropServices.DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr NativeRoundRect(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            Invalidate(true);
        }

    }
}
