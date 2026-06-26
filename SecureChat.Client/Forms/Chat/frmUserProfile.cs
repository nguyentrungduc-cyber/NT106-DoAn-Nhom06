using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client;
using SecureChat.Client.Services;
using SecureChat.Client.Forms.Settings;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmUserProfile : Form
    {
        public frmUserProfile(string displayName, string username, string? email, string? bio,
            bool isOnline = false, DateTime? lastSeenUtc = null, bool showOnlineStatus = true)
        {
            Text = "Profile";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            ClientSize = new Size(440, 400);
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            int y = 28;

            // Close button (✕) top-right
            var btnClose = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI", 12f),
                Size = new Size(30, 30),
                Location = new Point(ClientSize.Width - 46, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(0x2D, 0x3B, 0x4E),
                Cursor = Cursors.Hand,
                TabStop = false,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF0, 0xF4, 0xF8);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE8, 0xEE, 0xF5);
            btnClose.Click += (_, __) => Close();
            Controls.Add(btnClose);

            // Avatar (centered)
            var avatar = new AvatarControl
            {
                Size = new Size(100, 100),
                Location = new Point((ClientSize.Width - 100) / 2, y)
            };
            avatar.SetName(displayName);
            Controls.Add(avatar);
            y += 114;

            // Helper: label full-width (trừ margin 2 bên), dùng TextAlign để căn giữa thật -
            // không tự đo Width rồi tự chia đôi nữa nên không thể bị lệch/tràn mép như trước.
            Label AddCenteredLabel(string text, Font font, Color color, int topY)
            {
                int w = ClientSize.Width - 40;
                var measured = TextRenderer.MeasureText(text, font, new Size(w, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
                var lbl = new Label
                {
                    Text = text,
                    Font = font,
                    ForeColor = color,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    Location = new Point(20, topY),
                    Size = new Size(w, measured.Height + 4),
                };
                Controls.Add(lbl);
                return lbl;
            }

            // Display Name (centered, tự xuống dòng nếu tên dài)
            var lblName = AddCenteredLabel(displayName, TG.FontSemiBold(18f), TG.TextPrimary, y);
            y += lblName.Height + 4;

            // Username (centered)
            var lblUsername = AddCenteredLabel($"@{username}", TG.FontRegular(13f), TG.TextSecondary, y);
            y += lblUsername.Height + 6;

            // Presence status (centered)
            string presenceText;
            if (showOnlineStatus)
                presenceText = Helpers.PresenceFormatter.GetPresenceText(isOnline, lastSeenUtc);
            else
                presenceText = "offline";

            var lblStatus = AddCenteredLabel(presenceText, TG.FontRegular(11f),
                presenceText == "Online" ? Color.FromArgb(0x21, 0xA1, 0x66) : TG.TextSecondary, y);
            y += lblStatus.Height + 18;

            // Divider
            Controls.Add(new Panel
            {
                Height = 1,
                Width = ClientSize.Width - 80,
                BackColor = TG.Divider,
                Location = new Point(40, y)
            });
            y += 22;

            // Email
            AppendInfoField("Email", string.IsNullOrWhiteSpace(email) ? "No email available" : email, ref y);

            // Bio (optional)
            if (!string.IsNullOrWhiteSpace(bio))
            {
                y += 4;
                AppendInfoField("Bio", bio, ref y);
            }
            UiLocalization.ApplyToForm(this);

            // Co lại đúng theo nội dung thật (tránh bị clip khi tên/email/bio dài), nhưng vẫn giữ tối thiểu cho thoáng
            ClientSize = new Size(ClientSize.Width, Math.Max(380, y + 12));
        }

        private void AppendInfoField(string label, string value, ref int y)
        {
            int left = 40;

            var lblLabel = new Label
            {
                Text = label,
                Font = TG.FontRegular(9.5f),
                ForeColor = TG.TextHint,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(left, y),
            };
            Controls.Add(lblLabel);
            y += lblLabel.Height + 2;

            var lblValue = new Label
            {
                Text = value,
                Font = TG.FontRegular(11f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                MaximumSize = new Size(ClientSize.Width - 80, 0),
                BackColor = Color.Transparent,
                Location = new Point(left, y),
            };
            Controls.Add(lblValue);
            y += lblValue.Height + 20;
        }
    }
}
