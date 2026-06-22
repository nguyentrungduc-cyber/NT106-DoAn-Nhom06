using SecureChat.Client.Services;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmMembersSettings : Form
    {

        // Thêm biến này để lưu trữ ID của nhóm hiện tại
        private readonly string _conversationId;
        public sealed record MemberItemData(string Name, string Status, string Role, Color AvatarColor, string Initials);

        private System.Windows.Forms.Timer _fadeTimer;
        private TextBox _txtSearch;
        private Panel _pnlList;
        private List<MemberItemData> _allMembers;

        public IReadOnlyList<MemberItemData> Members => _allMembers;

        public frmMembersSettings(string conversationId)
        {
            ThemeRefreshHelper.Hook(this);
            _conversationId = conversationId;  // Gán vào biến
            _ = LoadMembersAsync();
            Text = "Members";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = TG.WindowBg;
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
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(300, 34)
            };

            var searchWrap = new Panel
            {
                Location = new Point(0, 62),
                Size = new Size(500, 52),
                BackColor = TG.WindowBg
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

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
                ForeColor = TG.TextSecondary,
                Location = new Point(56, 15),
                Size = new Size(420, 26),
                Text = "Search"
            };
            _txtSearch.GotFocus += (_, __) =>
            {
                if (_txtSearch.Text == "Search")
                {
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = TG.TextPrimary;
                }
            };
            _txtSearch.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "Search";
                    _txtSearch.ForeColor = TG.TextSecondary;
                }
            };
            _txtSearch.TextChanged += (_, __) =>
            {
                if (!_txtSearch.Focused) return;
                BuildMemberRows(_txtSearch.Text.Trim());
            };

            var sep = new Panel { Location = new Point(0, 51), Size = new Size(500, 1), BackColor = TG.Divider };
            searchWrap.Controls.AddRange(new Control[] { lblSearchIcon, _txtSearch, sep });

            _pnlList = new Panel
            {
                Location = new Point(0, 114),
                Size = new Size(500, 560),
                AutoScroll = true,
                BackColor = TG.WindowBg
            };

            var btnAdd = BuildBottomButton("Add members", TG.Blue, true, 140);
            btnAdd.Location = new Point(20, 690);
            btnAdd.Click += (_, __) => AddMember();

            var btnClose = BuildBottomButton("Close", TG.Blue, false, 90);
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
                BackColor = TG.WindowBg
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
                ForeColor = TG.TextPrimary,
                Location = new Point(92, 16),
                Size = new Size(240, 30)
            };

            var lblStatus = new Label
            {
                Text = m.Status,
                Font = new Font("Segoe UI", 12f),
                ForeColor = string.Equals(m.Status, "online", StringComparison.OrdinalIgnoreCase)
                    ? TG.Blue
                    : TG.TextSecondary,
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

        // SỬA LẠI HÀM AddMember() trong frmMembersSettings.cs
        private async void AddMember()
        {
            // 1. Mở Form chọn bạn bè (Giả sử bạn có frmSelectFriend trả về ID)
            // using var frm = new frmSelectFriend();
            // if (frm.ShowDialog() != DialogResult.OK) return;
            // string targetUserId = frm.SelectedUserId;

            string targetUserId = "ID_LAY_TU_FORM_CHON_BAN_BE"; // Tạm thời để string cho bạn dễ hình dung

            // Lưu ý: Cần thuật toán sinh khóa AES mới cho user này rồi mã hóa bằng Public Key của họ.
            // Ở đây dùng string tạm, bạn nhớ thay bằng hàm Crypto thực tế nhé.
            string encryptedKeyForNewMember = "KHOA_AES_DA_MA_HOA_BANG_PUBLIC_KEY_CUA_NEW_MEMBER";

            var req = new SecureChat.DTOs.AddMemberRequest(
                UserID: targetUserId,
                EncryptedKey: encryptedKeyForNewMember,
                Role: SecureChat.Models.MemberRole.Member
            );

            // Vô hiệu hóa nút trong lúc chờ API (tránh spam click)
            var btn = (Button)ActiveControl;
            btn.Enabled = false;
            btn.Text = "Adding...";

            var (ok, res, err) = await ApiClient.Instance.PostAsync<SecureChat.DTOs.AddMemberRequest, object>($"api/conversations/{_conversationId}/members", req);

            btn.Enabled = true;
            btn.Text = "Add members";

            if (!ok)
            {
                MessageBox.Show($"Không thể thêm thành viên: {err}", "Lỗi Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("Thêm thành viên thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 2. Gọi API tải lại danh sách thành viên mới (hoặc load lại form)
            // Nếu bạn muốn tải lại, bạn có thể gọi lại 1 hàm FetchMembers() chứa logic lấy danh sách từ API ở đây, 
            // sau đó gán lại vào _allMembers và gọi BuildMemberRows(_txtSearch.Text.Trim());
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
            // Gọi API lấy danh sách thành viên thật từ database
            var (ok, res, err) = await SecureChat.Client.Services.ApiClient.Instance
                .GetAsync<List<SecureChat.DTOs.MemberResponse>>($"api/conversations/{_conversationId}/members");

            if (ok && res != null)
            {
                // Chuyển đổi dữ liệu từ API (res) sang định dạng _allMembers của Form
                _allMembers = res.Select(m =>
                {
                    var status = (m.User?.ShowOnlineStatus == true)
                        ? SecureChat.Client.Helpers.PresenceFormatter.GetPresenceText(m.IsOnline, m.User?.LastSeenUtc)
                        : "offline";
                    return new MemberItemData(
                        m.User?.Username ?? "Unknown",
                        status,
                        m.Role.ToString(),
                        TG.Blue, // Màu mặc định
                        (m.User?.Username ?? "U").Substring(0, 1).ToUpper()
                    );
                }).ToList();

                // Vẽ lại danh sách lên màn hình
                BuildMemberRows(string.Empty);
            }
        }
    }
}
