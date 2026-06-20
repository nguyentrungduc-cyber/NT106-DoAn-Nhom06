using SecureChat.Client.Services;
using SecureChat.DTOs;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmMembersSettings : Form
    {

        // Thêm biến này để lưu trữ ID của nhóm hiện tại
        private readonly string _conversationId;
        private HashSet<string> _existingUserIds = new();
        private bool _isAdding;
        public sealed record MemberItemData(string Name, string Status, string Role, Color AvatarColor, string Initials);

        private System.Windows.Forms.Timer _fadeTimer;
        private TextBox _txtSearch;
        private Panel _pnlList;
        private List<MemberItemData> _allMembers = new();

        public IReadOnlyList<MemberItemData> Members => _allMembers;

        public frmMembersSettings(string conversationId)
        {
            _conversationId = conversationId;  // Gán vào biến
            _ = LoadMembersAsync();
            Text = "Members";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(500, 740);
            Opacity = 0;
            DoubleBuffered = true;

            _fadeTimer = new System.Windows.Forms.Timer { Interval = 14 };
            _fadeTimer.Tick += (_, __) =>
            {
                if (Opacity >= 1) { _fadeTimer.Stop(); return; }
                Opacity = Math.Min(1, Opacity + 0.12);
            };
            Shown += (_, __) => _fadeTimer.Start();

            var lblTitle = new Label
            {
                Text = "Members",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(20, 16),
                Size = new Size(300, 34)
            };

            var searchWrap = new Panel
            {
                Location = new Point(0, 62),
                Size = new Size(500, 52),
                BackColor = Color.White
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

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(0x7F, 0x8D, 0x9A),
                Location = new Point(56, 15),
                Size = new Size(420, 26),
                Text = "Search"
            };
            _txtSearch.GotFocus += (_, __) =>
            {
                if (_txtSearch.Text == "Search")
                {
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D);
                }
            };
            _txtSearch.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "Search";
                    _txtSearch.ForeColor = Color.FromArgb(0x7F, 0x8D, 0x9A);
                }
            };
            _txtSearch.TextChanged += (_, __) =>
            {
                if (!_txtSearch.Focused) return;
                BuildMemberRows(_txtSearch.Text.Trim());
            };

            var sep = new Panel { Location = new Point(0, 51), Size = new Size(500, 1), BackColor = Color.FromArgb(0xE6, 0xEB, 0xF1) };
            searchWrap.Controls.AddRange(new Control[] { lblSearchIcon, _txtSearch, sep });

            _pnlList = new Panel
            {
                Location = new Point(0, 114),
                Size = new Size(500, 560),
                AutoScroll = true,
                BackColor = Color.White
            };

            var btnAdd = BuildBottomButton("Add members", Color.FromArgb(0x2A, 0xAB, 0xEE), true, 140);
            btnAdd.Location = new Point(20, 690);
            btnAdd.Click += (_, __) => AddMember();

            var btnClose = BuildBottomButton("Close", Color.FromArgb(0x2A, 0xAB, 0xEE), false, 90);
            btnClose.Location = new Point(390, 690);
            btnClose.Click += (_, __) => DialogResult = DialogResult.OK;

            Controls.AddRange(new Control[]
            {
                lblTitle, searchWrap, _pnlList, btnAdd, btnClose
            });

            BuildMemberRows(string.Empty);
        }

        private void BuildMemberRows(string keyword)
        {
            _pnlList.SuspendLayout();
            _pnlList.Controls.Clear();

            var query = _allMembers.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(m => m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                      || m.Status.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                      || m.Role.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            int top = 12;
            foreach (var m in query)
            {
                var row = BuildMemberRow(m);
                row.Location = new Point(0, top);
                _pnlList.Controls.Add(row);
                top += row.Height;
            }

            _pnlList.ResumeLayout();
        }

        private Panel BuildMemberRow(MemberItemData m)
        {
            var row = new Panel
            {
                Size = new Size(500, 84),
                BackColor = Color.White
            };

            var avatar = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(52, 52),
                BackColor = m.AvatarColor
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
                Text = m.Initials,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 16f)
            };
            avatar.Controls.Add(lblInitial);

            var lblName = new Label
            {
                Text = m.Name,
                Font = new Font("Segoe UI Semibold", 16f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(92, 16),
                Size = new Size(240, 30)
            };

            var lblStatus = new Label
            {
                Text = m.Status,
                Font = new Font("Segoe UI", 12f),
                ForeColor = string.Equals(m.Status, "online", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(0x2A, 0xAB, 0xEE)
                    : Color.FromArgb(0x8A, 0x98, 0xA6),
                Location = new Point(92, 46),
                Size = new Size(230, 24)
            };

            row.Controls.AddRange(new Control[] { avatar, lblName, lblStatus });

            if (!string.IsNullOrWhiteSpace(m.Role))
            {
                var badge = new Label
                {
                    Text = m.Role,
                    Font = new Font("Segoe UI Semibold", 11f),
                    ForeColor = Color.FromArgb(0x9A, 0x77, 0xD5),
                    BackColor = Color.FromArgb(0xEF, 0xE8, 0xFF),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(420, 30),
                    Size = new Size(64, 28)
                };
                badge.Paint += (_, e) =>
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using var p = new System.Drawing.Drawing2D.GraphicsPath();
                    p.AddArc(0, 0, 14, 14, 180, 90);
                    p.AddArc(badge.Width - 14, 0, 14, 14, 270, 90);
                    p.AddArc(badge.Width - 14, badge.Height - 14, 14, 14, 0, 90);
                    p.AddArc(0, badge.Height - 14, 14, 14, 90, 90);
                    p.CloseFigure();
                    badge.Region = new Region(p);
                };
                row.Controls.Add(badge);
            }

            return row;
        }

        private async void AddMember()
        {
            // 1. Open picker (no re-entrancy risk — modal dialog blocks clicks)
            using var picker = new frmSelectFriend(_existingUserIds);
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            if (string.IsNullOrWhiteSpace(picker.SelectedUserId))
                return;

            var targetUserId = picker.SelectedUserId;

            // 2. Guard — prevent double-submit while API call is in flight
            if (_isAdding) return;
            _isAdding = true;
            try
            {
                // 3. Re-check membership — member may have been added while
                //    picker was open (by another admin or SignalR event).
                if (_existingUserIds.Contains(targetUserId))
                {
                    MessageBox.Show(this, "This user is already a member.",
                        "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 4. Validate public key — RSA-encrypt will fail if empty
                if (string.IsNullOrWhiteSpace(picker.SelectedUserPublicKey))
                {
                    MessageBox.Show(this, "This friend does not have a public key. Cannot share group key.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 5. Get fresh encrypted conversation key for the new member
                //    (always fresh — cache is cleared inside the method to
                //     avoid stale key after rekey).
                var decryptor = new MessageDecryptor();
                string? encryptedKey;
                try
                {
                    encryptedKey = await decryptor.EncryptConversationKeyForMemberAsync(
                        _conversationId, picker.SelectedUserPublicKey);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Failed to encrypt group key: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(encryptedKey))
                {
                    MessageBox.Show(this, "Cannot retrieve group encryption key. Try again.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var req = new AddMemberRequest(
                    UserID: targetUserId,
                    EncryptedKey: encryptedKey,
                    Role: SecureChat.Models.MemberRole.Member
                );

                // 6. Show progress in title bar
                var titleBackup = Text;
                Text = "Adding member...";

                var (ok, res, err) = await ApiClient.Instance.PostAsync<AddMemberRequest, object>(
                    $"api/conversations/{_conversationId}/members", req);

                Text = titleBackup;

                if (!ok)
                {
                    string userMsg = err switch
                    {
                        string e when e.Contains("Conflict") || e.Contains("already a member") =>
                            "This user is already a member.",
                        string e when e.Contains("Forbidden") || e.Contains("403") =>
                            "You don't have permission to add members.",
                        string e when e.Contains("Not Found") || e.Contains("404") =>
                            "User or conversation not found.",
                        string e when e.Contains("timeout") || e.Contains("timed out") =>
                            "Request timed out. Please check your connection and try again.",
                        _ => $"Cannot add member: {err}"
                    };
                    MessageBox.Show(this, userMsg, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 7. Success — refresh member list from API (authoritative).
                _ = RefreshMembersAsync();
            }
            finally
            {
                _isAdding = false;
            }
        }

        private async System.Threading.Tasks.Task RefreshMembersAsync()
        {
            var (ok, res, err) = await ApiClient.Instance
                .GetAsync<List<MemberResponse>>($"api/conversations/{_conversationId}/members");

            if (ok && res != null)
            {
                _existingUserIds = res
                    .Where(m => m.LeftAt == null)
                    .Select(m => m.UserID)
                    .ToHashSet();

                _allMembers = res.Select(m =>
                {
                    var status = (m.User?.ShowOnlineStatus == true)
                        ? Helpers.PresenceFormatter.GetPresenceText(m.IsOnline, m.User?.LastSeenUtc)
                        : "offline";
                    return new MemberItemData(
                        m.User?.DisplayName ?? m.Nickname ?? "Unknown",
                        status,
                        m.Role.ToString(),
                        Color.FromArgb(0x5C, 0xA5, 0xEC),
                        (m.User?.DisplayName ?? "U").Length > 0
                            ? m.User!.DisplayName[..1].ToUpper()
                            : "U"
                    );
                }).ToList();

                BuildMemberRows(_txtSearch.Text.Trim());
            }
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

        private async Task LoadMembersAsync()
        {
            var (ok, res, err) = await SecureChat.Client.Services.ApiClient.Instance
                .GetAsync<List<MemberResponse>>($"api/conversations/{_conversationId}/members");

            if (ok && res != null)
            {
                _existingUserIds = res
                    .Where(m => m.LeftAt == null)
                    .Select(m => m.UserID)
                    .ToHashSet();

                _allMembers = res.Select(m =>
                {
                    var status = (m.User?.ShowOnlineStatus == true)
                        ? SecureChat.Client.Helpers.PresenceFormatter.GetPresenceText(m.IsOnline, m.User?.LastSeenUtc)
                        : "offline";
                    return new MemberItemData(
                        m.User?.DisplayName ?? m.Nickname ?? "Unknown",
                        status,
                        m.Role.ToString(),
                        Color.FromArgb(0x5C, 0xA5, 0xEC),
                        (m.User?.DisplayName ?? "U").Length > 0
                            ? m.User!.DisplayName[..1].ToUpper()
                            : "U"
                    );
                }).ToList();

                BuildMemberRows(string.Empty);
            }
        }
    }
}
