using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmInviteLinksSettings : Form
    {
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private readonly Label _lblLinksCount;
        private int _linksCount;

        public int LinksCount => _linksCount;

        public frmInviteLinksSettings(int currentCount)
        {
            ThemeRefreshHelper.Hook(this);
            _linksCount = Math.Max(1, currentCount);

            Text = "Invite links";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(500, 440);
            Opacity = 0;

            _fadeTimer = new System.Windows.Forms.Timer { Interval = 14 };
            _fadeTimer.Tick += (_, __) =>
            {
                if (Opacity >= 1) { _fadeTimer.Stop(); return; }
                Opacity = Math.Min(1, Opacity + 0.12);
            };
            Shown += (_, __) => _fadeTimer.Start();

            var lblTitle = new Label
            {
                Text = "Invite links",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(260, 34)
            };

            var lblPrimary = new Label
            {
                Text = "Primary link",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = TG.Blue,
                Location = new Point(24, 72),
                Size = new Size(150, 30)
            };

            var pnlLink = new Panel
            {
                Location = new Point(24, 106),
                Size = new Size(452, 52),
                BackColor = TG.SidebarHover
            };

            var txtLink = new TextBox
            {
                Text = "t.me/+S3QfQvxTOhk5ZTY9",
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                BackColor = TG.SidebarHover,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 13f),
                Location = new Point(16, 15),
                Size = new Size(390, 28)
            };

            var btnMore = new Button
            {
                Text = "?",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Font = new Font("Segoe UI", 12f),
                Size = new Size(30, 30),
                Location = new Point(418, 10)
            };
            btnMore.FlatAppearance.BorderSize = 0;
            pnlLink.Controls.AddRange(new Control[] { txtLink, btnMore });

            var btnCopy = BuildBlueButton("\U0001F4CB  Copy Link", new Point(24, 174));
            btnCopy.Click += (_, __) =>
            {
                try { Clipboard.SetText(txtLink.Text); } catch { }
                MessageBox.Show(this, "Link copied.", "Invite links", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var btnShare = BuildBlueButton("\u27A1\uFE0F  Share Link", new Point(254, 174));
            btnShare.Click += (_, __) =>
                MessageBox.Show(this, "Share link flow will be connected next.", "Invite links", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var sep = new Panel { Location = new Point(0, 238), Size = new Size(500, 1), BackColor = TG.Divider };

            var rowNewLink = new Panel
            {
                Location = new Point(0, 240),
                Size = new Size(500, 58),
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand
            };
            rowNewLink.MouseEnter += (_, __) => rowNewLink.BackColor = Color.FromArgb(0xF7, 0xFA, 0xFD);
            rowNewLink.MouseLeave += (_, __) => rowNewLink.BackColor = TG.WindowBg;

            var lblCreate = new Label
            {
                Text = "\u2795  Create a New Link",
                Font = new Font("Segoe UI Emoji", 13f),
                ForeColor = TG.Blue,
                Location = new Point(24, 12),
                Size = new Size(260, 34),
                BackColor = Color.Transparent
            };
            rowNewLink.Controls.Add(lblCreate);

            rowNewLink.Click += (_, __) => CreateNewLink();
            lblCreate.Click += (_, __) => CreateNewLink();

            var pnlHint = new Panel
            {
                Location = new Point(0, 300),
                Size = new Size(500, 86),
                BackColor = Color.FromArgb(0xF4, 0xF6, 0xF8)
            };
            var lblHint = new Label
            {
                Text = "You can generate invite links that expire after they are used.",
                Font = new Font("Segoe UI", 10.5f),
                ForeColor = TG.TextSecondary,
                Location = new Point(24, 18),
                Size = new Size(450, 50)
            };
            pnlHint.Controls.Add(lblHint);

            _lblLinksCount = new Label
            {
                Text = $"Total links: {_linksCount}",
                Font = new Font("Segoe UI", 9.8f),
                ForeColor = TG.TextSecondary,
                Location = new Point(24, 392),
                Size = new Size(170, 24)
            };

            var btnDone = BuildBottomButton("Done", TG.Blue, true);
            btnDone.Location = new Point(394, 396);
            btnDone.Click += (_, __) => DialogResult = DialogResult.OK;

            Controls.AddRange(new Control[]
            {
                lblTitle, lblPrimary, pnlLink, btnCopy, btnShare,
                sep, rowNewLink, pnlHint, _lblLinksCount, btnDone
            });
        }

        private void CreateNewLink()
        {
            _linksCount++;
            _lblLinksCount.Text = $"Total links: {_linksCount}";
            MessageBox.Show(this, "A new invite link has been created.", "Invite links", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static Button BuildBlueButton(string text, Point location)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = TG.Blue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Emoji", 11f, FontStyle.Bold),
                Size = new Size(220, 42),
                Location = location,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private static Button BuildBottomButton(string text, Color color, bool bold = false)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", 11f, bold ? FontStyle.Bold : FontStyle.Regular),
                Size = new Size(90, 34),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fadeTimer.Stop();
            _fadeTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
