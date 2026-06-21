using SecureChat.Client.Components.Group;
using SecureChat.Client.Services;
using System.Drawing.Drawing2D;
using System.Linq;

namespace SecureChat.Client.Forms.Chat
{
    public partial class frmGroupInfo : Form
    {
        private void InitializeComponent() { }

        private const int FORM_WIDTH = 540;
        private const int FORM_HEIGHT = 760;
        private const int HEADER_HEIGHT = 210;
        private const int ACTIONS_HEIGHT = 96;
        private const int MEMBERS_HEADER_HEIGHT = 48;
        private const int BOTTOM_HEIGHT = 18;
        private const int SECTION_PAD = 18;

        private static readonly Color C_DANGER = Color.FromArgb(0xE2, 0x4B, 0x4A);
        // Colors read from TG at paint time

        private Panel _pnlList = null!;
        private PictureBox _pbAvatar = null!;
        private Label _lblName = null!;
        private Label _lblCount = null!;
        private Label _lblMembersTitle = null!;

        private Button _btnMute = null!;
        private Button _btnManage = null!;
        private Button _btnLeave = null!;

        private bool _disableSound;
        private bool _notificationsMuted;
        private DateTime? _muteUntilUtc;

        public event Action? AddMemberRequested;
        private string _conversationId = string.Empty;
        private string _currentUserDisplayName = string.Empty;
        private List<string> _memberIds = new();

        public frmGroupInfo()
        {
            InitializeComponent();
            BuildUI();
            ThemeRefreshHelper.Hook(this);
            // Dữ liệu nhóm sẽ được load từ bên ngoài qua LoadGroup(...)
        }

        private void BuildUI()
        {
            Text = "Group Info";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            HelpButton = false;
            ControlBox = false;
            ClientSize = new Size(FORM_WIDTH, FORM_HEIGHT);
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;

            BuildHeader();
            BuildActions();
            BuildMembers();
            BuildBottom();
        }

        private void BuildHeader()
        {
            var pnl = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(FORM_WIDTH, HEADER_HEIGHT),
                BackColor = TG.WindowBg
            };

            var btnClose = FlatIconButton("\u2715", "Segoe UI", 12f);
            btnClose.Location = new Point(FORM_WIDTH - 46, 14);
            btnClose.Click += (_, __) => Close();

