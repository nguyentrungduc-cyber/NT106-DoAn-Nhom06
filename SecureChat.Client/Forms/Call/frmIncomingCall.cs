using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SecureChat.Client.Forms.Call
{
    public sealed class frmIncomingCall : Form
    {
        public bool Accepted { get; private set; }

        private readonly Label _lblCaller;
        private readonly Label _lblInfo;
        private readonly Button _btnAccept;
        private readonly Button _btnReject;
        private readonly Panel _pnlAvatar;

        private static readonly Color TgBlue = Color.FromArgb(0x2C, 0xA5, 0xE0);
        private static readonly Color TgBg = Color.White;

        public frmIncomingCall(string callerName, int callType)
        {
            Text = "Incoming Call";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 260);
            BackColor = TgBg;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            _pnlAvatar = new Panel
            {
                Size = new Size(80, 80),
                Location = new Point(140, 24),
                BackColor = TgBlue
            };
            _pnlAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(TgBlue);
                e.Graphics.FillEllipse(br, 0, 0, _pnlAvatar.Width - 1, _pnlAvatar.Height - 1);
                using var brush = new SolidBrush(Color.White);
                var initial = callerName.Length > 0 ? callerName[0].ToString().ToUpperInvariant() : "?";
                using var font = new Font("Segoe UI", 28f, FontStyle.Bold);
                var size = e.Graphics.MeasureString(initial, font);
                e.Graphics.DrawString(initial, font, brush,
                    (_pnlAvatar.Width - size.Width) / 2,
                    (_pnlAvatar.Height - size.Height) / 2);
            };
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, _pnlAvatar.Width, _pnlAvatar.Height);
            _pnlAvatar.Region = new Region(path);
            Controls.Add(_pnlAvatar);

            _lblCaller = new Label
            {
                Text = callerName,
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _lblCaller.Location = new Point((ClientSize.Width - _lblCaller.Width) / 2, 112);
            Controls.Add(_lblCaller);

            string callTypeText = callType == 1 ? "Video call" : "Voice call";
            _lblInfo = new Label
            {
                Text = $"{callTypeText} incoming...",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(0x7A, 0x8A, 0x99),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _lblInfo.Location = new Point((ClientSize.Width - _lblInfo.Width) / 2, 138);
            Controls.Add(_lblInfo);

            _btnAccept = new Button
            {
                Text = "Accept",
                Size = new Size(120, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x21, 0xA1, 0x66),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f),
                Cursor = Cursors.Hand
            };
            _btnAccept.FlatAppearance.BorderSize = 0;
            _btnAccept.Location = new Point(40, 190);
            _btnAccept.Click += (_, __) => { Accepted = true; Close(); };
            Controls.Add(_btnAccept);

            _btnReject = new Button
            {
                Text = "Decline",
                Size = new Size(120, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE0, 0x24, 0x24),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f),
                Cursor = Cursors.Hand
            };
            _btnReject.FlatAppearance.BorderSize = 0;
            _btnReject.Location = new Point(200, 190);
            _btnReject.Click += (_, __) => Close();
            Controls.Add(_btnReject);
        }
    }
}
