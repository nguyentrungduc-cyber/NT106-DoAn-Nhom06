using System;
using System.Drawing;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmClearHistory : Form
    {
        private readonly CheckBox _chkDeleteForEveryone;

        public bool DeleteConfirmed { get; private set; }
        public bool DeleteForEveryone => _chkDeleteForEveryone.Checked;

        public frmClearHistory(string chatName)
        {
            Text = "Clear history";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(400, 290);

            var lblQuestion = new Label
            {
                Text = $"Are you sure you want to delete all\r\nmessages in \"{chatName}\"?",
                Font = new Font("Segoe UI", 16f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 26),
                Size = new Size(340, 76)
            };

            var lblWarning = new Label
            {
                Text = "This action cannot be undone.",
                Font = new Font("Segoe UI", 13f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 112),
                Size = new Size(300, 34)
            };

            _chkDeleteForEveryone = new CheckBox
            {
                Text = "Delete for everyone",
                Font = new Font("Segoe UI", 13f),
                ForeColor = TG.TextPrimary,
                Location = new Point(28, 168),
                Size = new Size(260, 32),
                AutoSize = false
            };

            var btnCancel = BuildActionButton("Cancel", TG.Blue);
            btnCancel.Location = new Point(222, 238);
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            var btnDelete = BuildActionButton("Delete", Color.FromArgb(0xE2, 0x4B, 0x4A));
            btnDelete.Location = new Point(306, 238);
            btnDelete.Click += (_, __) =>
            {
                DeleteConfirmed = true;
                DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[]
            {
                lblQuestion,
                lblWarning,
                _chkDeleteForEveryone,
                btnCancel,
                btnDelete
            });
        }

        private static Button BuildActionButton(string text, Color color)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", 12f, FontStyle.Regular),
                Size = new Size(78, 36),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
