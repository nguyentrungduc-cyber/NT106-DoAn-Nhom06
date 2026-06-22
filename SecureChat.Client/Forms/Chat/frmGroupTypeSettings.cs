namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmGroupTypeSettings : Form
    {
        private readonly RadioButton _rbPublic;
        private readonly RadioButton _rbPrivate;

        public string GroupType { get; private set; }

        public frmGroupTypeSettings(string currentType)
        {
            GroupType = string.IsNullOrWhiteSpace(currentType) ? "Private" : currentType;

            Text = "Group type";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(500, 340);

            var lblTitle = new Label
            {
                Text = "Group type",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
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
                ForeColor = Color.FromArgb(0x7D, 0x8B, 0x98),
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
                Text = "People can only join if they are added by an admin",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(0x7D, 0x8B, 0x98),
                AutoSize = false,
                Size = new Size(430, 54),
                Location = new Point(58, 194)
            };

            var btnCancel = BuildBottomButton("Cancel", Color.FromArgb(0x2A, 0xAB, 0xEE));
            btnCancel.Location = new Point(300, 300);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnSave = BuildBottomButton("Save", Color.FromArgb(0x2A, 0xAB, 0xEE), true);
            btnSave.Location = new Point(392, 300);
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
                btnCancel, btnSave
            });
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
    }
}
