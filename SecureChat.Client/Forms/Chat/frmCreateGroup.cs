using SecureChat.Client.Components.Group;
using SecureChat.Client.Services;
using System.Drawing.Drawing2D;

namespace SecureChat.Client.Forms.Chat
{
    /*
    ╔══════════════════════════════════════════════════════════╗
    ║         HỘP THOẠI TẠO NHÓM – GIAO DIỆN 2 BƯỚC          ║
    ╠══════════════════════════════════════════════════════════╣
    ║  BƯỚC 1 – Group Name                                     ║
    ║  ┌─────────────────────────────────────────────────┐    ║
    ║  │            [📷 avatar tròn]                     │    ║
    ║  │         ___________________________             │    ║
    ║  │         Group name       (gạch dưới accent)     │    ║
    ║  │                           [Cancel]  [Next]      │    ║             
    ║  └─────────────────────────────────────────────────┘    ║
    ║                                                          ║
    ║  BƯỚC 2 – Add Members                                    ║
    ║  ┌─────────────────────────────────────────────────┐    ║
    ║  │  Add Members                     0 / 200000     │    ║
    ║  │  [chip]  [chip]  ... (scroll ngang)             │    ║
    ║  │  🔍 Search                                      │    ║
    ║  │  [●] Chú Lực  last seen...             [○]      │    ║
    ║  │  [●] Khang    last seen...             [○]      │    ║
    ║  │                           [Cancel] [Create]     │    ║
    ║  └─────────────────────────────────────────────────┘    ║
    ╚══════════════════════════════════════════════════════════╝
    */
    public partial class frmCreateGroup : Form
    {
        // ═══════════════════════════════════════════════════
        //  TELEGRAM LIGHT PALETTE
        // ═══════════════════════════════════════════════════
        private const int FORM_WIDTH = 520;
        private const int FORM_HEIGHT = 560;
        private const int BOTTOM_HEIGHT = 60;
        private const int CONTENT_HEIGHT = FORM_HEIGHT - BOTTOM_HEIGHT;
        private const int SCROLLBAR_WIDTH = 18;

        // ═══════════════════════════════════════════════════
        //  CONTROLS – Step 1
        // ═══════════════════════════════════════════════════
        private Panel _pnlStep1;
        private PictureBox _pbAvatar;
        private Panel _pnlCamOverlay;
        private TextBox _txtGroupName;
        private Panel _pnlUnderline;
        private Label _lblPlaceholder;

        // ═══════════════════════════════════════════════════
        //  CONTROLS – Step 2
        // ═══════════════════════════════════════════════════
        private Panel _pnlStep2;
        private Label _lblMemberCount;
        private FlowLayoutPanel _flpChips;
        private Panel _pnlSearch;
        private TextBox _txtSearch;
        private Panel _pnlUserList;

        // ═══════════════════════════════════════════════════
        //  CONTROLS – Bottom bar
        // ═══════════════════════════════════════════════════
        private Panel _pnlBottom;
        private Button _btnCancel;
        private Button _btnAction;   // "Next" hoặc "Create"

        // ═══════════════════════════════════════════════════
        //  STATE
        // ═══════════════════════════════════════════════════
        private int _step = 1;

        private static readonly Color[] _palette =
        {
            Color.FromArgb(0xE5,0x7E,0x25), Color.FromArgb(0x40,0x9A,0xFF),
            Color.FromArgb(0x5A,0xC7,0x67), Color.FromArgb(0xFF,0x6B,0x81),
            Color.FromArgb(0x00,0xB2,0xFF), Color.FromArgb(0xA6,0x6C,0xFF),
            Color.FromArgb(0xFF,0xAA,0x00),
        };

        private readonly List<ucUserItem> _allUsers = new();
        private readonly List<ucUserItem> _selectedUsers = new();

