using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmUserProfile : Form
    {
        private static readonly Color C_BG = Color.White;
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x2D, 0x3D);

        public frmUserProfile(string displayName, string username, string userId, string? bio)
        {
            Text = "Profile";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            ClientSize = new Size(400, 420);
            BackColor = C_BG;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            int y = 24;

            // Close button (✕) — giống frmGroupInfo
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

            // Large avatar (centered)
            var avatar = new AvatarControl
            {
                Size = new Size(100, 100),
                Location = new Point((ClientSize.Width - 100) / 2, y)
            };
            avatar.SetName(displayName);
            Controls.Add(avatar);
            y += 118;

            // Display name (centered)
            var lblName = new Label
            {
                Text = displayName,
                Font = TG.FontSemiBold(16f),
                ForeColor = TG.TextPrimary,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 0),
                AutoSize = true,
                MaximumSize = new Size(ClientSize.Width - 40, 0),
                BackColor = Color.Transparent,
            };
            if (lblName.Height < 24) lblName.Height = 24;
            lblName.Location = new Point(0, y);
            Controls.Add(lblName);
            y += lblName.Height + 4;

            // Username (centered)
            var lblUsername = new Label
            {
                Text = $"@{username}",
                Font = TG.FontRegular(12f),
                ForeColor = TG.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 0),
                AutoSize = true,
                MaximumSize = new Size(ClientSize.Width - 40, 0),
                BackColor = Color.Transparent,
            };
            if (lblUsername.Height < 20) lblUsername.Height = 20;
            lblUsername.Location = new Point(0, y);
            Controls.Add(lblUsername);
            y += lblUsername.Height + 20;

            // Divider
            Controls.Add(new Panel
            {
                Height = 1,
                Width = ClientSize.Width - 80,
                BackColor = TG.Divider,
                Location = new Point(40, y)
            });
            y += 16;

            // Info: User ID
            AppendInfoRow("🆔", userId, ref y);

            // Info: Bio
            if (!string.IsNullOrWhiteSpace(bio))
                AppendInfoRow("ℹ️", bio, ref y);
        }

        private void AppendInfoRow(string icon, string text, ref int y)
        {
            var lbl = new Label
            {
                Text = $"{icon}  {text}",
                Font = TG.FontRegular(11f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                MaximumSize = new Size(ClientSize.Width - 80, 0),
                BackColor = Color.Transparent,
                Location = new Point(40, y)
            };
            Controls.Add(lbl);
            y += lbl.Height + 12;
        }
    }
}
