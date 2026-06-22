using SecureChat.Client.Services;
namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmChatHistorySettings : Form
    {
        private readonly RadioButton _rbVisible;
        private readonly RadioButton _rbHidden;

        public string ChatHistoryMode { get; private set; }

        public frmChatHistorySettings(string current)
        {
            ThemeRefreshHelper.Hook(this);
            ChatHistoryMode = string.IsNullOrWhiteSpace(current) ? "Hidden" : current;

            Text = "Chat history for new members";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(500, 340);

            var lblTitle = new Label
            {
                Text = "Chat history for new members",
                Font = new Font("Segoe UI Semibold", 17f),
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(430, 34)
            };

            _rbVisible = new RadioButton
            {
                Text = "Visible",
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Location = new Point(26, 82)
            };
            var lblVisibleDesc = new Label
            {
                Text = "New members will see messages\r\nthat were sent before they joined.",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Size = new Size(430, 52),
                Location = new Point(58, 110)
            };

            _rbHidden = new RadioButton
            {
                Text = "Hidden",
                Font = new Font("Segoe UI", 11f),
                AutoSize = true,
                Location = new Point(26, 176)
            };
            var lblHiddenDesc = new Label
            {
                Text = "New members won't see more\r\nthan 100 previous messages.",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Size = new Size(430, 52),
                Location = new Point(58, 204)
            };

            var btnCancel = BuildBottomButton("Cancel", TG.Blue);
            btnCancel.Location = new Point(300, 292);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnSave = BuildBottomButton("Save", TG.Blue, true);
            btnSave.Location = new Point(392, 292);
            btnSave.Click += (_, __) =>
            {
                ChatHistoryMode = _rbVisible.Checked ? "Visible" : "Hidden";
                DialogResult = DialogResult.OK;
            };

            if (string.Equals(ChatHistoryMode, "Visible", StringComparison.OrdinalIgnoreCase)) _rbVisible.Checked = true;
            else _rbHidden.Checked = true;

            Controls.AddRange(new Control[]
            {
                lblTitle, _rbVisible, lblVisibleDesc, _rbHidden, lblHiddenDesc, btnCancel, btnSave
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
