using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client;
using SecureChat.Client.Services;
using SecureChat.Client.Forms.Settings;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmUserProfile : Form
    {
        private readonly AvatarControl _avatar;
        private readonly Label _lblName;
        private readonly Label _lblUsername;
        private readonly Label _lblStatus;
        private Label? _lblEmailValue;
        private Label? _lblBioValue;
        private readonly string _userId;
        private readonly string _origDisplayName;
        private readonly string _origUsername;
        private readonly string? _origEmail;
        private readonly string? _origBio;
        private readonly bool _showOnlineStatus;
        private bool _isClosing;
        private long _avatarLoadVersion;

        public frmUserProfile(string displayName, string username, string? email, string? bio,
            bool isOnline = false, DateTime? lastSeenUtc = null, bool showOnlineStatus = true,
            string userId = "")
        {
            _userId = userId;
            _origDisplayName = displayName;
            _origUsername = username;
            _origEmail = email;
            _origBio = bio;
            _showOnlineStatus = showOnlineStatus;

            Text = "Profile";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            ClientSize = new Size(440, 400);
            BackColor = Color.White;
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
                ForeColor = Color.FromArgb(0x2D, 0x3B, 0x4E),
                Cursor = Cursors.Hand,
                TabStop = false,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF0, 0xF4, 0xF8);
            btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE8, 0xEE, 0xF5);
            btnClose.Click += (_, __) => Close();
            Controls.Add(btnClose);

            // Avatar (centered)
            _avatar = new AvatarControl
            {
                Size = new Size(100, 100),
                Location = new Point((ClientSize.Width - 100) / 2, y)
            };
            _avatar.SetName(displayName);
            Controls.Add(_avatar);
            y += 114;

            // Helper: label full-width (trừ margin 2 bên), dùng TextAlign để căn giữa thật -
            // không tự đo Width rồi tự chia đôi nữa nên không thể bị lệch/tràn mép như trước.
            Label AddCenteredLabel(string text, Font font, Color color, int topY)
            {
                int w = ClientSize.Width - 40;
                var measured = TextRenderer.MeasureText(text, font, new Size(w, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
                var lbl = new Label
                {
                    Text = text,
                    Font = font,
                    ForeColor = color,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false,
                    BackColor = Color.Transparent,
                    Location = new Point(20, topY),
                    Size = new Size(w, measured.Height + 4),
                };
                Controls.Add(lbl);
                return lbl;
            }

            // Display Name (centered, tự xuống dòng nếu tên dài)
            _lblName = AddCenteredLabel(displayName, TG.FontSemiBold(18f), TG.TextPrimary, y);
            y += _lblName.Height + 4;

            // Username (centered)
            _lblUsername = AddCenteredLabel($"@{username}", TG.FontRegular(13f), TG.TextSecondary, y);
            y += _lblUsername.Height + 6;

            // Presence status (centered)
            string presenceText;
            if (showOnlineStatus)
                presenceText = Helpers.PresenceFormatter.GetPresenceText(isOnline, lastSeenUtc);
            else
                presenceText = "offline";

            _lblStatus = AddCenteredLabel(presenceText, TG.FontRegular(11f),
                presenceText == "Online" ? Color.FromArgb(0x21, 0xA1, 0x66) : TG.TextSecondary, y);
            y += _lblStatus.Height + 18;

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
            _lblEmailValue = AppendInfoField("Email", string.IsNullOrWhiteSpace(email) ? "No email available" : email, ref y);

            // Bio (optional)
            if (!string.IsNullOrWhiteSpace(bio))
            {
                y += 4;
                _lblBioValue = AppendInfoField("Bio", bio, ref y);
            }
            UiLocalization.ApplyToForm(this);

            // Co lại đúng theo nội dung thật (tránh bị clip khi tên/email/bio dài), nhưng vẫn giữ tối thiểu cho thoáng
            ClientSize = new Size(ClientSize.Width, Math.Max(380, y + 12));

            if (!string.IsNullOrWhiteSpace(_userId))
            {
                frmMainChat.GlobalProfileUpdated -= OnGlobalProfileUpdated;
                frmMainChat.GlobalProfileUpdated += OnGlobalProfileUpdated;
            }
        }

        private Label AppendInfoField(string label, string value, ref int y)
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
            return lblValue;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;
            if (!string.IsNullOrWhiteSpace(_userId))
                frmMainChat.GlobalProfileUpdated -= OnGlobalProfileUpdated;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !string.IsNullOrWhiteSpace(_userId))
                frmMainChat.GlobalProfileUpdated -= OnGlobalProfileUpdated;
            base.Dispose(disposing);
        }

        private void OnGlobalProfileUpdated(string userId, string displayName, string username, string avatarUrl)
        {
            if (_isClosing || IsDisposed || userId != _userId) return;

            long thisVersion = Interlocked.Increment(ref _avatarLoadVersion);

            // Load avatar off UI thread
            bool avatarRemoved = avatarUrl == string.Empty;
            Task<Image?>? loadTask = null;
            if (!string.IsNullOrWhiteSpace(avatarUrl))
                loadTask = Task.Run(() => AvatarCacheService.LoadImage(avatarUrl));

            BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (_isClosing || IsDisposed) return;

                    string newName = !string.IsNullOrWhiteSpace(displayName) ? displayName : _origDisplayName;
                    string newUsername = !string.IsNullOrWhiteSpace(username) ? username : _origUsername;

                    _lblName.Text = newName;
                    _lblUsername.Text = $"@{newUsername}";

                    _avatar.SetName(newName);
                    if (loadTask != null)
                    {
                        var img = await loadTask;
                        if (_isClosing || IsDisposed) return;
                        // Only apply if no newer update arrived
                        if (_avatarLoadVersion != thisVersion)
                        {
                            img?.Dispose();
                            return;
                        }
                        if (img != null)
                        {
                            var old = _avatar.Photo;
                            _avatar.Photo = new Bitmap(img);
                            old?.Dispose();
                            img.Dispose();
                        }
                    }
                    else if (avatarRemoved)
                    {
                        var old = _avatar.Photo;
                        _avatar.Photo = null;
                        old?.Dispose();
                    }
                    _avatar.Invalidate();
                }
                catch { }
            }));
        }
    }
}