            _pbAvatar = new PictureBox
            {
                Size = new Size(96, 96),
                Location = new Point((FORM_WIDTH - 96) / 2, 34),
                BackColor = Color.FromArgb(0xF4, 0xA4, 0x44),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            _pbAvatar.SizeChanged += (_, __) => ClipCircle(_pbAvatar);
            _pbAvatar.Paint += (_, __) => ClipCircle(_pbAvatar);

            _lblName = new Label
            {
                Text = string.Empty,
                Font = new Font("Segoe UI Semibold", 17f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                Size = new Size(FORM_WIDTH, 36),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 136),
                BackColor = Color.Transparent
            };

            _lblCount = new Label
            {
                Text = "2 members",
                Font = new Font("Segoe UI", 11f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Size = new Size(FORM_WIDTH, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 166),
                BackColor = Color.Transparent
            };

            pnl.Controls.AddRange(new Control[] { btnClose, _pbAvatar, _lblName, _lblCount });
            Controls.Add(pnl);
        }

        private void BuildActions()
        {
            var pnl = new Panel
            {
                Location = new Point(0, HEADER_HEIGHT),
                Size = new Size(FORM_WIDTH, ACTIONS_HEIGHT),
                BackColor = TG.WindowBg
            };

            _btnMute = BuildActionCard("\U0001F514", "Mute");
            _btnManage = BuildActionCard("\u2699\uFE0F", "Manage");
            _btnLeave = BuildActionCard("\u21AA\uFE0F", "Leave", C_DANGER);

            _btnMute.Click += (_, __) => OpenMuteNotifications();
            _btnManage.Click += (_, __) => OpenEditGroup();
            _btnLeave.Click += (_, __) => OpenLeaveGroup();

            int cardW = 112;
            int gap = 12;
            int total = cardW * 3 + gap * 2;
            int startX = (FORM_WIDTH - total) / 2;

            _btnMute.Location = new Point(startX, 12);
            _btnManage.Location = new Point(startX + cardW + gap, 12);
            _btnLeave.Location = new Point(startX + (cardW + gap) * 2, 12);

            pnl.Controls.AddRange(new Control[] { _btnMute, _btnManage, _btnLeave });
            Controls.Add(pnl);
            Controls.Add(Separator(HEADER_HEIGHT + ACTIONS_HEIGHT - 1));
        }

        private void BuildMembers()
        {
            int top = HEADER_HEIGHT + ACTIONS_HEIGHT;

            var header = new Panel
            {
                Location = new Point(0, top),
                Size = new Size(FORM_WIDTH, MEMBERS_HEADER_HEIGHT),
                BackColor = TG.WindowBg
            };

            var icon = new Label
            {
                Text = "\U0001F465",
                Font = new Font("Segoe UI Emoji", 14f),
                Size = new Size(28, 28),
                Location = new Point(SECTION_PAD, 10),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _lblMembersTitle = new Label
            {
                Text = "2 MEMBERS",
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(SECTION_PAD + 34, 14),
                BackColor = Color.Transparent
            };

            var btnAdd = FlatIconButton("\u2795", "Segoe UI Emoji", 12f);
            btnAdd.Location = new Point(FORM_WIDTH - 44, 8);
            btnAdd.Click += (_, __) => AddMemberRequested?.Invoke();

            header.Controls.AddRange(new Control[] { icon, _lblMembersTitle, btnAdd });
            Controls.Add(header);
            Controls.Add(Separator(top + MEMBERS_HEADER_HEIGHT - 1));

            _pnlList = new Panel
            {
                Location = new Point(0, top + MEMBERS_HEADER_HEIGHT),
                Size = new Size(FORM_WIDTH, FORM_HEIGHT - (top + MEMBERS_HEADER_HEIGHT) - BOTTOM_HEIGHT),
                AutoScroll = true,
                BackColor = TG.WindowBg
            };
            _pnlList.SizeChanged += (_, __) => LayoutMemberItems();
            Controls.Add(_pnlList);
        }

        private void BuildBottom()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = BOTTOM_HEIGHT,
                BackColor = TG.WindowBg
            };
            Controls.Add(pnl);
        }

        private Button BuildActionCard(string emoji, string title, Color? titleColor = null)
        {
            {
                var b = new Button
                {
                    Size = new Size(112, 70),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = TG.SidebarHover,
                    ForeColor = titleColor ?? TG.TextPrimary,
                    Font = new Font("Segoe UI Emoji", 10.8f),
                    Text = $"{emoji}\n{title}",
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                    UseCompatibleTextRendering = true,
                    TabStop = false
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = TG.SidebarHover;
                b.FlatAppearance.MouseDownBackColor = TG.SidebarHover;
                return b;
            }
        }

        private static Button FlatIconButton(string text, string fontFamily, float size)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(30, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = TG.TextPrimary,
                Font = new Font(fontFamily, size, FontStyle.Regular),
                Cursor = Cursors.Hand,
                TabStop = false,
                UseCompatibleTextRendering = true
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = TG.SidebarHover;
            b.FlatAppearance.MouseDownBackColor = TG.SidebarHover;
            return b;
        }

        private static Panel Separator(int top)
        {
            return new Panel
            {
                Location = new Point(0, top),
                Size = new Size(FORM_WIDTH, 1),
                BackColor = TG.Divider
            };
        }

        private static void ClipCircle(PictureBox pb)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, pb.Width, pb.Height);
            pb.Region = new Region(path);
        }

        private void OpenMuteNotifications()
        {
            using var f = new frmMuteNotifications(_disableSound, _notificationsMuted, _muteUntilUtc);
            if (f.ShowDialog(this) != DialogResult.OK) return;

            _disableSound = f.DisableSound;
            _notificationsMuted = f.IsMuted;
            _muteUntilUtc = f.MuteUntilUtc;
        }

        private void OpenEditGroup()
        {
            using var f = new frmEditGroup(_conversationId, _lblName.Text);
            if (f.ShowDialog(this) != DialogResult.OK) return;

            _lblName.Text = f.GroupName;
        }

