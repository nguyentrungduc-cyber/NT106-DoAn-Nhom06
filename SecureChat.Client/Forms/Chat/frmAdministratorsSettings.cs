using System.Drawing;
using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmAdministratorsSettings : Form
    {
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private readonly Label _lblCount;
        private readonly Panel _pnlAdmins;
        private readonly string _conversationId;
        private int _adminsCount;

        public int AdministratorsCount => _adminsCount;

        public frmAdministratorsSettings(string conversationId, int currentCount)
        {
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            _conversationId = conversationId;
            _adminsCount = Math.Max(0, currentCount);

            Text = "Administrators";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
            SecureChat.Client.Services.ThemeRefreshHelper.Hook(this);
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(500, 740);
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
                Text = "Administrators",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(300, 34)
            };

            var pnlSearch = new Panel
            {
                Location = new Point(0, 62),
                Size = new Size(500, 54),
                BackColor = TG.WindowBg
            };
            var txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
                ForeColor = TG.TextSecondary,
                Text = "Search",
                Location = new Point(54, 16),
                Size = new Size(420, 26)
            };
            var lblSearchIcon = new Label
            {
                Text = "\U0001F50D",
                Font = new Font("Segoe UI Emoji", 13f),
                ForeColor = TG.TextSecondary,
                Location = new Point(16, 10),
                Size = new Size(32, 32),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var sep = new Panel { Location = new Point(0, 53), Size = new Size(500, 1), BackColor = TG.Divider };
            pnlSearch.Controls.AddRange(new Control[] { lblSearchIcon, txtSearch, sep });

            // Panel chứa danh sách admin — sẽ được populate từ API
            _pnlAdmins = new Panel
            {
                Location = new Point(0, 120),
                Size = new Size(500, 560),
                AutoScroll = true,
                BackColor = TG.WindowBg
            };

            _lblCount = new Label
            {
                Text = $"Administrators: {_adminsCount}",
                Font = new Font("Segoe UI", 10f),
                ForeColor = TG.TextSecondary,
                Location = new Point(20, 688),
                Size = new Size(200, 24)
            };

            var btnClose = BuildBottomButton("Close", TG.Blue, false, 90);
            btnClose.Location = new Point(390, 698);
            btnClose.Click += (_, __) => DialogResult = DialogResult.OK;

            Controls.AddRange(new Control[] { lblTitle, pnlSearch, _pnlAdmins, _lblCount, btnClose });

            _ = LoadAdminsAsync();
        }

        private async Task LoadAdminsAsync()
        {
            try
            {
                var (ok, view, _) = await SecureChat.Client.Services.ApiClient.Instance
                    .GetAsync<SecureChat.DTOs.ConversationViewResponse>($"api/conversations/{_conversationId}/view");
                if (!ok || view?.Admins == null) return;

                var admins = view.Admins;
                _adminsCount = admins.Count;

                BeginInvoke(new Action(() =>
                {
                    _pnlAdmins.Controls.Clear();
                    int y = 0;
                    foreach (var m in admins)
                    {
                        var row = BuildAdminRow(
                            m.User?.DisplayName ?? m.Nickname ?? m.User?.Username ?? "Unknown",
                            m.Role.ToString());
                        row.Location = new Point(0, y);
                        _pnlAdmins.Controls.Add(row);
                        y += 84;
                    }
                    _lblCount.Text = $"Administrators: {_adminsCount}";
                }));
            }
            catch { }
        }

        private static Panel BuildAdminRow(string displayName, string role)
        {
            var row = new Panel { Size = new Size(500, 84), BackColor = TG.WindowBg };

            var initials = displayName.Length >= 2
                ? $"{displayName[0]}".ToUpper()
                : displayName.ToUpper();

            var avatar = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(52, 52),
                BackColor = TG.Blue
            };
            avatar.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var path = new System.Drawing.Drawing2D.GraphicsPath();

                path.AddEllipse(0, 0, avatar.Width, avatar.Height);
                avatar.Region = new Region(path);
            };
            var lblInitial = new Label
            {
                Text = initials,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 16f)
            };
            avatar.Controls.Add(lblInitial);

            var lblName = new Label
            {
                Text = displayName,
                Font = new Font("Segoe UI Semibold", 13f),
                ForeColor = TG.TextPrimary,
                Location = new Point(92, 16),
                Size = new Size(220, 28)
            };
            var lblRole = new Label
            {
                Text = role,
                Font = new Font("Segoe UI", 11f),
                ForeColor = TG.TextSecondary,
                Location = new Point(92, 46),
                Size = new Size(120, 24)
            };

            var isOwner = role.Equals("Owner", StringComparison.OrdinalIgnoreCase);
            var roleBadge = new Label
            {
                Text = role.ToLower(),
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = isOwner ? Color.FromArgb(0x9A, 0x77, 0xD5) : TG.Blue,
                BackColor = isOwner ? Color.FromArgb(0xEF, 0xE8, 0xFF) : Color.FromArgb(0xE3, 0xF4, 0xFF),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(416, 28),
                Size = new Size(68, 28)
            };
            roleBadge.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var p = new System.Drawing.Drawing2D.GraphicsPath();
                p.AddArc(0, 0, 14, 14, 180, 90);
                p.AddArc(roleBadge.Width - 14, 0, 14, 14, 270, 90);
                p.AddArc(roleBadge.Width - 14, roleBadge.Height - 14, 14, 14, 0, 90);
                p.AddArc(0, roleBadge.Height - 14, 14, 14, 90, 90);
                p.CloseFigure();
                roleBadge.Region = new Region(p);
            };

            row.Controls.AddRange(new Control[] { avatar, lblName, lblRole, roleBadge });
            return row;
        }

        private static Button BuildBottomButton(string text, Color color, bool bold, int width)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", 11f, bold ? FontStyle.Bold : FontStyle.Regular),
                Size = new Size(width, 34),
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
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            ThemeRefreshHelper.ApplyTo(this);  // ← delegate hết cho helper
        }

    }
}
