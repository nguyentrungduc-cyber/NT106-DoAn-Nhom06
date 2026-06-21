using SecureChat.Client.Services;
namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmGroupTypeSettings : Form
    {
        private readonly RadioButton _rbPublic;
        private readonly RadioButton _rbPrivate;

        public string GroupType { get; private set; }

        public frmGroupTypeSettings(string currentType)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            GroupType = string.IsNullOrWhiteSpace(currentType) ? "Private" : currentType;

            Text = "Group type";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(500, 470);

            var lblTitle = new Label
            {
                Text = "Group type",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 14),
                Size = new Size(250, 34)
            };

            _rbPublic = new RadioButton
            {
                Text = "Public Group",
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Location = new Point(26, 76)
            };
            var lblPublicDesc = new Label
            {
                Text = "Anyone can find the group in search and\r\njoin, chat history is available to everybody",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Size = new Size(430, 54),
                Location = new Point(58, 104)
            };

            _rbPrivate = new RadioButton
            {
                Text = "Private Group",
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Location = new Point(26, 166)
            };
            var lblPrivateDesc = new Label
            {
                Text = "People can only join if they are added or\r\nhave an invite link",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Size = new Size(430, 54),
                Location = new Point(58, 194)
            };

            var sep = new Panel
            {
                Location = new Point(0, 258),
                Size = new Size(500, 1),
                BackColor = TG.Divider
            };

            var lblPrimaryLink = new Label
            {
                Text = "Primary link",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = TG.Blue,
                Location = new Point(24, 272),
                Size = new Size(150, 30)
            };

            var txtLink = new TextBox
            {
                Text = "t.me/+S3QfQvxTOhk5ZTY9",
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = TG.SidebarHover,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 11f),
                Location = new Point(38, 322),
                Size = new Size(402, 28)
            };

            var pnlLink = new Panel
            {
                Location = new Point(24, 310),
                Size = new Size(452, 48),
                BackColor = TG.SidebarHover
            };
            pnlLink.Controls.Add(txtLink);

            var btnCopy = BuildBlueButton("\U0001F4CB  Copy Link", new Point(24, 374));
            btnCopy.Click += (_, __) =>
            {
                try { Clipboard.SetText(txtLink.Text); } catch { }
                MessageBox.Show(this, "Link copied.", "Group type", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var btnShare = BuildBlueButton("\u27A1\uFE0F  Share Link", new Point(256, 374));
            btnShare.Click += (_, __) =>
                MessageBox.Show(this, "Share link action will be connected next.", "Group type", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var btnCancel = BuildBottomButton("Cancel", TG.Blue);
            btnCancel.Location = new Point(300, 430);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnSave = BuildBottomButton("Save", TG.Blue, true);
            btnSave.Location = new Point(392, 430);
            btnSave.Click += (_, __) =>
            {
                GroupType = _rbPublic.Checked ? "Public" : "Private";
                DialogResult = DialogResult.OK;
            };

            if (string.Equals(GroupType, "Public", StringComparison.OrdinalIgnoreCase)) _rbPublic.Checked = true;
            else _rbPrivate.Checked = true;

            Controls.AddRange(new Control[]
            {
                lblTitle, _rbPublic, lblPublicDesc, _rbPrivate, lblPrivateDesc,
                sep, lblPrimaryLink, pnlLink, btnCopy, btnShare, btnCancel, btnSave
            });
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
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            Invalidate(true);
            ThemeRefreshHelper.ApplyTo(this);
        }

    }
}
