using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Services;
using SecureChat.Client.Forms.Settings;
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
        private bool _isSearching;

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
            BackColor = TG.WindowBg;
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
                ForeColor = TG.TextPrimary,
                Location = new Point(20, 16),
                Size = new Size(300, 34)
            };

            var btnClose = new Button
            {
                Text = "\u2715",
                Font = new Font("Segoe UI", 12f),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextPrimary,
                Tag = "close-btn",
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
                BackColor = TG.WindowBg,
                ForeColor = TG.TextSecondary,
                Location = new Point(20, 62),
                Size = new Size(380, 26),
                Text = "Search friends..."
            };
            _txtSearch.GotFocus += (_, __) =>
            {
                if (_txtSearch.Text == "Search friends...")
                {
                    _txtSearch.Text = string.Empty;
                    _txtSearch.ForeColor = TG.TextPrimary;
                }
                _isSearching = true;
            };
            _txtSearch.LostFocus += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "Search friends...";
                    _txtSearch.ForeColor = TG.TextSecondary;
                    _isSearching = false;
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
                BackColor = TG.Divider,
                Tag = "sep"
            };

            _pnlList = new Panel
            {
                Location = new Point(0, 108),
                Size = new Size(420, ClientSize.Height - 108 - 60),
                AutoScroll = true,
                BackColor = TG.WindowBg
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.Blue,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                Tag = "accent-fg",
                Size = new Size(90, 34),
                Location = new Point(ClientSize.Width - 110, 520),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[] { lblTitle, btnClose, _txtSearch, sep, _pnlList, btnCancel });
            NightModeService.ThemeChanged += OnThemeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
            UiLocalization.ApplyToForm(this);
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
                    ForeColor = TG.TextSecondary,
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
            var keyword = _isSearching ? _txtSearch.Text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(keyword))
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
                    ForeColor = TG.TextSecondary,
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
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand
            };

            var f = friend.Friend;
            var avatar = new AvatarControl
            {
                Location = new Point(20, 14),
                Size = new Size(52, 52),
            };
            avatar.SetName(f.DisplayName ?? f.Username ?? "?");

            var lblName = new Label
            {
                Text = f.DisplayName ?? f.Username ?? LocalizationService.Translate("Unknown"),
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = TG.TextPrimary,
                Location = new Point(nameX, 14),
                Size = new Size(nameWidth, 30),
                AutoEllipsis = true
            };

            var lblUsername = new Label
            {
                Text = $"@{f.Username}",
                Font = new Font("Segoe UI", 11f),
                ForeColor = TG.TextSecondary,
                Tag = "sub",
                Location = new Point(nameX, 46),
                Size = new Size(nameWidth, 24),
                AutoEllipsis = true
            };

            row.Controls.AddRange(new Control[] { avatar, lblName, lblUsername });

            row.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
            row.MouseLeave += (_, __) => row.BackColor = TG.WindowBg;
            row.Click += (_, __) => SelectFriend(friend);

            foreach (Control c in new Control[] { avatar, lblName, lblUsername })
            {
                c.MouseEnter += (_, __) => row.BackColor = TG.SidebarHover;
                c.MouseLeave += (_, __) => row.BackColor = TG.WindowBg;
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
                    if (c.HasChildren)
                        FixControls(c);
                }
            }
            FixControls(this);

            if (_txtSearch != null && !_txtSearch.IsDisposed)
            {
                _txtSearch.BackColor = TG.WindowBg;
                if (!_txtSearch.Focused)
                {
                    bool showingPlaceholder = string.IsNullOrWhiteSpace(_txtSearch.Text) || _txtSearch.Text == "Search friends...";
                    _txtSearch.ForeColor = showingPlaceholder ? TG.TextSecondary : TG.TextPrimary;
                    if (showingPlaceholder) _isSearching = false;
                }
            }

            foreach (Control row in _pnlList.Controls)
            {
                if (row is Panel p)
                {
                    foreach (Control rc in p.Controls)
                    {
                        if (rc.Tag as string == "sub")
                            rc.ForeColor = TG.TextSecondary;
                    }
                }
            }
        }
    }
}
