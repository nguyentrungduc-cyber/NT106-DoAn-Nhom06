using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SecureChat.Models;
using SecureChat.Client;
using SecureChat.Client.Services;
using SecureChat.Client.Forms.Settings;

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
        private readonly string _callerName;
        private readonly CallType _callType;
        private string? _callerUserId;

        public string? CallerUserId
        {
            get => _callerUserId;
            set
            {
                _callerUserId = value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    frmMainChat.GlobalProfileUpdated -= OnCallerProfileUpdated;
                    frmMainChat.GlobalProfileUpdated += OnCallerProfileUpdated;
                }
            }
        }

        private static readonly Color TgBlue = Color.FromArgb(0x2C, 0xA5, 0xE0);
        private static readonly Color AcceptGreen = Color.FromArgb(0x21, 0xA1, 0x66);
        private static readonly Color DeclineRed = Color.FromArgb(0xE0, 0x24, 0x24);

        public frmIncomingCall(string callerName, CallType callType)
        {
            _callerName = callerName;
            _callType = callType;
            Text = LocalizationService.Translate("Incoming Call");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 260);
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            _pnlAvatar = new Panel
            {
                Size = new Size(80, 80),
                Location = new Point(140, 24),
                Tag = "accent"
            };
            _pnlAvatar.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var br = new SolidBrush(TgBlue);
                e.Graphics.FillEllipse(br, 0, 0, _pnlAvatar.Width - 1, _pnlAvatar.Height - 1);
                using var brush = new SolidBrush(Color.White);
                var currentName = _lblCaller?.Text ?? _callerName;
                var initial = currentName.Length > 0 ? currentName[0].ToString().ToUpperInvariant() : "?";
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
                BackColor = Color.Transparent,
                Tag = "white-fg"
            };
            _lblCaller.Location = new Point((ClientSize.Width - _lblCaller.Width) / 2, 112);
            Controls.Add(_lblCaller);

            string callTypeText = callType == CallType.Video ? "Video call" : "Voice call";
            _lblInfo = new Label
            {
                Text = string.Format(LocalizationService.Translate("{0} incoming..."), callTypeText),
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(0x7A, 0x8A, 0x99),
                AutoSize = true,
                BackColor = Color.Transparent,
                Tag = "white-fg"
            };
            _lblInfo.Location = new Point((ClientSize.Width - _lblInfo.Width) / 2, 138);
            Controls.Add(_lblInfo);

            _btnAccept = new Button
            {
                Text = LocalizationService.Translate("Accept"),
                Size = new Size(120, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = AcceptGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f),
                Cursor = Cursors.Hand,
                Tag = "accent"
            };
            _btnAccept.FlatAppearance.BorderSize = 0;
            _btnAccept.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x1E, 0x93, 0x5D);
            _btnAccept.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x1A, 0x82, 0x52);
            _btnAccept.Location = new Point(40, 190);
            _btnAccept.Click += (_, __) => { if (!_btnAccept.Enabled) return; _btnAccept.Enabled = false; _btnReject.Enabled = false; Accepted = true; Close(); };
            Controls.Add(_btnAccept);

            _btnReject = new Button
            {
                Text = LocalizationService.Translate("Decline"),
                Size = new Size(120, 42),
                FlatStyle = FlatStyle.Flat,
                BackColor = DeclineRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f),
                Cursor = Cursors.Hand,
                Tag = "accent"
            };
            _btnReject.FlatAppearance.BorderSize = 0;
            _btnReject.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xC0, 0x1E, 0x1E);
            _btnReject.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xA0, 0x18, 0x18);
            _btnReject.Location = new Point(200, 190);
            _btnReject.Click += (_, __) => { if (!_btnReject.Enabled) return; _btnAccept.Enabled = false; _btnReject.Enabled = false; Close(); };
            Controls.Add(_btnReject);
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            SecureChat.Client.Services.ThemeRefreshHelper.ApplyTo(this);
            LocalizationService.LanguageChanged += OnLanguageChanged;
            UiLocalization.ApplyToForm(this);
        }

        private void OnLanguageChanged()
        {
            if (IsDisposed) return;
            Text = LocalizationService.Translate("Incoming Call");
            string callTypeText = _callType == CallType.Video ? LocalizationService.Translate("Video call") : LocalizationService.Translate("Voice call");
            _lblInfo.Text = string.Format(LocalizationService.Translate("{0} incoming..."), callTypeText);
            _btnAccept.Text = LocalizationService.Translate("Accept");
            _btnReject.Text = LocalizationService.Translate("Decline");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LocalizationService.LanguageChanged -= OnLanguageChanged;
                if (!string.IsNullOrWhiteSpace(_callerUserId))
                    frmMainChat.GlobalProfileUpdated -= OnCallerProfileUpdated;
            }
            base.Dispose(disposing);
        }

        private void OnCallerProfileUpdated(string userId, string displayName, string username, string avatarUrl)
        {
            if (userId != _callerUserId) return;
            if (IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                string newName = !string.IsNullOrWhiteSpace(displayName) ? displayName : (!string.IsNullOrWhiteSpace(username) ? username : _callerName);
                _lblCaller.Text = newName;
                _pnlAvatar.Invalidate();
            }));
        }
    }
}
