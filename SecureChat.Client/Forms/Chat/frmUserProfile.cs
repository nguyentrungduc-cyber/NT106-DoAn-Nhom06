using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmUserProfile : Form
    {
        private static readonly Color C_BG = Color.White;
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x2D, 0x3D);
        private static readonly Color C_SUB = Color.FromArgb(0x7D, 0x8B, 0x98);
        private static readonly Color C_ACCENT = Color.FromArgb(0x2A, 0xAB, 0xEE);

        public frmUserProfile(string displayName, string username, string userId, string? bio)
        {
            Text = "View Profile";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ClientSize = new Size(400, 380);
            BackColor = C_BG;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            var avatar = new Panel
            {
                Size = new Size(100, 100),
                Location = new Point(150, 24),
                BackColor = TG.GetAvatarColor(displayName),
            };
            avatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = new GraphicsPath();
                path.AddEllipse(0, 0, avatar.Width - 1, avatar.Height - 1);
                avatar.Region = new Region(path);
                if (string.IsNullOrEmpty(displayName)) return;
                string initial = displayName.Length > 0 ? displayName[..1].ToUpper() : "?";
                using var brush = new SolidBrush(Color.White);
                using var fmt = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(initial, new Font("Segoe UI Semibold", 36f), brush,
                    new RectangleF(0, 0, avatar.Width, avatar.Height), fmt);
            };

            var lblName = new Label
            {
                Text = displayName,
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = C_TEXT,
                BackColor = Color.Transparent,
                AutoSize = true,
            };
            lblName.Location = new Point((ClientSize.Width - lblName.Width) / 2, 140);

            var lblUsername = new Label
            {
                Text = $"@{username}",
                Font = new Font("Segoe UI", 11f),
                ForeColor = C_SUB,
                BackColor = Color.Transparent,
                AutoSize = true,
            };
            lblUsername.Location = new Point((ClientSize.Width - lblUsername.Width) / 2, 172);

            int infoY = 220;

            AddInfoField("User ID", userId, ref infoY);
            if (!string.IsNullOrWhiteSpace(bio))
                AddInfoField("Bio", bio, ref infoY);

            var btnClose = new Button
            {
                Text = "Close",
                FlatStyle = FlatStyle.Flat,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Size = new Size(120, 40),
                Location = new Point((ClientSize.Width - 120) / 2, ClientSize.Height - 70),
                Cursor = Cursors.Hand,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => Close();

            Controls.AddRange(new Control[] { avatar, lblName, lblUsername, btnClose });

            void AddInfoField(string label, string value, ref int y)
            {
                var lbl = new Label
                {
                    Text = label,
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = C_SUB,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Location = new Point(40, y),
                };
                var val = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI", 10.5f),
                    ForeColor = C_TEXT,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Location = new Point(40, y + 18),
                    MaximumSize = new Size(320, 0),
                };
                Controls.Add(lbl);
                Controls.Add(val);
                y += 56;
            }
        }
    }
}
