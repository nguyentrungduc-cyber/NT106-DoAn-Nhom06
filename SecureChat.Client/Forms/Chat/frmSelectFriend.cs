using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Services;
using SecureChat.DTOs;

namespace SecureChat.Client.Forms.Chat
{
    public sealed class frmSelectFriend : Form
    {
        private readonly HashSet<string> _excludeUserIds;
        private List<FriendResponse> _allFriends = new();
        private Panel _pnlList = null!;
        private TextBox _txtSearch;
        private System.Windows.Forms.Timer _fadeTimer;

        public string? SelectedUserId { get; private set; }
        public string? SelectedUserPublicKey { get; private set; }
        public string? SelectedDisplayName { get; private set; }

        public frmSelectFriend(HashSet<string>? excludeUserIds = null)
        {
            _excludeUserIds = excludeUserIds ?? new HashSet<string>();
            _ = LoadFriendsAsync();

            Text = "Select Friend";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            ClientSize = new Size(420, 560);
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
                Text = "Add member",
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(20, 16),
                Size = new Size(300, 34)
            };

            var btnClose = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI", 12f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(0x2D, 0x3B, 0x4E),
                Size = new Size(30, 30),
                Location = new Point(ClientSize.Width - 46, 14),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, __) => DialogResult = DialogResult.Cancel;

            _txtSearch = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
                ForeColor = Color.FromArgb(0x7F, 0x8D, 0x9A),
                Location = new Point(20, 62),
                Size = new Size(380, 26),
                Text = "Search friends..."
            };
            _txtSearch.GotFocus += (_, __) =>
            {
                if (_txtSearch.Text == "Search friends...")
                {
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D);
                }
            };
            _txtSearch.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "Search friends...";
                    _txtSearch.ForeColor = Color.FromArgb(0x7F, 0x8D, 0x9A);
                }
            };
            _txtSearch.TextChanged += (_, __) =>
            {
                if (!_txtSearch.Focused) return;
                BuildFriendList();
            };

            var sep = new Panel
            {
                Location = new Point(20, 98),
                Size = new Size(380, 1),
                BackColor = Color.FromArgb(0xE6, 0xEB, 0xF1)
            };

            _pnlList = new Panel
            {
                Location = new Point(0, 108),
                Size = new Size(420, ClientSize.Height - 108 - 60),
                AutoScroll = true,
                BackColor = Color.White
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(0x2A, 0xAB, 0xEE),
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Size = new Size(90, 34),
                Location = new Point(ClientSize.Width - 110, 520),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] { lblTitle, btnClose, _txtSearch, sep, _pnlList, btnCancel });
        }

        private async System.Threading.Tasks.Task LoadFriendsAsync()
        {
            var (ok, res, err) = await ApiClient.Instance.GetAsync<List<FriendResponse>>("api/friends");
            if (ok && res != null)
            {
                _allFriends = res
                    .Where(f => f.Friend != null && !_excludeUserIds.Contains(f.Friend.UserID))
                    .ToList();
                BuildFriendList();
            }
            else
            {
                _pnlList.Controls.Clear();
                _pnlList.Controls.Add(new Label
                {
                    Text = "No friends available.",
                    Font = new Font("Segoe UI", 12f),
                    ForeColor = Color.FromArgb(0x8A, 0x98, 0xA6),
                    Location = new Point(20, 20),
                    Size = new Size(380, 30),
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }
        }

        private void BuildFriendList()
        {
            _pnlList.SuspendLayout();
            _pnlList.Controls.Clear();

            var query = _allFriends.AsEnumerable();
            var keyword = _txtSearch.Text.Trim();
            if (!string.IsNullOrWhiteSpace(keyword) && _txtSearch.ForeColor != Color.FromArgb(0x7F, 0x8D, 0x9A))
            {
                query = query.Where(f =>
                    f.Friend != null && (
                    f.Friend.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    f.Friend.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            int top = 8;
            foreach (var friend in query)
            {
                if (friend?.Friend == null) continue;
                var row = BuildFriendRow(friend);
                row.Location = new Point(0, top);
                _pnlList.Controls.Add(row);
                top += row.Height + 4;
            }

            if (top == 8)
            {
                _pnlList.Controls.Add(new Label
                {
                    Text = "No results found.",
                    Font = new Font("Segoe UI", 12f),
                    ForeColor = Color.FromArgb(0x8A, 0x98, 0xA6),
                    Location = new Point(20, 20),
                    Size = new Size(380, 30),
                    TextAlign = ContentAlignment.MiddleCenter
                });
            }

            _pnlList.ResumeLayout();
        }

        private Panel BuildFriendRow(FriendResponse friend)
        {
            const int rowWidth = 420;
            const int nameX = 84;
            const int nameWidth = rowWidth - nameX - 16;

            var row = new Panel
            {
                Size = new Size(rowWidth, 80),
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };

            var f = friend.Friend;
            string initials = f.DisplayName?.Length > 0
                ? f.DisplayName[..1].ToUpperInvariant()
                : "?";
            var avatarColor = TG.GetAvatarColor(f.DisplayName ?? "?");

            var avatar = new Panel
            {
                Location = new Point(20, 14),
                Size = new Size(52, 52),
                BackColor = avatarColor
            };
            avatar.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = new GraphicsPath();
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
                Text = f.DisplayName ?? f.Username ?? "Unknown",
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = Color.FromArgb(0x1F, 0x2D, 0x3D),
                Location = new Point(nameX, 14),
                Size = new Size(nameWidth, 30),
                AutoEllipsis = true
            };

            var lblUsername = new Label
            {
                Text = $"@{f.Username}",
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(0x8A, 0x98, 0xA6),
                Location = new Point(nameX, 46),
                Size = new Size(nameWidth, 24),
                AutoEllipsis = true
            };

            row.Controls.AddRange(new Control[] { avatar, lblName, lblUsername });

            row.MouseEnter += (_, __) => row.BackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
            row.MouseLeave += (_, __) => row.BackColor = Color.White;
            row.Click += (_, __) => SelectFriend(friend);

            foreach (Control c in new Control[] { avatar, lblName, lblUsername })
            {
                c.MouseEnter += (_, __) => row.BackColor = Color.FromArgb(0xF5, 0xF7, 0xFA);
                c.MouseLeave += (_, __) => row.BackColor = Color.White;
                c.Click += (_, __) => SelectFriend(friend);
            }

            return row;
        }

        private void SelectFriend(FriendResponse friend)
        {
            var f = friend.Friend;
            SelectedUserId = f.UserID;
            SelectedUserPublicKey = f.PublicKey;
            SelectedDisplayName = f.DisplayName ?? f.Username;
            DialogResult = DialogResult.OK;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fadeTimer.Stop();
            _fadeTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
