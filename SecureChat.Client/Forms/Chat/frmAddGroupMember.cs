using System;
using SecureChat.Client.Services;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Components.Group;

namespace SecureChat.Client.Forms.Chat
{
    /// <summary>
    /// Dialog chọn bạn bè để thêm vào nhóm đã tồn tại.
    /// Tái dùng ucUserItem, loại trừ những người đã là thành viên.
    /// </summary>
    public sealed class frmAddGroupMember : Form
    {
        private readonly List<ucUserItem> _allUsers = new();
        private readonly HashSet<string> _selectedUserIds = new();
        private readonly HashSet<string> _existingMemberIds;

        private FlowLayoutPanel _flpList;
        private TextBox _txtSearch;
        private Label _lblCount;
        private Button _btnAdd;

        private readonly Color[] _palette = {
            Color.FromArgb(0x5A, 0xC8, 0xFA), Color.FromArgb(0xFF, 0x9F, 0x43),
            Color.FromArgb(0x9B, 0x59, 0xB6), Color.FromArgb(0x2E, 0xCC, 0x71),
            Color.FromArgb(0xE7, 0x4C, 0x3C), Color.FromArgb(0x34, 0x98, 0xDB),
        };

        public List<string> ResultMemberIds { get; private set; } = new();

        /// <param name="existingMemberUserIds">UserID của các thành viên đã có trong nhóm (sẽ bị loại khỏi danh sách chọn)</param>
        public frmAddGroupMember(IEnumerable<string> existingMemberUserIds)
        {
            ThemeRefreshHelper.Hook(this);
            _existingMemberIds = new HashSet<string>(existingMemberUserIds);
            Text = "Thêm thành viên";
            Size = new Size(420, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = TG.WindowBg;

            BuildUI();
            Shown += async (_, __) => await LoadFriendsAsync();
        }

        private void BuildUI()
        {
            var lblTitle = new Label
            {
                Text = "Chọn bạn bè để thêm vào nhóm",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Location = new Point(16, 14),
                AutoSize = true,
            };

            _txtSearch = new TextBox
            {
                Location = new Point(16, 48),
                Width = 380,
                PlaceholderText = "Tìm kiếm...",
            };
            _txtSearch.TextChanged += (_, __) => FilterUsers(_txtSearch.Text);

            var pnlListContainer = new Panel
            {
                Location = new Point(16, 78),
                Size = new Size(380, 380),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
            };

            _flpList = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
            };
            pnlListContainer.Controls.Add(_flpList);

            _lblCount = new Label
            {
                Text = "Đã chọn: 0",
                Location = new Point(16, 466),
                AutoSize = true,
                ForeColor = TG.TextSecondary,
            };

            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(214, 490),
                Size = new Size(85, 32),
                DialogResult = DialogResult.Cancel,
            };

            _btnAdd = new Button
            {
                Text = "Thêm",
                Location = new Point(311, 490),
                Size = new Size(85, 32),
                Enabled = false,
                BackColor = Color.FromArgb(0x2A, 0xAB, 0xEE),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
            };
            _btnAdd.FlatAppearance.BorderSize = 0;
            _btnAdd.Click += (_, __) =>
            {
                ResultMemberIds = _selectedUserIds.ToList();
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.Add(lblTitle);
            Controls.Add(_txtSearch);
            Controls.Add(pnlListContainer);
            Controls.Add(_lblCount);
            Controls.Add(btnCancel);
            Controls.Add(_btnAdd);

            CancelButton = btnCancel;
        }

        private async System.Threading.Tasks.Task LoadFriendsAsync()
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.GetAsync("api/friends");
                if (!res.IsSuccessStatusCode) return;

                var json = await res.Content.ReadAsStringAsync();
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.FriendResponse>>(json, opts);
                if (list == null) return;

                // Loại bỏ những người đã là thành viên nhóm
                var candidates = list.Where(f => !_existingMemberIds.Contains(f.Friend.UserID)).ToList();

                BeginInvoke(new Action(() =>
                {
                    _allUsers.Clear();
                    _flpList.Controls.Clear();
                    int idx = 0;
                    foreach (var f in candidates)
                    {
                        string status = f.Friend.ShowOnlineStatus
                            ? SecureChat.Client.Helpers.PresenceFormatter.GetPresenceText(f.IsOnline, f.Friend.LastSeenUtc)
                            : "offline";

                        var item = new ucUserItem(f.Friend.DisplayName, status, _palette[idx++ % _palette.Length])
                        {
                            UserId = f.Friend.UserID,
                            Width = 360,
                        };
                        item.SelectionChanged += OnUserSelectionChanged;
                        _allUsers.Add(item);
                        _flpList.Controls.Add(item);
                    }

                    if (_allUsers.Count == 0)
                    {
                        var lblEmpty = new Label
                        {
                            Text = "Không còn bạn bè nào để thêm vào nhóm.",
                            ForeColor = TG.TextSecondary,
                            AutoSize = true,
                            Padding = new Padding(8),
                        };
                        _flpList.Controls.Add(lblEmpty);
                    }
                }));
            }
            catch { /* giữ list rỗng nếu lỗi mạng */ }
        }

        private void OnUserSelectionChanged(ucUserItem item, bool selected)
        {
            if (selected) _selectedUserIds.Add(item.UserId);
            else _selectedUserIds.Remove(item.UserId);

            _lblCount.Text = $"Đã chọn: {_selectedUserIds.Count}";
            _btnAdd.Enabled = _selectedUserIds.Count > 0;
        }

        private void FilterUsers(string query)
        {
            query = query.Trim();
            _flpList.SuspendLayout();
            foreach (var item in _allUsers)
            {
                bool match = string.IsNullOrEmpty(query) ||
                             item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
                item.Visible = match;
            }
            _flpList.ResumeLayout();
        }
    }
}
