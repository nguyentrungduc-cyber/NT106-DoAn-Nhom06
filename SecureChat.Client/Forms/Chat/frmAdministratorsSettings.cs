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
            _conversationId = conversationId;
            _adminsCount = Math.Max(0, currentCount);

            Text = "Administrators";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = Color.White;
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
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(20, 16),
                Size = new Size(300, 34)
            };

            var pnlSearch = new Panel
            {
                Location = new Point(0, 62),
                Size = new Size(500, 54),
                BackColor = Color.White
            };
            var txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(0x7F, 0x8D, 0x9A),
                Text = "Search",
                Location = new Point(54, 16),
                Size = new Size(420, 26)
            };
            var lblSearchIcon = new Label
            {
                Text = "\U0001F50D",
                Font = new Font("Segoe UI Emoji", 13f),
                ForeColor = Color.FromArgb(0x8E, 0x9A, 0xA7),
                Location = new Point(16, 10),
                Size = new Size(32, 32),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var sep = new Panel { Location = new Point(0, 53), Size = new Size(500, 1), BackColor = Color.FromArgb(0xE6, 0xEB, 0xF1) };
            pnlSearch.Controls.AddRange(new Control[] { lblSearchIcon, txtSearch, sep });

            // Panel chứa danh sách admin — sẽ được populate từ API
            _pnlAdmins = new Panel
            {
                Location = new Point(0, 120),
                Size = new Size(500, 560),
                AutoScroll = true,
                BackColor = Color.White
            };

            _lblCount = new Label
            {
                Text = $"Administrators: {_adminsCount}",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(0x8A, 0x98, 0xA6),
                Location = new Point(20, 688),
                Size = new Size(200, 24)
            };

            var btnClose = BuildBottomButton("Close", Color.FromArgb(0x2A, 0xAB, 0xEE), false, 90);
            btnClose.Location = new Point(390, 698);
            btnClose.Click += (_, __) => DialogResult = DialogResult.OK;

            Controls.AddRange(new Control[] { lblTitle, pnlSearch, _pnlAdmins, _lblCount, btnClose });

            _ = LoadAdminsAsync();
        }

        private async Task LoadAdminsAsync()
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.GetAsync($"api/conversations/{_conversationId}/members");
                if (!res.IsSuccessStatusCode) return;

                var json = await res.Content.ReadAsStringAsync();
                var opts = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MemberResponse>>(json, opts);
                if (list == null) return;

                var admins = list.Where(m =>
    m.Role == SecureChat.Models.MemberRole.Owner ||
    m.Role == SecureChat.Models.MemberRole.Moderator).ToList();

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
            var row = new Panel { Size = new Size(500, 84), BackColor = Color.White };

            var initials = displayName.Length >= 2
                ? $"{displayName[0]}".ToUpper()
                : displayName.ToUpper();

            var avatar = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(52, 52),
                BackColor = Color.FromArgb(0x5C, 0xA5, 0xEC)
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
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(92, 16),
                Size = new Size(220, 28)
            };
            var lblRole = new Label
            {
                Text = role,
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(0x7D, 0x8B, 0x98),
                Location = new Point(92, 46),
                Size = new Size(120, 24)
            };

            var isOwner = role.Equals("Owner", StringComparison.OrdinalIgnoreCase);
            var roleBadge = new Label
            {
                Text = role.ToLower(),
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = isOwner ? Color.FromArgb(0x9A, 0x77, 0xD5) : Color.FromArgb(0x2A, 0xAB, 0xEE),
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
    }
}