        // ═══════════════════════════════════════════════════
        //  RESULT PROPERTIES
        // ═══════════════════════════════════════════════════
        public string ResultGroupName { get; private set; } = "";
        public string ResultAvatarPath { get; private set; } = "";
        public List<string> ResultMembers { get; private set; } = new();
        public List<string> ResultMemberIds { get; private set; } = new();

        // ═══════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════
        public frmCreateGroup()
        {
            InitializeComponent();
            ThemeRefreshHelper.Hook(this);
            BuildUI();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowStep(1);
            _ = LoadFriendsAsync(); // Load API thật
        }

        private async Task LoadFriendsAsync()
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

                var users = list.Select(f =>
                {
                    string status;
                    if (f.Friend.ShowOnlineStatus)
                        status = SecureChat.Client.Helpers.PresenceFormatter.GetPresenceText(f.IsOnline, f.Friend.LastSeenUtc);
                    else
                        status = "offline";
                    return (f.Friend.UserID, f.Friend.DisplayName, status);
                });
                BeginInvoke(new Action(() => LoadUserList(users)));
            }
            catch { /* Giữ list rỗng nếu lỗi mạng */ }
        }

        // ───────────────────────────────────────────────────
        //  API: Nạp danh sách user từ service
        // ───────────────────────────────────────────────────
        public void LoadUserList(IEnumerable<(string UserId, string Name, string Status)> users)
        {
            _allUsers.Clear();
            _selectedUsers.Clear();
            int idx = 0;
            foreach (var (userId, name, status) in users)
            {
                var item = new ucUserItem(name, status, _palette[idx++ % _palette.Length]);
                item.UserId = userId;
                item.SelectionChanged += OnUserSelectionChanged;
                _allUsers.Add(item);
            }
            PopulateList(_allUsers);
        }

        // ═══════════════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════════════
        private void BuildUI()
        {
            Text = "New Group";
            ClientSize = new Size(FORM_WIDTH, FORM_HEIGHT);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = TG.WindowBg;
            Font = new Font("Segoe UI", 9.75f);
            DoubleBuffered = true;

            BuildStep1();
            BuildStep2();
            BuildBottom();
        }

        // ──────────────────────────────────────────────────
        //  STEP 1
        // ──────────────────────────────────────────────────
        private void BuildStep1()
        {
            _pnlStep1 = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(FORM_WIDTH, CONTENT_HEIGHT),
                BackColor = TG.WindowBg,
            };

            // "Group name" heading
            var lblHead = Label("Group name", 10f, TG.TextSecondary);
            lblHead.TextAlign = ContentAlignment.MiddleCenter;
            lblHead.Size = new Size(FORM_WIDTH, 28);
            lblHead.Location = new Point(0, 22);

            // Avatar
            const int AVA = 82;
            int avaL = (FORM_WIDTH - AVA) / 2;
            _pbAvatar = new PictureBox
            {
                Size = new Size(AVA, AVA),
                Location = new Point(avaL, 66),
                BackColor = TG.Blue,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand,
            };
            ClipCircle(_pbAvatar);

            _pnlCamOverlay = new Panel
            {
                Size = _pbAvatar.Size,
                Location = _pbAvatar.Location,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _pnlCamOverlay.Paint += DrawCameraOverlay;
            _pnlCamOverlay.Click += (_, __) => PickAvatar();
            _pbAvatar.Click += (_, __) => PickAvatar();

            // TextBox
            const int TX = 52, TY = 182;
            int TW = FORM_WIDTH - (TX * 2);
            _lblPlaceholder = Label("Group name", 13.5f, TG.TextSecondary);
            _lblPlaceholder.Location = new Point(TX, TY);
            _lblPlaceholder.Size = new Size(TW, 30);
            _lblPlaceholder.Cursor = Cursors.IBeam;
            _lblPlaceholder.Click += (_, __) => _txtGroupName.Focus();

            _txtGroupName = new TextBox
            {
                Location = new Point(TX, TY),
                Size = new Size(TW, 30),
                BackColor = TG.WindowBg,
                ForeColor = TG.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 13.5f),
                MaxLength = 128,
            };
            _txtGroupName.TextChanged += (_, __) =>
            {
                _lblPlaceholder.Visible = string.IsNullOrEmpty(_txtGroupName.Text);
                _pnlUnderline.Invalidate();
                UpdateActionButton();
            };
            _txtGroupName.GotFocus += (_, __) => _pnlUnderline.Invalidate();
            _txtGroupName.LostFocus += (_, __) => _pnlUnderline.Invalidate();

            _pnlUnderline = new Panel
            {
                Location = new Point(TX, TY + 33),
                Size = new Size(TW, 2),
                BackColor = Color.Transparent,
            };
            _pnlUnderline.Paint += (_, e) =>
                e.Graphics.Clear(_txtGroupName.Focused ? TG.TextBlue : TG.Divider);

            _pnlStep1.Controls.AddRange(new Control[]
            {
                lblHead,
                _pbAvatar,
                _lblPlaceholder,
                _txtGroupName,
                _pnlUnderline,
                _pnlCamOverlay,  // phải thêm SAU để overlay lên trên avatar
            });
            Controls.Add(_pnlStep1);
        }

        // ──────────────────────────────────────────────────
        //  STEP 2
        // ──────────────────────────────────────────────────
        private void BuildStep2()
        {
            _pnlStep2 = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(FORM_WIDTH, CONTENT_HEIGHT),
                BackColor = TG.WindowBg,
                Visible = false,
            };

            var lblTitle = Label("Add Members", 13f, TG.TextPrimary, bold: true);
            lblTitle.Location = new Point(18, 16);
            lblTitle.AutoSize = true;

            _lblMemberCount = Label("0 / 200000", 10f, TG.TextSecondary);
            _lblMemberCount.AutoSize = true;
            _lblMemberCount.Location = new Point(FORM_WIDTH - _lblMemberCount.Width - 18, 20);

            // Chips (selected users, horizontal scroll)
            _flpChips = new FlowLayoutPanel
            {
                Location = new Point(0, 52),
                Size = new Size(FORM_WIDTH, 0),
                BackColor = TG.WindowBg,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Visible = false,
                Padding = new Padding(6, 4, 6, 4),
            };

            // Search bar
            _pnlSearch = new Panel
            {
                Location = new Point(0, 52),
                Size = new Size(FORM_WIDTH, 50),
                BackColor = TG.InputBg,
            };
            var lblIcon = Label("🔍", 13f, TG.TextSecondary);
            lblIcon.Size = new Size(42, 50);
            lblIcon.Location = new Point(6, 0);
            lblIcon.TextAlign = ContentAlignment.MiddleCenter;
            lblIcon.BackColor = TG.InputBg;

            var lblPH = Label("Search", 12f, TG.TextSecondary);
            lblPH.Location = new Point(50, 13);
            lblPH.Size = new Size(200, 24);
            lblPH.BackColor = TG.InputBg;

            _txtSearch = new TextBox
            {
                Location = new Point(50, 10),
                Size = new Size(FORM_WIDTH - 62, 30),
                BackColor = TG.InputBg,
                ForeColor = TG.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 12f),
            };
            _txtSearch.TextChanged += (_, __) =>
            {
                lblPH.Visible = string.IsNullOrEmpty(_txtSearch.Text);
                FilterUsers(_txtSearch.Text);
            };
            _txtSearch.GotFocus += (_, __) => lblPH.Visible = false;
            _txtSearch.LostFocus += (_, __) => lblPH.Visible = string.IsNullOrEmpty(_txtSearch.Text);

            _pnlSearch.Controls.AddRange(new Control[] { lblIcon, lblPH, _txtSearch });

            // User list
            _pnlUserList = new Panel
            {
                Location = new Point(0, 102),
                Size = new Size(FORM_WIDTH, CONTENT_HEIGHT - 102),
                BackColor = TG.WindowBg,
                AutoScroll = true,
                Padding = new Padding(12, 0, 12, 0),
            };

            _pnlStep2.Controls.AddRange(new Control[]
            {
                lblTitle, _lblMemberCount,
                _flpChips, _pnlSearch, _pnlUserList,
            });
            Controls.Add(_pnlStep2);
        }

        // ──────────────────────────────────────────────────
        //  BOTTOM BAR
        // ──────────────────────────────────────────────────
        private void BuildBottom()
        {
            _pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = BOTTOM_HEIGHT,
                BackColor = TG.WindowBg,
            };

            var sep = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = TG.Divider };

            _btnAction = FlatBtn("Next", 84, 36);
            _btnAction.ForeColor = TG.TextBlue;
            _btnAction.Enabled = false;
            _btnAction.Click += BtnAction_Click;

            _btnCancel = FlatBtn("Cancel", 90, 36);
            _btnCancel.ForeColor = TG.TextSecondary;
            _btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            int margin = 18;
            int spacing = 12;
            _btnAction.Location = new Point(FORM_WIDTH - margin - _btnAction.Width, 12);
            _btnCancel.Location = new Point(_btnAction.Left - spacing - _btnCancel.Width, 12);

            _pnlBottom.Controls.AddRange(new Control[] { sep, _btnCancel, _btnAction });
            Controls.Add(_pnlBottom);
        }

        // ═══════════════════════════════════════════════════
        //  LOGIC – Navigation
        // ═══════════════════════════════════════════════════
        private void ShowStep(int step)
        {
            _step = step;
            _pnlStep1.Visible = step == 1;
            _pnlStep2.Visible = step == 2;

            if (step == 1)
            {
                Text = "New Group";
                _btnAction.Text = "Next";
                UpdateActionButton();
                BeginInvoke((Action)(() => _txtGroupName.Focus()));
            }
            else
            {
                Text = "Add Members";
                _btnAction.Text = "Create";
                _btnAction.Enabled = true;
                _btnAction.ForeColor = TG.TextBlue;
                RefreshCount();
                BeginInvoke((Action)(() => _txtSearch.Focus()));
            }
        }

        private void BtnAction_Click(object sender, EventArgs e)
        {
            if (_step == 1)
            {
                string name = _txtGroupName.Text.Trim();
                if (string.IsNullOrEmpty(name)) return;
                ResultGroupName = name;
                ShowStep(2);
            }
            else
            {
                ResultMembers = _selectedUsers.Select(u => u.DisplayName).ToList();
                ResultMemberIds = _selectedUsers.Select(u => u.UserId).ToList();
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void UpdateActionButton()
        {
            bool ok = !string.IsNullOrWhiteSpace(_txtGroupName?.Text);
            _btnAction.Enabled = ok;
            _btnAction.ForeColor = ok ? TG.TextBlue : TG.TextSecondary;
        }

        // ═══════════════════════════════════════════════════
        //  LOGIC – User list
        // ═══════════════════════════════════════════════════
        private void PopulateList(IEnumerable<ucUserItem> items)
        {
            _pnlUserList.SuspendLayout();
            _pnlUserList.Controls.Clear();
            int y = 0;
            int effectiveWidth = _pnlUserList.ClientSize.Width - _pnlUserList.Padding.Horizontal - SCROLLBAR_WIDTH;
            foreach (var item in items)
            {
                item.Location = new Point(_pnlUserList.Padding.Left, y);
                item.Width = effectiveWidth;
                _pnlUserList.Controls.Add(item);
                y += item.Height;
            }
            _pnlUserList.AutoScrollMinSize = new Size(0, y);
            _pnlUserList.ResumeLayout();
        }

        private void FilterUsers(string q)
        {
            var list = string.IsNullOrWhiteSpace(q)
                ? _allUsers
                : (IEnumerable<ucUserItem>)_allUsers
                    .Where(u => u.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase));
            PopulateList(list);
        }

        // ═══════════════════════════════════════════════════
        //  LOGIC – Selection chips
        // ═══════════════════════════════════════════════════
        private void OnUserSelectionChanged(ucUserItem item, bool selected)
        {
            if (selected) { if (!_selectedUsers.Contains(item)) _selectedUsers.Add(item); }
            else { _selectedUsers.Remove(item); }
            RefreshChips();
            RefreshCount();
        }

        private void RefreshChips()
        {
            _flpChips.SuspendLayout();
            _flpChips.Controls.Clear();

            foreach (var u in _selectedUsers)
            {
                var chip = new ucSelectedUser(u.DisplayName, u.AvatarColor);
                var cap = u;
                chip.RemoveClicked += () => cap.IsChecked = false;
                _flpChips.Controls.Add(chip);
            }

            _flpChips.ResumeLayout();

            bool has = _selectedUsers.Count > 0;
            int chipH = has ? 82 : 0;

            _flpChips.Visible = has;
            _flpChips.Size = new Size(FORM_WIDTH, chipH);
            _pnlSearch.Top = 52 + chipH;
            _pnlUserList.Top = _pnlSearch.Bottom;
            _pnlUserList.Height = CONTENT_HEIGHT - _pnlUserList.Top;
            _pnlUserList.PerformLayout();
        }

        private void RefreshCount()
        {
            _lblMemberCount.Text = $"{_selectedUsers.Count} / 200000";
            _lblMemberCount.Left = FORM_WIDTH - _lblMemberCount.Width - 18;
        }

        // ═══════════════════════════════════════════════════
        //  AVATAR PICKER
        // ═══════════════════════════════════════════════════
        private void PickAvatar()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh đại diện nhóm",
                Filter = "Ảnh|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tất cả|*.*",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            ResultAvatarPath = dlg.FileName;
            try
            {
                var old = _pbAvatar.Image;
                _pbAvatar.Image = Image.FromFile(ResultAvatarPath);
                old?.Dispose();
                _pbAvatar.BackColor = TG.SidebarBg;
                _pnlCamOverlay.Invalidate();
            }
            catch { /* ảnh không hợp lệ – bỏ qua */ }
        }

        // ═══════════════════════════════════════════════════
        //  PAINT – camera overlay
        // ═══════════════════════════════════════════════════
        private static void DrawCameraOverlay(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            int sz = ((Panel)sender).Width;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var b = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
                g.FillEllipse(b, 0, 0, sz, sz);

            int cx = sz / 2, cy = sz / 2;
            using var pen = new Pen(Color.White, 2f) { LineJoin = LineJoin.Round };
            RoundRect(g, pen, cx - 16, cy - 9, 32, 20, 4);   // body
            g.DrawEllipse(pen, cx - 7, cy - 7, 14, 14);       // lens
            RoundRect(g, pen, cx - 5, cy - 15, 10, 7, 2);     // top bump
            using var dot = new SolidBrush(TG.TitleBarFg);
            g.FillEllipse(dot, cx + 8, cy - 11, 4, 4);        // flash
        }

        // ═══════════════════════════════════════════════════
        //  DRAWING HELPERS
        // ═══════════════════════════════════════════════════
        private static void ClipCircle(PictureBox pb)
        {
            var p = new GraphicsPath();
            p.AddEllipse(0, 0, pb.Width, pb.Height);
            pb.Region = new Region(p);
        }

        private static void RoundRect(Graphics g, Pen pen, int x, int y, int w, int h, int r)
        {
            int d = r * 2;
            var p = new GraphicsPath();
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            g.DrawPath(pen, p);
        }

        private static Label Label(string text, float size, Color fore, bool bold = false)
            => new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size,
                                bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = fore,
                BackColor = Color.Transparent,
                AutoSize = false,
            };

        private static Button FlatBtn(string text, int w, int h)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(w, h),
                BackColor = Color.Transparent,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 10.5f),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 255, 255, 255);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(44, 255, 255, 255);
            return b;
        }
    }
}