        private async void OpenLeaveGroup()
        {
            var memberNames = _pnlList.Controls
                .OfType<ucGroupMemberItem>()
                .Select(item => item.DisplayName)
                .ToList();

            var memberIds = new List<string>(_memberIds);

            // Exclude current user from admin appointment list
            var currentIdx = memberNames.FindIndex(n => n.Equals(_currentUserDisplayName, StringComparison.OrdinalIgnoreCase));
            string defaultNextOwner;
            if (currentIdx >= 0)
            {
                memberNames.RemoveAt(currentIdx);
                if (currentIdx < memberIds.Count)
                    memberIds.RemoveAt(currentIdx);
                defaultNextOwner = memberNames.Count > 0 ? memberNames[0] : "Group member";
            }
            else
            {
                defaultNextOwner = _currentUserDisplayName;
            }

            using var f = new frmLeaveGroup(_lblName.Text, defaultNextOwner, memberNames, memberIds);
            if (f.ShowDialog(this) != DialogResult.OK || !f.LeaveConfirmed) return;

            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var payload = new
                {
                    newOwnerMemberId = f.AppointedAdminMemberId
                };
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var res = await http.PostAsync(
                    $"api/conversations/{_conversationId}/leave",
                    content);

                if (res.IsSuccessStatusCode)
                    Close();
                else
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show(this, $"Không thể rời nhóm: {err}", "Lỗi",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LoadGroup(string name, Image? avatar, IReadOnlyList<MemberModel> members)
        {
            _lblName.Text = name;
            _lblCount.Text = $"{members.Count} members";
            _lblMembersTitle.Text = $"{members.Count} MEMBERS";

            var oldAvatar = _pbAvatar.Image;
            _pbAvatar.Image = avatar;
            oldAvatar?.Dispose();
            _pbAvatar.BackColor = avatar == null ? Color.FromArgb(0xF4, 0xA4, 0x44) : Color.Transparent;

            _pnlList.SuspendLayout();
            DisposeOldMemberItems();
            _pnlList.Controls.Clear();
            _memberIds = members.Select(m => m.MemberId).ToList();
            int y = 0;
            foreach (var m in members)
            {
                var item = new ucGroupMemberItem
                {
                    Dock = DockStyle.None,
                    Margin = Padding.Empty,
                    Location = new Point(SECTION_PAD, y),
                    BackColor = Color.Transparent
                };
                item.DisplayName = m.Name;
                item.Status = m.Status;
                item.Role = m.Role;
                item.AvatarImage = m.Avatar;
                item.AvatarColor = m.AvatarColor;
                item.SetInitial(m.Name.Length > 0 ? m.Name.Substring(0, 1).ToUpperInvariant() : "?");
                _pnlList.Controls.Add(item);
                y += item.Height;
            }
            _pnlList.AutoScrollMinSize = new Size(0, y);
            _pnlList.ResumeLayout();
            LayoutMemberItems();
        }

        private void DisposeOldMemberItems()
        {
            foreach (Control c in _pnlList.Controls)
            {
                if (c is ucGroupMemberItem item)
                {
                    item.AvatarImage = null;
                }
                c.Dispose();
            }
        }

        private void LayoutMemberItems()
        {
            int scrollbar = SystemInformation.VerticalScrollBarWidth;
            int available = _pnlList.ClientSize.Width - (SECTION_PAD * 2) - scrollbar;
            foreach (Control c in _pnlList.Controls)
            {
                if (c is ucGroupMemberItem item)
                {
                    item.Width = available;
                    item.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                }
            }
        }

        public void SetContext(string conversationId, string currentUserDisplayName)
        {
            _conversationId = conversationId;
            _currentUserDisplayName = currentUserDisplayName;
        }
        private void OnThemeChanged()
        {
            if (InvokeRequired) { Invoke(new Action(OnThemeChanged)); return; }
            BackColor = TG.WindowBg;
            this.Invalidate(true);
            foreach (Control c in this.Controls)
                ApplyThemeRecursive(c);
        }

        private static void ApplyThemeRecursive(Control c)
        {
            if (c.BackColor != Color.Transparent &&
                c.BackColor != TG.Blue &&
                c.BackColor != TG.SidebarActive)
                c.BackColor = TG.WindowBg;
            if (c.Tag is string t && t == "secondary-text")
                c.ForeColor = TG.TextSecondary;
            c.Invalidate();
            foreach (Control child in c.Controls)
                ApplyThemeRecursive(child);
        }

    }

    public record MemberModel(string Name, string Status, string Role, Image? Avatar, Color AvatarColor, string MemberId = "");
}
