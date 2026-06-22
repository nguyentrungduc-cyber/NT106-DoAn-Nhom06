using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmUserProfile : Form
    {
        public frmUserProfile(string displayName, string username, string? email, string? bio,
            bool isOnline = false, DateTime? lastSeenUtc = null, bool showOnlineStatus = true)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            Text = "Profile";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            ClientSize = new Size(400, 400);
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
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
                ForeColor = TG.TextPrimary,
                Cursor = Cursors.Hand,
                TabStop = false,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = TG.SidebarHover;
            btnClose.FlatAppearance.MouseDownBackColor = TG.SidebarHover;
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

            // Display Name (centered)
            var lblName = new Label
            {
                Text = displayName,
                Font = TG.FontSemiBold(18f),
                ForeColor = TG.TextPrimary,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                BackColor = Color.Transparent,
            };
            if (lblName.Height < 28) lblName.Height = 28;
            lblName.Location = new Point((ClientSize.Width - lblName.Width) / 2, y);
            Controls.Add(lblName);
            y += lblName.Height + 4;

            // Username (centered)
            var lblUsername = new Label
            {
                Text = $"@{username}",
                Font = TG.FontRegular(13f),
                ForeColor = TG.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                BackColor = Color.Transparent,
            };
            if (lblUsername.Height < 20) lblUsername.Height = 20;
            lblUsername.Location = new Point((ClientSize.Width - lblUsername.Width) / 2, y);
            Controls.Add(lblUsername);
            y += lblUsername.Height + 6;

            // Presence status (centered)
            string presenceText;
            if (showOnlineStatus)
                presenceText = Helpers.PresenceFormatter.GetPresenceText(isOnline, lastSeenUtc);
            else
                presenceText = "offline";

            var lblStatus = new Label
            {
                Text = presenceText,
                Font = TG.FontRegular(11f),
                ForeColor = presenceText == "Online" ? Color.FromArgb(0x21, 0xA1, 0x66) : TG.TextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                BackColor = Color.Transparent,
            };
            lblStatus.Location = new Point((ClientSize.Width - lblStatus.Width) / 2, y);
            Controls.Add(lblStatus);
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
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            Invalidate(true);
            ApplyThemeToControls(Controls);
        }

        private static void ApplyThemeToControls(System.Windows.Forms.Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.BackColor != Color.Transparent &&
                    c.BackColor != TG.Blue &&
                    c.BackColor != TG.SidebarActive &&
                    c.BackColor != TG.TitleBarBg &&
                    c.Tag as string != "accent")
                    c.BackColor = TG.WindowBg;
                if (c.ForeColor != Color.White && c.Tag as string != "white-fg")
                    c.ForeColor = TG.TextPrimary;
                c.Invalidate();
                ApplyThemeToControls(c.Controls);
            }
        }

    }
}
