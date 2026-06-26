using SecureChat.Client.Services;
using SecureChat.Client.Forms.Settings;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmAdministratorsSettings : Form
    {
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private readonly Label _lblCount;
        private readonly Panel _pnlAdmins;
        private readonly string _conversationId;
        private int _adminsCount;
        private bool _searchActive;

        public int AdministratorsCount => _adminsCount;

        public frmAdministratorsSettings(string conversationId, int currentCount)
        {
            _conversationId = conversationId;
            _adminsCount = Math.Max(0, currentCount);

            Text = "Administrators";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
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
                BackColor = TG.WindowBg,
                ForeColor = TG.TextSecondary,
                Text = "Search",
                Tag = "search-tb",
                Location = new Point(54, 16),
                Size = new Size(420, 26)
            };
            txtSearch.GotFocus += (_, __) =>
            {
                if (!_searchActive)
                {
                    txtSearch.Text = string.Empty;
                    txtSearch.ForeColor = TG.TextPrimary;
                    _searchActive = true;
                }
            };
            txtSearch.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = LocalizationService.Translate("Search");
                    txtSearch.ForeColor = TG.TextSecondary;
                    _searchActive = false;
                }
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
            var sep = new Panel { Location = new Point(0, 53), Size = new Size(500, 1), BackColor = TG.Divider, Tag = "sep" };
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
                Tag = "sub",
                Location = new Point(20, 688),
                Size = new Size(200, 24)
            };

            var btnClose = BuildBottomButton("Close", TG.Blue, false, 90);
            btnClose.Tag = "accent-fg";
            btnClose.Location = new Point(390, 698);
            btnClose.Click += (_, __) => DialogResult = DialogResult.OK;

            Controls.AddRange(new Control[] { lblTitle, pnlSearch, _pnlAdmins, _lblCount, btnClose });

            _ = LoadAdminsAsync();
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            UiLocalization.ApplyToForm(this);
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

            var avatar = new AvatarControl
            {
                Location = new Point(20, 14),
                Size = new Size(52, 52),
            };
            avatar.SetName(displayName);

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
                BackColor = isOwner ? Color.FromArgb(0xEF, 0xE8, 0xFF) : TG.SidebarHover,
                Tag = "role-badge",
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
            if (IsDisposed) return;

            ThemeRefreshHelper.ApplyTo(this);

            void FixControls(Control parent)
            {
                foreach (Control c in parent.Controls)
                {
                    if (c.Tag as string == "accent-fg")
                        c.ForeColor = TG.Blue;
                    if (c.Tag as string == "sep")
                        c.BackColor = TG.Divider;
                    if (c.Tag as string == "sub")
                        c.ForeColor = TG.TextSecondary;
                    if (c.Tag as string == "role-badge" && c is Label badge)
                    {
                        bool isOwner = badge.Text.Equals("owner", StringComparison.OrdinalIgnoreCase);
                        badge.ForeColor = isOwner ? Color.FromArgb(0x9A, 0x77, 0xD5) : TG.Blue;
                        badge.BackColor = isOwner ? Color.FromArgb(0xEF, 0xE8, 0xFF) : TG.SidebarHover;
                    }
                    if (c.Tag as string == "search-tb")
                    {
                        c.BackColor = TG.WindowBg;
                        if (c is TextBox tb && !tb.Focused)
                            tb.ForeColor = _searchActive ? TG.TextPrimary : TG.TextSecondary;
                    }
                    if (c.HasChildren)
                        FixControls(c);
                }
            }
            FixControls(this);
        }
    }
}