using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SecureChat.Client.Services;

namespace SecureChat.Client
{
    // Dùng một List<ContactItem> để chứa cả bạn bè và nhóm
    // Tìm kiếm hoặc hiển thị danh sách tổng hợp dễ dàng hơn.
    public enum ContactType { Friend, Group }

    public enum FriendStatus { None, PendingIncoming, PendingOutgoing, Friend, Blocked }
    /*Trạng thái quan hệ bạn bè:
    None — chưa có quan hệ
    PendingIncoming — đang có lời mời gửi đến
    PendingOutgoing — đã gửi lời mời, chờ đối phương
    Friend — đã là bạn bè
    Blocked — đã bị chặn*/


    // Model dữ liệu đại diện cho một contact (bạn bè hoặc nhóm):
    public class ContactItem
    {
        public ContactType Type { get; set; } = ContactType.Friend;
        // Khi tạo 1 đối tượng ContactItem, mặc định là Friend
        // Khi tạo Group thì ghi đè lại

        public string DisplayName { get; set; } = string.Empty; // Tên hiển thị
        public string AvatarUrl { get; set; } = string.Empty; // URL ảnh đại diện
        public string LastSeenAt { get; set; } = string.Empty; // Thời gian hoạt động cuối

        public string UserId { get; set; } = string.Empty; // ID người dùng
        public string Username { get; set; } = string.Empty; // Tên đăng nhập (@username)
        public string Nickname { get; set; } = string.Empty; // Biệt danh (nếu có)
        public bool IsOnline { get; set; } // Đang online hay không: có chấm xanh cạnh Avatar
        public FriendStatus Status { get; set; } = FriendStatus.None; // Trạng thái bạn bè: 5 cái (None, PendingIncoming, PendingOutgoing, Friend, Blocked)

        public string ConversationId { get; set; } = string.Empty; // ID cuộc hội thoại (dùng cho Group)
        public int MemberCount { get; set; } // Số thành viên (dùng cho Group)

        public string FriendshipId { get; set; } = string.Empty; // ID của quan hệ bạn bè (dùng để Unfriend)
        public string BlockId { get; set; } = string.Empty;       // ID của bản ghi block (dùng để Unblock)
    }

    // Model cho một lời mời kết bạn:
    public class FriendRequestItem
    {
        public string RequestId { get; set; } = string.Empty; // ID người nhận
        public string SenderId { get; set; } = string.Empty; // ID người gửi
        public string RecipientId { get; set; } = string.Empty; // ID người nhận
        public string DisplayName { get; set; } = string.Empty; // Tên người gửi
        public string Username { get; set; } = string.Empty; // Username người gửi
        public string AvatarUrl { get; set; } = string.Empty; // Ảnh đại diện người gửi
        public string CreatedAt { get; set; } = string.Empty; // Thời điểm gửi
        public int MutualCount { get; set; } // Số bạn chung
        public bool IsIncoming { get; set; } // true = lời mời nhận được, false = mình đã gửi
    }

    public class frmContacts : Form
    {
        // Biến toàn cục của class (private readonly).
        // Điều này giúp các hàm khác trong class có thể truy cập trực tiếp vào hai cái bảng này bất cứ lúc nào
        // Ví dụ: để làm mới danh sách bạn bè mà không cần tìm lại nó nằm ở đâu.

        private readonly TabControl _tabs; // chứa 3 tab cha : Danh sách, Lời mời, và Tìm kiếm.
        private readonly TabPage _tabContacts, _tabRequests, _tabSearch; // 3 trang nội dung tương ứng với 3 tab cha.

        // Tab con trong "Danh sách" để chia Bạn bè / Nhóm.
        private readonly TabControl _contactSubTabs;
        private readonly Panel _pnlFriends, _pnlGroups; // Khác với tab cha Lời mời, tab con của Danh sách được khai báo toàn cục.
                                                        // Khi 1 người dùng được cập nhật hay thay đổi trạng thái thì không ảnh hưởng đến còn lại

        // ----------------------- Thiếu tab con của lời mời "Người dùng đã bị chặn"" ----------------------------------------

        /*
        _______________________________________________________
        |                                                     |
        |   [Tab 1 ][Tab 2 ][Tab 3 ]                          | <-- Tabstrip
        |_____________________________________________________|
        |                                                     |
        |                                                     |
        |                  TAB PAGE                           |
        |                                                     |
        |                                                     |
        |                                                     |
        |_____________________________________________________|
        */

        // Tab con trong "Lời mời" để chia Đã nhận / Đã gửi.
        private readonly TabControl _requestSubTabs;
        private readonly Panel _pnlBlockedUsers; // Panel mới cho tab "Đã chặn"
        private Panel _pnlIncoming = new();      // Panel lời mời đã nhận (field để refresh sau khi load API)
        private Panel _pnlSentRequests = new();  // Panel lời mời đã gửi (field để refresh sau khi load API)
        private TabPage _tpIncoming = new();
        private TabPage _tpSent = new();
        private TabPage _tpBlocked = new();
        // Panel cho 2 tab con được khai báo cục bộ
        // Trong lập trình WinForms, thay vì tìm cách chèn thêm 1 dòng vào giữa một cái Panel đang có sẵn, người ta thường chọn cách xóa sạch và vẽ lại:

        // Thanh tìm kiếm, vùng kết quả, và label gợi ý tìm kiếm.
        private readonly TelegramTextBox _tbSearch;
        private readonly Panel _pnlSearchResults;
        // Khác với Placeholder là "Bạn có thể gõ gì vào đây", Hint là giải thích "Tại sao vùng này đang trống"
        private readonly Label _lblSearchHint;

        // Đếm số những lời mời bạn đã nhận được (Incoming) (để hiển thị badge đỏ trên tab "Lời mời").
        private int _incomingCount = 0;

        // Khai báo field timer 
        private System.Windows.Forms.Timer? _searchDebounceTimer;
        public string? PendingOpenConversationId { get; private set; }

        private List<ContactItem> _friends = new List<ContactItem>();
        private List<ContactItem> _groups = new List<ContactItem>();
        private List<FriendRequestItem> _requests = new List<FriendRequestItem>();
        private List<ContactItem> _blockedUsers = new List<ContactItem>();

        public frmContacts()
        {
            // TabPage chính vừa là cái "đầu tab" (tabstrip) vừa là vùng nội dung bên trong.
            // Chuỗi "  Danh sách  " truyền vào constructor chính là thuộc tính Text của TabPage, và WinForms tự dùng Text đó để vẽ chữ lên tabstrip.
            // WinForms không tự vẽ nữa — thay vào đó hàm DrawTabItem() tự lấy tab.Text ra để vẽ.
            _tabs = new TabControl();
            _tabContacts = new TabPage("  Danh sách  ") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };
            _tabRequests = new TabPage("  Lời mời    ") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };
            _tabSearch = new TabPage("  Tìm kiếm  ") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };


            _contactSubTabs = new TabControl();
            _pnlFriends = new Panel();
            _pnlGroups = new Panel();

            _pnlBlockedUsers = new Panel();

            _requestSubTabs = new TabControl();

            _tbSearch = new TelegramTextBox();
            _pnlSearchResults = new Panel();
            _lblSearchHint = new Label();

            InitializeComponent();
            ThemeRefreshHelper.Hook(this);
            this.Load += async (s, e) => await LoadContactsFromApiAsync();
        }


        private async Task LoadContactsFromApiAsync()
        {
            _friends = new();
            _requests = new();
            _blockedUsers = new();
            _groups = new();

            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            var opts = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            // 1. Load danh sách bạn bè
            try
            {
                var res = await http.GetAsync("api/friends");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.FriendResponse>>(json, opts);
                    if (list != null)
                    {
                        _friends = list.Select(f => new ContactItem
                        {
                            Type = ContactType.Friend,
                            UserId = f.Friend.UserID,
                            DisplayName = f.Friend.DisplayName,
                            Username = f.Friend.Username,
                            IsOnline = f.IsOnline,
                            Status = FriendStatus.Friend,
                            FriendshipId = f.FriendshipID,
                        }).ToList();
                    }
                }
            }
            catch { /* Bỏ qua lỗi mạng, giữ list rỗng */ }

            // 2. Load lời mời đã nhận (received)
            try
            {
                var res = await http.GetAsync("api/friends/requests/received");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.FriendRequestResponse>>(json, opts);
                    if (list != null)
                    {
                        _requests = list.Select(r => new FriendRequestItem
                        {
                            RequestId = r.RequestID,
                            SenderId = r.Sender.UserID,
                            RecipientId = r.Recipient.UserID,
                            DisplayName = r.Sender.DisplayName,
                            Username = r.Sender.Username,
                            IsIncoming = true,
                            CreatedAt = r.CreatedAt.ToString("o"),
                        }).ToList();
                    }
                }
            }
            catch { }

            // 3. Load lời mời đã gửi (sent)
            try
            {
                var res = await http.GetAsync("api/friends/requests/sent");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.FriendRequestResponse>>(json, opts);
                    if (list != null)
                    {
                        _requests.AddRange(list.Select(r => new FriendRequestItem
                        {
                            RequestId = r.RequestID,
                            SenderId = r.Sender.UserID,
                            RecipientId = r.Recipient.UserID,
                            DisplayName = r.Recipient.DisplayName,
                            Username = r.Recipient.Username,
                            IsIncoming = false,
                            CreatedAt = r.CreatedAt.ToString("o"),
                        }));
                    }
                    else
                    {
                        var err = await res.Content.ReadAsStringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                
            }

            // 4. Load danh sách bị block
            try
            {
                var res = await http.GetAsync("api/friends/blocked");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.BlockedUserResponse>>(json, opts);
                    if (list != null)
                    {
                        _blockedUsers = list.Select(b => new ContactItem
                        {
                            Type = ContactType.Friend,
                            UserId = b.Blocked.UserID,
                            DisplayName = b.Blocked.DisplayName,
                            Username = b.Blocked.Username,
                            IsOnline = false,
                            Status = FriendStatus.Blocked,
                            BlockId = b.BlockID,
                        }).ToList();
                    }
                }
            }
            catch { }

            // 5. Load nhóm chat (conversations có type = Group)
            try
            {
                var res = await http.GetAsync("api/conversations");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.ConversationResponse>>(json, opts);
                    if (list != null)
                    {
                        _groups = list
                            .Where(c => c.Type == SecureChat.Models.ConversationType.Group)
                            .Select(c => new ContactItem
                            {
                                Type = ContactType.Group,
                                ConversationId = c.ConversationID,
                                DisplayName = c.Name ?? "Nhóm",
                                MemberCount = c.MemberCount,
                            }).ToList();
                    }
                }
            }
            catch { }

            _incomingCount = _requests.Count(r => r.IsIncoming);

            // Refresh UI trên main thread
            BeginInvoke(new Action(() =>
            {
                BuildFriendList(_friends, _pnlFriends);
                BuildGroupList(_groups, _pnlGroups);
                RefreshRequestPanels();
                LoadBlockedUsers();
                // Refresh lại search nếu user đang tìm kiếm
                if (!string.IsNullOrWhiteSpace(_tbSearch.Text) && _tbSearch.Text.Length >= 2)
                    DoSearch(_tbSearch.Text);
            }));
        }

        private void InitializeComponent()
        {
            Text = "Danh bạ";
            Size = new Size(440, 620);
            MinimumSize = new Size(360, 560);
            StartPosition = FormStartPosition.CenterParent;

            FormBorderStyle = FormBorderStyle.FixedSingle; // Viền cố định, không kéo được
            // FormBorderStyle = FormBorderStyle.Sizable; // Cho phép kéo thay đổi kích thước

            BackColor = TG.WindowBg;
            MaximizeBox = false; // chặn nút phóng to
            Font = TG.FontRegular(9.5f);

            // 1. Header (đặt trước để WinForms tính docking đúng)
            var header = new TelegramHeader
            {
                Title = "Danh bạ",
                ShowBack = true,
                Dock = DockStyle.Top
            };
            header.BackClicked += (s, e) => Close();

            // 2. Tab cha

            // Cấu hình Kích thước và Vị trí
            _tabs.Padding = new Point(0, 0); // Loại bỏ khoảng cách đệm giữa các TabPage và nội dung bên trong
                                             // giúp giao diện khít sát và gọn gàng.
            _tabs.Dock = DockStyle.Fill; // Làm cho bộ Tab này lấp đầy toàn bộ diện tích của Form hoặc Panel chứa nó.
            _tabs.ItemSize = new Size(0, 32); // Thiết lập chiều cao của thanh tiêu đề Tabstrip là 32 pixel.
                                              // Số 0 ở đầu có nghĩa là chiều rộng sẽ được tự động tính toán.
            _tabs.SizeMode = TabSizeMode.FillToRight; // Các tiêu đề Tab sẽ tự động giãn ra để dàn đều theo chiều ngang,
                                                      // lấp đầy thanh menu phía trên thay vì chỉ co cụm ở bên trái.

            // Hình thức
            _tabs.Appearance = TabAppearance.FlatButtons; // Thay đổi kiểu hiển thị từ dạng "thẻ kẹp hồ sơ" truyền thống của Windows sang dạng nút phẳng.
                                                          // Khi kết hợp với OwnerDrawFixed, nó sẽ giúp bạn dễ dàng vẽ lại màu sắc theo ý muốn.
            _tabs.Font = TG.FontRegular(9.5f);

            // Vẽ
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed; // Bật chế độ tự vẽ
            _tabs.DrawItem += DrawTabItem; // Sự kiện kích hoạt hàm DrawTabItem.
                                           // Trong hàm đó, bạn sẽ code để tô màu nền xanh khi chọn Tab, vẽ số thông báo(badge) đỏ, hoặc đổi màu chữ.
            _tabs.Selected += (s, e) => _tabs.Invalidate(); // Khi người dùng bấm chọn một Tab khác, lệnh Invalidate() yêu cầu Tab đó phải vẽ lại ngay lập tức để cập nhật trạng thái "đang được chọn"

            // Thêm 3 trang
            _tabs.TabPages.AddRange(new[] { _tabContacts, _tabRequests, _tabSearch });

            // Khởi tạo chi tiết bên trong từng trang (ví dụ: tạo các Tab con, tạo Panel, đổ dữ liệu ban đầu).
            BuildContactsTab();
            BuildRequestsTab();
            BuildSearchTab();

            // 3. Panel Content (Fill) - PHẢI ADD TRƯỚC HEADER

            var pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = TG.WindowBg
            };
            pnlContent.Controls.Add(_tabs);

            // Thứ tự Add quan trọng: Fill trước → Top sau
            Controls.Add(pnlContent);
            Controls.Add(header);
            // KHÔNG dùng BringToFront() nữa


            LoadBlockedUsers();
        }

        // Hàm xử lý resize panel. Khi panel thay đổi kích thước khi tôi kéo to nhỏ form,
        // Tất cả các row bên trong cũng được cập nhật độ rộng theo
        // row là các đoạn chat trong tabpage để không bị khoảnh trắng lúc ta kéo
        private void Pnl_UpdateRowsWidth(object? sender, EventArgs e)
        {
            if (sender is Panel pnl) // kiểm tra và ép kiểu để sài các function của 1 panel
                                     // tránh gây lỗi "văng" ứng dụng (crash) nếu lỡ tay gán sự kiện này cho một cái nút hay cái nhãn.
            {
                pnl.SuspendLayout(); // tạm dừng bố cục lại, giúp hệ thống chỉ tính toán một lần duy nhất ở cuối nếu kéo lâu lần.
                int targetWidth = pnl.ClientSize.Width;
                // int targetWidth = pnl.Width;
                foreach (Control row in pnl.Controls)
                {
                    row.Width = targetWidth;
                }
                pnl.ResumeLayout();
            }
        }

        private void DrawTabItem(object? sender, DrawItemEventArgs e)
        {
            var tab = _tabs.TabPages[e.Index];
            bool selected = e.Index == _tabs.SelectedIndex;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillRectangle(new SolidBrush(TG.WindowBg), e.Bounds);

            if (selected)
            {
                var underline = new Rectangle(e.Bounds.Left, e.Bounds.Bottom - 2, e.Bounds.Width, 2);
                e.Graphics.FillRectangle(new SolidBrush(TG.Blue), underline); // gạch xanh cho tab cha đang chọn
            }

            Color fgColor = selected ? TG.Blue : TG.TextSecondary; // Nếu chọn thì chữ màu xanh, không chọn thì chữ màu xám phụ
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(tab.Text.Trim(), TG.FontSemiBold(9.5f), new SolidBrush(fgColor), e.Bounds, sf);

            if (e.Index == 1 && _incomingCount > 0) // Chỉ vẽ trên Tab thứ 2 (Tab Lời mời).
                                                    // Chỉ vẽ khi thực sự có lời mời đang chờ. Nếu không có ai kết bạn, cái chấm đỏ sẽ tự biến mất.
            {
                int bx = e.Bounds.Right - 22; // Lấy mép phải của Tab lùi vào 22 pixel.
                // int by = e.Bounds.Top + 6; // Cách mép trên của Tab xuống 6 pixel.
                int by = e.Bounds.Top;
                var badgeRect = new Rectangle(bx, by, 18, 18); // Tạo một khung hình vuông kích thước 18x18 pixel.
                e.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(0xE2, 0x4B, 0x4A)), badgeRect); // Vẽ hình tròn màu đỏ hồng

                // sau khi vẽ xong số, hệ thống sẽ giải phóng bộ nhớ (Dispose) của đối tượng StringFormat ngay lập tức
                using var sf2 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(_incomingCount.ToString(), TG.FontSemiBold(7.5f), Brushes.White, badgeRect, sf2);
            }
        }

        // Dựng tab Danh sách gồm 2 sub-tab
        // Bạn bè → gọi BuildFriendList() đổ data vào _pnlFriends.
        // Nhóm   → gọi BuildGroupList() đổ data vào _pnlGroups.
        private void BuildContactsTab()
        {
            _contactSubTabs.Dock = DockStyle.Fill; // Làm cho thanh Tab chiếm toàn bộ diện tích của vùng chứa nó.
            _contactSubTabs.Appearance = TabAppearance.FlatButtons; // Chuyển kiểu hiển thị từ tab truyền thống sang dạng nút phẳng (giúp tùy biến giao diện dễ hơn).
            _contactSubTabs.DrawMode = TabDrawMode.OwnerDrawFixed; // Chế độ này báo cho Windows biết rằng "Tôi sẽ tự vẽ (code) giao diện các tab này" thay vì dùng giao diện mặc định của Windows.

            _contactSubTabs.Margin = new Padding(0);
            _contactSubTabs.Padding = new Point(0, 0); // Ép khoảng cách giữa nội dung và viền tab về 0

            _contactSubTabs.ItemSize = new Size(0, 30); // Đặt chiều cao của thanh tab là 30 pixel. Giá trị 0 ở chiều rộng sẽ tự động điều chỉnh theo SizeMode.
            // _contactSubTabs.ItemSize = new Size(180, 30); // Size(180, 30): Ép buộc chiều rộng mỗi Tab đúng 180 pixel.
            // _contactSubTabs.ItemSize = new Size(_contactSubTabs.Width / 2 - 2, 30);

            // _contactSubTabs.SizeMode = TabSizeMode.FillToRight; // Các Tab sẽ tự co giãn để lấp đầy toàn bộ chiều ngang của Control.
            _contactSubTabs.SizeMode = TabSizeMode.Fixed; // TabSizeMode.Fixed: Tất cả các Tab đều có kích thước bằng hệt nhau


            _contactSubTabs.Multiline = false; // Đảm bảo chỉ trên 1 dòng

            _contactSubTabs.Font = TG.FontRegular(9f);

            _contactSubTabs.DrawItem += (s, e) =>
            {
                var t = _contactSubTabs.TabPages[e.Index];
                bool sel = e.Index == _contactSubTabs.SelectedIndex;

                e.Graphics.FillRectangle(
                    sel ? new SolidBrush(TG.SidebarHover) : new SolidBrush(TG.WindowBg),
                    e.Bounds
                );

                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(
                    t.Text,
                    TG.FontSemiBold(9f),
                    new SolidBrush(sel ? TG.Blue : TG.TextSecondary),
                    e.Bounds,
                    sf
                );


            };

            // Tạo tab "Bạn bè" với nền trắng.
            var tpFriends = new TabPage("Bạn bè") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };
            // Tạo tab "Nhóm" với nền trắng.
            var tpGroups = new TabPage("Nhóm") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };
            // Thêm cả 2 tab này vào thanh điều hướng chính.
            _contactSubTabs.TabPages.AddRange(new[] { tpFriends, tpGroups });

            _pnlFriends.Dock = DockStyle.Fill;

            _pnlFriends.AutoScroll = true;

            _pnlFriends.BackColor = TG.WindowBg;
            _pnlFriends.Resize += Pnl_UpdateRowsWidth;

            _pnlGroups.Dock = DockStyle.Fill;
            _pnlGroups.AutoScroll = true;
            _pnlGroups.BackColor = TG.WindowBg;
            _pnlGroups.Resize += Pnl_UpdateRowsWidth;

            BuildFriendList(_friends, _pnlFriends);
            BuildGroupList(_groups, _pnlGroups);

            tpFriends.Controls.Add(_pnlFriends);
            tpGroups.Controls.Add(_pnlGroups);

            _tabContacts.Controls.Add(_contactSubTabs);

            _contactSubTabs.Resize += (s, e) =>
            {
                if (_contactSubTabs.TabCount > 0 && _contactSubTabs.Width > 10)
                {
                    // Trừ hẳn 6-8 pixel để tạo "khoảng thở" an toàn cho thanh Tab
                    int tabWidth = (_contactSubTabs.Width - 22) / _contactSubTabs.TabCount;

                    if (_contactSubTabs.ItemSize.Width != tabWidth && tabWidth > 0)
                    {
                        _contactSubTabs.ItemSize = new Size(tabWidth, 30);
                    }
                }
            };

        }

        private void BuildFriendList(List<ContactItem> friends, Panel pnl)
        {
            pnl.Controls.Clear();

            // Khai báo biến y để xác định tọa độ dọc. Mỗi khi thêm một người bạn mới, y sẽ tăng lên để người tiếp theo không bị đè lên người trước.
            int y = 0;

            // 1. Lấy chiều rộng hiện tại (nếu là 0 thì mới dùng 360 hoặc 440)
            int initialWidth = pnl.ClientSize.Width > 0 ? pnl.ClientSize.Width : 440;

            foreach (var item in friends)
            {
                var row = BuildFriendRow(item, initialWidth);
                row.Location = new Point(0, y);

                // 2. Ép chiều rộng của dòng bằng với Panel để tránh khoảng trắng
                row.Width = pnl.ClientSize.Width > 0 ? pnl.ClientSize.Width : initialWidth;

                // 3. Cho phép dòng tự co dãn khi người dùng kéo rộng Form
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                pnl.Controls.Add(row);
                y += 62;
            }
        }

        private void BuildGroupList(List<ContactItem> groups, Panel pnl)
        {
            pnl.Controls.Clear();
            int y = 0;
            int initialWidth = pnl.ClientSize.Width > 0 ? pnl.ClientSize.Width : 360;
            foreach (var item in groups)
            {
                var row = BuildGroupRow(item, initialWidth);
                row.Location = new Point(0, y);
                pnl.Controls.Add(row);
                y += 62;
            }
        }

        private Panel BuildFriendRow(ContactItem c, int initialWidth)
        {
            // 1. Khởi tạo Panel chính
            var pnl = new Panel
            {
                Height = 62,
                Width = initialWidth,
                BackColor = TG.WindowBg,
                Cursor = Cursors.Hand
            };

            // Bật Double Buffering để chống lag và bóng mờ (Ghosting)
            pnl.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(pnl, true, null);

            // 2. Avatar
            var avatar = new AvatarControl
            {
                Size = new Size(44, 44),
                Location = new Point(10, 9),
                ShowOnline = c.IsOnline
            };
            avatar.SetName(c.DisplayName);

            // 3. Tên và Username
            string displayText = string.IsNullOrWhiteSpace(c.Nickname) ? c.DisplayName : c.Nickname;

            var lblName = new Label
            {
                Text = displayText,
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.TextName,
                AutoSize = false,
                Height = 22,
                Location = new Point(62, 12),
                BackColor = Color.Transparent,
                AutoEllipsis = true // Tự động thêm "..." nếu tên quá dài
            };

            var lblSub = new Label
            {
                Text = "@" + c.Username,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 20,
                Location = new Point(62, 32),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
            // 4. Nút tin nhắn
            var btnMsg = new TelegramButton
            {
                Text = "💬",
                Width = 36,
                Height = 28,
                Font = new Font("Segoe UI Emoji", 11f),
                Radius = TG.RadiusSmall,

                // NormalColor = Color.Transparent,
                NormalColor = TG.WindowBg,

                TextColor = TG.Blue,
                Cursor = Cursors.Hand
            };

            // 4b. Nút ⋮ (3 chấm) — menu Hủy kết bạn / Chặn
            var btnMore = new TelegramButton
            {
                Text = "⋮",
                Width = 28,
                Height = 28,
                Font = TG.FontSemiBold(13f),
                Radius = TG.RadiusSmall,
                NormalColor = TG.WindowBg,
                TextColor = TG.TextSecondary,
                Cursor = Cursors.Hand
            };

            btnMore.Click += async (s, e) =>
            {
                var menu = new ContextMenuStrip();
                menu.Font = TG.FontRegular(9.5f);
                menu.RenderMode = ToolStripRenderMode.System;

                var itemUnfriend = new ToolStripMenuItem("  🙍  Hủy kết bạn");
                itemUnfriend.ForeColor = TG.TextName;
                itemUnfriend.Click += async (_, __) =>
                {
                    var confirm = MessageBox.Show(
                        $"Hủy kết bạn với {c.DisplayName}?",
                        "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;

                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var res = await http.DeleteAsync($"api/friends/{c.FriendshipId}");
                    if (res.IsSuccessStatusCode)
                        await LoadContactsFromApiAsync();
                    else
                        MessageBox.Show("Hủy kết bạn thất bại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                var itemBlock = new ToolStripMenuItem("  🚫  Chặn người này");
                itemBlock.ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A);
                itemBlock.Click += async (_, __) =>
                {
                    var confirm = MessageBox.Show(
                        $"Chặn {c.DisplayName}? Người này sẽ không thể nhắn tin cho bạn.",
                        "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (confirm != DialogResult.Yes) return;

                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var body = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(new { BlockedID = c.UserId }),
                        System.Text.Encoding.UTF8, "application/json");
                    var res = await http.PostAsync("api/friends/blocked", body);
                    if (res.IsSuccessStatusCode)
                        await LoadContactsFromApiAsync();
                    else
                        MessageBox.Show("Chặn người dùng thất bại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                menu.Items.Add(itemUnfriend);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(itemBlock);
                menu.Show(btnMore, new Point(0, btnMore.Height));
            };

            btnMsg.Click += async (s, e) =>
            {
                btnMsg.Enabled = false;
                try
                {
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var opts = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };

                    // Lấy current user ID
                    var meRes = await http.GetAsync("api/users/me");
                    if (!meRes.IsSuccessStatusCode) { btnMsg.Enabled = true; return; }
                    var me = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.UserResponse>(
                        await meRes.Content.ReadAsStringAsync(), opts);
                    if (me == null) { btnMsg.Enabled = true; return; }

                    // Tạo AES conversation key và mã hóa cho mỗi member
                    var (convKey, convIv) = SecureChat.Shared.Security.AesEncryption.GenerateKeyAndIv();
                    string encryptedKeyForMe, encryptedKeyForOther;

                    try
                    {
                        // Lấy public key của chính mình
                        var (pub, _) = SecureChat.Shared.Security.KeyManager.GetKeyPair();
                        if (string.IsNullOrWhiteSpace(pub))
                        {
                            var newKeys = SecureChat.Shared.Security.RSAEncryption.GenerateKeyPair();
                            SecureChat.Shared.Security.KeyManager.SetKeyPair(newKeys.publicKeyPem, newKeys.privateKeyPem);
                            pub = newKeys.publicKeyPem;
                        }

                        // Mã hóa AES key với public key của mình
                        byte[] encKeyMe = SecureChat.Shared.Security.RSAEncryption.Encrypt(convKey, pub);
                        encryptedKeyForMe = Convert.ToBase64String(encKeyMe);

                        // Lấy public key của người kia
                        var otherUserRes = await http.GetAsync($"api/users/{c.UserId}");
                        if (!otherUserRes.IsSuccessStatusCode)
                        {
                            btnMsg.Enabled = true;
                            MessageBox.Show("Không thể lấy thông tin người dùng.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        var otherUser = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.UserResponse>(
                            await otherUserRes.Content.ReadAsStringAsync(), opts);
                        if (otherUser == null || string.IsNullOrWhiteSpace(otherUser.PublicKey))
                        {
                            btnMsg.Enabled = true;
                            MessageBox.Show("Người dùng chưa thiết lập khóa mã hóa.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        byte[] encKeyOther = SecureChat.Shared.Security.RSAEncryption.Encrypt(convKey, otherUser.PublicKey);
                        encryptedKeyForOther = Convert.ToBase64String(encKeyOther);
                    }
                    catch (Exception ex)
                    {
                        btnMsg.Enabled = true;
                        MessageBox.Show($"Không thể tạo khóa mã hóa: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var reqBody = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Type = 0, // ConversationType.Direct
                        Name = (string?)null,
                        AvatarUrl = (string?)null,
                        Members = new[]
                        {
                            new { UserID = me.UserID, EncryptedKey = encryptedKeyForMe },
                            new { UserID = c.UserId,  EncryptedKey = encryptedKeyForOther }
                        }
                    });

                    
                    var res = await http.PostAsync("api/conversations",
                        new StringContent(reqBody, System.Text.Encoding.UTF8, "application/json"));

                    /*
                    if (!res.IsSuccessStatusCode) { btnMsg.Enabled = true; return; }

                    var conv = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.ConversationResponse>(
                        await res.Content.ReadAsStringAsync(), opts);
                    if (conv == null) { btnMsg.Enabled = true; return; }
                    */

                    if (!res.IsSuccessStatusCode)
                    {
                        var errBody = await res.Content.ReadAsStringAsync();
                        btnMsg.Enabled = true;
                        MessageBox.Show($"Lỗi {(int)res.StatusCode}: {errBody}", "Debug");
                        return;
                    }

                    var resBody = await res.Content.ReadAsStringAsync();
                    var conv = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.ConversationResponse>(resBody, opts);
                    if (conv == null)
                    {
                        btnMsg.Enabled = true;
                        MessageBox.Show($"conv null, body: {resBody}", "Debug");
                        return;
                    }




                    PendingOpenConversationId = conv.ConversationID;
                    Close();
                }
                catch (Exception ex)
                {
                    btnMsg.Enabled = true;
                    MessageBox.Show($"Không thể mở cuộc trò chuyện:\n{ex.Message}",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            int initRightMargin = 8;
            btnMore.Location = new Point(
                initialWidth - btnMore.Width - initRightMargin,
                (62 - btnMore.Height) / 2);
            btnMsg.Location = new Point(
                btnMore.Left - btnMsg.Width - 4,
                (62 - btnMsg.Height) / 2);
            int initTextWidth = btnMsg.Left - 62 - 10;
            lblName.Width = Math.Max(0, initTextWidth);
            lblSub.Width = Math.Max(0, initTextWidth);

            // Xử lý sự kiện click cho toàn bộ dòng
            pnl.Click += (s, e) =>
            {
                if (c.Status == FriendStatus.Friend || c.Status == FriendStatus.None)
                {
                    btnMsg.PerformClick();
                }
            };
            avatar.Click += (s, e) =>
            {
                if (c.Status == FriendStatus.Friend || c.Status == FriendStatus.None)
                {
                    btnMsg.PerformClick();
                }
            };
            lblName.Click += (s, e) =>
            {
                if (c.Status == FriendStatus.Friend || c.Status == FriendStatus.None)
                {
                    btnMsg.PerformClick();
                }
            };
            lblSub.Click += (s, e) =>
            {
                if (c.Status == FriendStatus.Friend || c.Status == FriendStatus.None)
                {
                    btnMsg.PerformClick();
                }
            };
            pnl.Cursor = Cursors.Hand;


            // 5. Thêm các control vào panel
            pnl.Controls.AddRange(new Control[] { avatar, lblName, lblSub, btnMsg, btnMore });
            // 6. Vẽ đường kẻ chia hàng (Divider)
            pnl.Paint += (s, e) =>
            {
                using (var pen = new Pen(TG.DividerLight))
                {
                    // Vẽ đường kẻ từ vị trí 62 (thẳng hàng với text)
                    e.Graphics.DrawLine(pen, 62, pnl.Height - 1, pnl.Width, pnl.Height - 1);
                }
            };

            // 7. Xử lý co giãn (Responsive)
            pnl.Resize += (s, e) =>
            {
                int rightMargin = 8;
                btnMore.Left = pnl.Width - btnMore.Width - rightMargin;
                btnMore.Top  = (62 - btnMore.Height) / 2;
                btnMsg.Left  = btnMore.Left - btnMsg.Width - 4;
                btnMsg.Top   = (62 - btnMsg.Height) / 2;

                int textWidth = btnMsg.Left - lblName.Left - 10;
                lblName.Width = Math.Max(0, textWidth);
                lblSub.Width  = Math.Max(0, textWidth);

                pnl.Invalidate(); // Vẽ lại đường kẻ divider khi co giãn
            };

            // 8. Hiệu ứng Hover (Đổi màu đồng bộ)
            // Hàm dùng chung để đổi màu
            Action<Color> setHoverColor = (color) =>
            {
                pnl.BackColor = color;
                // Buộc các label vẽ lại trên nền mới để tránh rác
                lblName.Invalidate();
                lblSub.Invalidate();
            };

            foreach (Control ctrl in pnl.Controls)
            {
                // Khi chuột đi vào bất kỳ control con nào, panel vẫn giữ màu hover
                ctrl.MouseEnter += (s, e) => setHoverColor(TG.SidebarHover);
                ctrl.MouseLeave += (s, e) =>
                {
                    // Chỉ trả về màu trắng nếu chuột thực sự rời khỏi vùng của Panel
                    if (!pnl.ClientRectangle.Contains(pnl.PointToClient(Control.MousePosition)))
                        setHoverColor(TG.WindowBg);
                };
            }

            // pnl.MouseEnter += (s, e) => setHoverColor(TG.SidebarHover);
            // pnl.MouseLeave += (s, e) => setHoverColor(TG.WindowBg);

            // Trong hàm BuildFriendRow, đoạn xử lý Hover:
            pnl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
            pnl.MouseLeave += (s, e) => pnl.BackColor = TG.WindowBg;

            return pnl;
        }


        private Panel BuildGroupRow(ContactItem c, int initialWidth)
        {
            var pnl = new Panel { Height = 62, Width = initialWidth, BackColor = TG.WindowBg, Cursor = Cursors.Hand };

            var avatar = new AvatarControl { Size = new Size(44, 44), Location = new Point(10, 9), ShowOnline = false };
            avatar.SetName(c.DisplayName);

            var lblName = new Label
            {
                Text = c.DisplayName,
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.TextName,
                AutoSize = false,
                Height = 20,
                Location = new Point(62, 12),
                Width = initialWidth - 80,
                BackColor = Color.Transparent,
            };

            var lblSub = new Label
            {
                Text = $"{c.MemberCount} thành viên",
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 18,
                Location = new Point(62, 32),
                Width = initialWidth - 112,
                BackColor = Color.Transparent,
            };

            var btnMsg = new TelegramButton
            {
                Text = "💬",
                Width = 36,
                Height = 28,
                Font = new Font("Segoe UI Emoji", 11f),
                Radius = TG.RadiusSmall,
                NormalColor = Color.Transparent,
                TextColor = TG.Blue,
                Location = new Point(initialWidth - 46, 17),
            };
            btnMsg.Click += async (s, e) =>
            {
                btnMsg.Enabled = false;
                try
                {
                    PendingOpenConversationId = c.ConversationId;
                    Close();
                }
                catch (Exception ex)
                {
                    btnMsg.Enabled = true;
                    MessageBox.Show($"Không thể mở cuộc trò chuyện:\n{ex.Message}",
                        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            // Khi click vào avatar hoặc tên người dùng, tự động kích hoạt nút tin nhắn
            avatar.Click += (s, e) =>
            {
                btnMsg.PerformClick();
            };
            lblName.Click += (s, e) =>
            {
                btnMsg.PerformClick();
            };
            lblSub.Click += (s, e) =>
            {
                btnMsg.PerformClick();
            };
            pnl.Controls.AddRange(new Control[] { avatar, lblName, lblSub, btnMsg });
            pnl.Paint += (s, e) => e.Graphics.DrawLine(new Pen(TG.DividerLight), 62, 61, pnl.Width, 61);

            EventHandler resizeHandler = (s, e) =>
            {
                const int rightMargin = 10;
                int newBtnLeft = Math.Max(62 + 80, pnl.ClientSize.Width - btnMsg.Width - rightMargin);
                btnMsg.Left = newBtnLeft;
                int available = btnMsg.Left - lblName.Left - 12;
                lblName.Width = Math.Max(80, available);
                lblSub.Width = lblName.Width;

                pnl.Refresh(); // 🔥 XÓA RÁC BÓNG MỜ
            };

            pnl.Resize += resizeHandler;
            pnl.HandleCreated += resizeHandler; // Gọi sau khi panel có kích thước thật

            foreach (Control ctrl in pnl.Controls)
            {
                ctrl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
                ctrl.MouseLeave += (s, e) =>
                {
                    if (!pnl.ClientRectangle.Contains(pnl.PointToClient(Control.MousePosition)))
                        pnl.BackColor = TG.WindowBg;
                };
            }
            pnl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
            pnl.MouseLeave += (s, e) => pnl.BackColor = TG.WindowBg;

            return pnl;
        }

        private void BuildRequestsTab()
        {
            _requestSubTabs.Dock = DockStyle.Fill;
            _requestSubTabs.Appearance = TabAppearance.FlatButtons;
            _requestSubTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _requestSubTabs.Margin = new Padding(0);  // ✅ FIX: _contactSubTabs → _requestSubTabs
            _requestSubTabs.Padding = new Point(0, 0);  // ✅ FIX: _contactSubTabs → _requestSubTabs
            _requestSubTabs.ItemSize = new Size(0, 30);
            _requestSubTabs.SizeMode = TabSizeMode.Fixed;


            _requestSubTabs.Multiline = false;  // ✅ FIX: _contactSubTabs → _requestSubTabs
            _requestSubTabs.Font = TG.FontRegular(9f);
            _requestSubTabs.DrawItem += (s, e) =>
            {
                var t = _requestSubTabs.TabPages[e.Index];
                bool sel = e.Index == _requestSubTabs.SelectedIndex;
                e.Graphics.FillRectangle(sel ? new SolidBrush(TG.SidebarHover) : new SolidBrush(TG.WindowBg), e.Bounds);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(t.Text, TG.FontSemiBold(9f), new SolidBrush(sel ? TG.Blue : TG.TextSecondary), e.Bounds, sf);
            };

            int incomingCount = _requests.FindAll(r => r.IsIncoming).Count;
            int outgoingCount = _requests.FindAll(r => !r.IsIncoming).Count;
            int blockedCount = _blockedUsers.Count;  // ✅ FIX: Thêm đếm số blocked users

            _tpIncoming = new TabPage($"Đã nhận ({incomingCount})") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };
            _tpSent = new TabPage($"Đã gửi ({outgoingCount})") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };
            _tpBlocked = new TabPage($"Đã chặn ({blockedCount})") { BackColor = TG.WindowBg, UseVisualStyleBackColor = false };

            _requestSubTabs.TabPages.AddRange(new[] { _tpIncoming, _tpSent, _tpBlocked });

            // Gán Panel cho tab "Đã chặn"
            _pnlBlockedUsers.Dock = DockStyle.Fill;
            _pnlBlockedUsers.AutoScroll = true;
            _pnlBlockedUsers.Resize += Pnl_UpdateRowsWidth;
            _tpBlocked.Controls.Add(_pnlBlockedUsers);
            LoadBlockedUsers();  // ✅ FIX: Gọi hàm load dữ liệu

            // ============ TAB "Đã nhận" ============
            _pnlIncoming = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = TG.WindowBg };
            _pnlIncoming.Resize += Pnl_UpdateRowsWidth;
            int y = 0;
            int initInWidth = _pnlIncoming.ClientSize.Width > 0 ? _pnlIncoming.ClientSize.Width : 360;
            foreach (var req in _requests.FindAll(r => r.IsIncoming))
            {
                var row = BuildRequestRow(req, true, initInWidth);
                row.Location = new Point(0, y);
                _pnlIncoming.Controls.Add(row);
                y += 86;
            }
            _tpIncoming.Controls.Add(_pnlIncoming);

            // ============ TAB "Đã gửi" ============
            _pnlSentRequests = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = TG.WindowBg };
            _pnlSentRequests.Resize += Pnl_UpdateRowsWidth;
            y = 0;
            int initSentWidth = _pnlSentRequests.ClientSize.Width > 0 ? _pnlSentRequests.ClientSize.Width : 360;
            foreach (var req in _requests.FindAll(r => !r.IsIncoming))
            {
                var row = BuildRequestRow(req, false, initSentWidth);
                row.Location = new Point(0, y);
                _pnlSentRequests.Controls.Add(row);
                y += 86;
            }
            _tpSent.Controls.Add(_pnlSentRequests);

            _tabRequests.Controls.Add(_requestSubTabs);

            // ============ Tab Size Calculation ============
            _requestSubTabs.Resize += (s, e) =>
            {
                if (_requestSubTabs.TabCount > 0 && _requestSubTabs.Width > 10)
                {
                    // ✅ FIX: Tăng margin từ 22 lên 30 (vì có 3 tabs thay vì 2)
                    int tabWidth = (_requestSubTabs.Width - 33) / _requestSubTabs.TabCount;
                    if (_requestSubTabs.ItemSize.Width != tabWidth && tabWidth > 0)
                    {
                        _requestSubTabs.ItemSize = new Size(tabWidth, 30);
                    }
                }
            };
        }

        private Panel BuildRequestRow(FriendRequestItem req, bool isIncoming, int initialWidth)
        {
            var pnl = new Panel { Height = 86, Width = initialWidth, BackColor = TG.WindowBg };

            var avatar = new AvatarControl { Size = new Size(44, 44), Location = new Point(12, 10) };
            avatar.SetName(req.DisplayName);

            var lblName = new Label
            {
                Text = req.DisplayName,
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.TextName,
                AutoSize = false,
                Height = 20,
                Location = new Point(64, 12),
                Width = initialWidth - 76,
                BackColor = Color.Transparent,
            };

            var lblSub = new Label
            {
                Text = req.MutualCount > 0 ? $"Bạn bè chung: {req.MutualCount}" : "@" + req.Username,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 22,
                Location = new Point(64, 32),
                Width = initialWidth - 76,
                BackColor = Color.Transparent,
            };

            if (isIncoming)
            {
                int btnWidth = (initialWidth - 88) / 2;
                var btnAccept = new TelegramButton { Text = "Chấp nhận", Height = 28, Radius = TG.RadiusSmall, Font = TG.FontRegular(8.5f), Location = new Point(64, 54), Width = btnWidth };
                var btnDecline = new TelegramButton { Text = "Từ chối", Height = 28, Radius = TG.RadiusSmall, Font = TG.FontRegular(8.5f), IsOutlined = true, Location = new Point(64 + btnWidth + 8, 54), Width = btnWidth };

                btnAccept.Click += async (s, e) =>
                {
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var res = await http.PutAsync($"api/friends/requests/{req.RequestId}/accept", null);
                    if (res.IsSuccessStatusCode)
                    {
                        _incomingCount--;
                        _tabs.Refresh();
                        await LoadContactsFromApiAsync();
                    }
                    else MessageBox.Show("Thao tác thất bại.", "Lỗi");
                };
                btnDecline.Click += async (s, e) =>
                {
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var res = await http.PutAsync($"api/friends/requests/{req.RequestId}/decline", null);
                    if (res.IsSuccessStatusCode)
                        await LoadContactsFromApiAsync();
                    else MessageBox.Show("Thao tác thất bại.", "Lỗi");
                };

                pnl.Controls.AddRange(new Control[] { avatar, lblName, lblSub, btnAccept, btnDecline });

                pnl.Resize += (s, e) =>
                {
                    lblName.Width = pnl.ClientSize.Width - 76;
                    lblSub.Width = pnl.ClientSize.Width - 76;
                    int bw = (pnl.ClientSize.Width - 88) / 2;
                    btnAccept.Width = bw;
                    btnDecline.Left = 64 + bw + 8;
                    btnDecline.Width = bw;
                    pnl.Refresh(); // 🔥 XÓA RÁC BÓNG MỜ
                };
            }
            else
            {
                var btnCancel = new TelegramButton { Text = "Hủy lời mời", Height = 28, Radius = TG.RadiusSmall, Font = TG.FontRegular(8.5f), IsOutlined = true, Location = new Point(64, 54), Width = initialWidth - 76 };
                btnCancel.Click += async (s, e) =>
                {
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var res = await http.DeleteAsync($"api/friends/requests/{req.RequestId}");
                    if (res.IsSuccessStatusCode)
                        await LoadContactsFromApiAsync();
                    else MessageBox.Show("Thao tác thất bại.", "Lỗi");
                };

                pnl.Controls.AddRange(new Control[] { avatar, lblName, lblSub, btnCancel });

                pnl.Resize += (s, e) =>
                {
                    lblName.Width = pnl.ClientSize.Width - 76;
                    lblSub.Width = pnl.ClientSize.Width - 76;
                    btnCancel.Width = pnl.ClientSize.Width - 76;
                    pnl.Refresh(); // 🔥 XÓA RÁC BÓNG MỜ
                };
            }

            pnl.Paint += (s, e) => e.Graphics.DrawLine(new Pen(TG.DividerLight), 0, 85, pnl.Width, 85);
            return pnl;
        }

        private static void RemoveRequest(Panel row, bool isAccepted)
        {
            string msg = isAccepted ? "Đã chấp nhận lời mời kết bạn!" : "Đã từ chối lời mời.";
            MessageBox.Show(msg, "SecureChat", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Control container = row.Parent;
            int rowHeight = row.Height;
            int rowTop = row.Top;

            // Xóa hàng hiện tại
            container.Controls.Remove(row);
            row.Dispose();

            // Duyệt qua tất cả các hàng còn lại trong container
            foreach (Control c in container.Controls)
            {
                // Nếu hàng nào nằm dưới hàng vừa xóa, kéo nó lên
                if (c.Top > rowTop)
                {
                    c.Top -= rowHeight;
                }
            }
        }


        private void BuildSearchTab()
        {
            var pnlSearch = new Panel { Height = 52, Dock = DockStyle.Top, BackColor = TG.WindowBg, Padding = new Padding(12, 8, 12, 6) };

            _tbSearch.Height = 36;
            _tbSearch.Dock = DockStyle.Fill;
            _tbSearch.SetPlaceholder("🔍  Tìm theo tên hoặc @username...");
            _tbSearch.TextChanged += (s, e) =>
            {
                _searchDebounceTimer?.Stop();
                if (_searchDebounceTimer == null)
                {
                    _searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 400 };
                    _searchDebounceTimer.Tick += (_, __) =>
                    {
                        _searchDebounceTimer.Stop();
                        DoSearch(_tbSearch.Text);
                    };
                }
                _searchDebounceTimer.Start();
            };
            pnlSearch.Controls.Add(_tbSearch);

            _lblSearchHint.Text = "Nhập tên hoặc username để tìm kiếm";
            _lblSearchHint.Font = TG.FontRegular(9.5f);
            _lblSearchHint.ForeColor = TG.TextHint;
            _lblSearchHint.AutoSize = false;
            _lblSearchHint.BackColor = Color.Transparent;
            _lblSearchHint.TextAlign = ContentAlignment.MiddleCenter;
            _lblSearchHint.Dock = DockStyle.Fill;

            _pnlSearchResults.Dock = DockStyle.Fill;
            _pnlSearchResults.AutoScroll = true;
            _pnlSearchResults.BackColor = TG.WindowBg;
            _pnlSearchResults.Resize += Pnl_UpdateRowsWidth;
            _pnlSearchResults.Controls.Add(_lblSearchHint);

            _tabSearch.Controls.AddRange(new Control[] { _pnlSearchResults, pnlSearch });
        }

        private async void DoSearch(string query)
        {
            _pnlSearchResults.Controls.Clear();

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                _pnlSearchResults.Controls.Add(_lblSearchHint);
                return;
            }

            // Hiển thị trạng thái đang tìm
            var lblLoading = new Label
            {
                Text = "Đang tìm kiếm...",
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 40,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
            };
            _pnlSearchResults.Controls.Add(lblLoading);

            List<ContactItem> results = new();

            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var encoded = Uri.EscapeDataString(query);
                var res = await http.GetAsync($"api/users/search?q={encoded}");

                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var users = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.UserResponse>>(json, opts);
                    if (users != null)
                    {
                        // Xác định trạng thái quan hệ với từng user trong kết quả
                        results = users.Select(u =>
                        {
                            FriendStatus status = FriendStatus.None;
                            if (_friends.Any(f => f.UserId == u.UserID))
                                status = FriendStatus.Friend;
                            else if (_requests.Any(r => r.RecipientId == u.UserID && !r.IsIncoming))
                                status = FriendStatus.PendingOutgoing;
                            else if (_requests.Any(r => r.SenderId == u.UserID && r.IsIncoming))
                                status = FriendStatus.PendingIncoming;
                            else if (_blockedUsers.Any(b => b.UserId == u.UserID))
                                status = FriendStatus.Blocked;

                            return new ContactItem
                            {
                                Type = ContactType.Friend,
                                UserId = u.UserID,
                                DisplayName = u.DisplayName,
                                Username = u.Username,
                                IsOnline = u.IsOnline,
                                Status = status,
                            };
                        }).ToList();
                    }
                }
            }
            catch { /* Bỏ qua lỗi mạng */ }

            // Rebuild kết quả trên UI
            _pnlSearchResults.Controls.Clear();

            int y = 0;
            var lblHdr = new Label
            {
                Text = results.Count > 0
                    ? $"Kết quả cho \"{query}\""
                    : $"Không tìm thấy kết quả cho \"{query}\"",
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 24,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 4, 0, 0),
                Location = new Point(0, y),
                Width = _pnlSearchResults.ClientSize.Width,
            };
            _pnlSearchResults.Controls.Add(lblHdr);
            y += 26;

            int initialWidth = _pnlSearchResults.ClientSize.Width > 0 ? _pnlSearchResults.ClientSize.Width : 360;
            foreach (var r in results)
            {
                var row = BuildSearchRow(r, initialWidth);
                row.Location = new Point(0, y);
                _pnlSearchResults.Controls.Add(row);
                y += 60;
            }
        }

        private Panel BuildSearchRow(ContactItem c, int initialWidth)
        {
            var pnl = new Panel { Height = 60, Width = initialWidth, BackColor = TG.WindowBg, Cursor = Cursors.Hand };

            var avatar = new AvatarControl { Size = new Size(40, 40), Location = new Point(10, 10), ShowOnline = c.IsOnline };
            avatar.SetName(c.DisplayName);

            var lblName = new Label
            {
                Text = c.DisplayName,
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.TextName,
                AutoSize = false,
                Height = 20,
                Location = new Point(58, 10),
                Width = initialWidth - 158,
                BackColor = Color.Transparent,
            };

            var lblUser = new Label
            {
                Text = "@" + c.Username,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextBlue,
                AutoSize = false,
                Height = 20,
                Location = new Point(58, 30),
                Width = initialWidth - 158,
                BackColor = Color.Transparent,
            };

            Control statusCtrl;
            int statusX = initialWidth - 92;

            switch (c.Status)
            {
                case FriendStatus.Friend:
                    statusCtrl = new Label { Text = "✓ Bạn bè", Font = TG.FontRegular(8f), ForeColor = TG.AccentGreen, BackColor = Color.FromArgb(40, TG.AccentGreen.R, TG.AccentGreen.G, TG.AccentGreen.B), AutoSize = false, Height = 24, Width = 80, Location = new Point(statusX, 16), TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(6, 0, 6, 0), BorderStyle = BorderStyle.FixedSingle };
                    break;
                case FriendStatus.PendingOutgoing:
                    statusCtrl = new Label { Text = "Đã gửi", Font = TG.FontRegular(8f), ForeColor = TG.TextSecondary, BackColor = Color.FromArgb(40, TG.TextSecondary.R, TG.TextSecondary.G, TG.TextSecondary.B), AutoSize = false, Height = 24, Width = 80, Location = new Point(statusX, 16), TextAlign = ContentAlignment.MiddleCenter };
                    break;
                case FriendStatus.PendingIncoming:
                    statusCtrl = new Label { Text = "Chờ duyệt", Font = TG.FontRegular(8f), ForeColor = TG.Blue, BackColor = Color.FromArgb(40, TG.Blue.R, TG.Blue.G, TG.Blue.B), AutoSize = false, Height = 24, Width = 80, Location = new Point(statusX, 16), TextAlign = ContentAlignment.MiddleCenter };
                    break;
                case FriendStatus.Blocked:
                    statusCtrl = new Label { Text = "Đã chặn", Font = TG.FontRegular(8f), ForeColor = TG.TextSecondary, BackColor = TG.Divider, AutoSize = false, Height = 24, Width = 80, Location = new Point(statusX, 16), TextAlign = ContentAlignment.MiddleCenter };
                    break;
                default:
                    var btn = new TelegramButton { Text = "+ Kết bạn", Height = 28, Width = 80, Radius = TG.RadiusSmall, Font = TG.FontRegular(8.5f), Location = new Point(statusX, 16) };
                    btn.Click += async (s, e) =>
                    {
                        btn.Enabled = false;
                        btn.Text = "Đang gửi...";
                        try
                        {
                            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                            var body = new StringContent(
                                System.Text.Json.JsonSerializer.Serialize(new { RecipientID = c.UserId }),
                                System.Text.Encoding.UTF8, "application/json");
                            var res = await http.PostAsync("api/friends/requests", body);
                            if (res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.Conflict)
                            {
                                c.Status = FriendStatus.PendingOutgoing;
                                btn.Text = "Đã gửi";
                                btn.ForeColor = System.Drawing.Color.FromArgb(0xE6, 0x5C, 0x00);
                                await LoadContactsFromApiAsync();
                            }
                            else
                            {
                                var errorBody = await res.Content.ReadAsStringAsync();
                                btn.Text = "+ Kết bạn";
                                btn.Enabled = true;
                                MessageBox.Show($"Lỗi {(int)res.StatusCode}: {errorBody}", "Gửi thất bại");
                            }
                        }
                        catch
                        {
                            btn.Text = "+ Kết bạn";
                            btn.Enabled = true;
                        }
                    };
                    statusCtrl = btn;
                    break;
            }

            EventHandler resizeHandler = (s, e) =>
            {
                const int marginRight = 12;
                int statusW = statusCtrl.Width;
                int desiredLeft = pnl.ClientSize.Width - statusW - marginRight;
                int minNameWidth = 80;
                int minLeftForStatus = lblName.Left + minNameWidth + 12;
                statusCtrl.Left = Math.Max(minLeftForStatus, desiredLeft);

                int availableForName = statusCtrl.Left - lblName.Left - 12;
                lblName.Width = Math.Max(minNameWidth, availableForName);
                lblUser.Width = lblName.Width;

                pnl.Refresh(); // 🔥 XÓA RÁC BÓNG MỜ
            };

            pnl.Resize += resizeHandler;
            resizeHandler(pnl, EventArgs.Empty); // Gọi ngay 1 lần thay vì BeginInvoke

            pnl.Controls.AddRange(new Control[] { avatar, lblName, lblUser, statusCtrl });
            pnl.Paint += (s, e) => e.Graphics.DrawLine(new Pen(TG.DividerLight), 58, 59, pnl.Width, 59);

            foreach (Control ctrl in pnl.Controls)
            {
                ctrl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
                ctrl.MouseLeave += (s, e) =>
                {
                    if (!pnl.ClientRectangle.Contains(pnl.PointToClient(Control.MousePosition)))
                        pnl.BackColor = TG.WindowBg;
                };
            }
            pnl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
            pnl.MouseLeave += (s, e) => pnl.BackColor = TG.WindowBg;

            return pnl;
        }

        private void RefreshRequestPanels()
        {
            // Rebuild panel lời mời đã nhận
            _pnlIncoming.Controls.Clear();
            int y = 0;
            int w = _pnlIncoming.ClientSize.Width > 0 ? _pnlIncoming.ClientSize.Width : 360;
            foreach (var req in _requests.FindAll(r => r.IsIncoming))
            {
                var row = BuildRequestRow(req, true, w);
                row.Location = new Point(0, y);
                _pnlIncoming.Controls.Add(row);
                y += 86;
            }

            // Rebuild panel lời mời đã gửi
            _pnlSentRequests.Controls.Clear();
            y = 0;
            w = _pnlSentRequests.ClientSize.Width > 0 ? _pnlSentRequests.ClientSize.Width : 360;
            foreach (var req in _requests.FindAll(r => !r.IsIncoming))
            {
                var row = BuildRequestRow(req, false, w);
                row.Location = new Point(0, y);
                _pnlSentRequests.Controls.Add(row);
                y += 86;
            }
            _tpIncoming.Text = $"Đã nhận ({_requests.Count(r => r.IsIncoming)})";
            _tpSent.Text = $"Đã gửi ({_requests.Count(r => !r.IsIncoming)})";
            _requestSubTabs.Invalidate();

        }

        private void LoadBlockedUsers()
        {
            _pnlBlockedUsers.Controls.Clear();

            // Cập nhật số đếm trên tab
            _tpBlocked.Text = $"Đã chặn ({_blockedUsers.Count})";
            _requestSubTabs.Invalidate();

            if (_blockedUsers.Count == 0)
            {
                var lbl = new Label
                {
                    Text = "Chưa chặn ai",
                    Font = TG.FontRegular(9f),
                    ForeColor = TG.TextSecondary,
                    AutoSize = false,
                    Height = 40,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                _pnlBlockedUsers.Controls.Add(lbl);
                return;
            }

            int y = 0;
            int initialWidth = _pnlBlockedUsers.ClientSize.Width > 0 ? _pnlBlockedUsers.ClientSize.Width : 360;

            foreach (var user in _blockedUsers)
            {
                var row = BuildBlockedUserRow(user, initialWidth);
                row.Location = new Point(0, y);
                _pnlBlockedUsers.Controls.Add(row);
                y += 60;
            }
        }

        private Panel BuildBlockedUserRow(ContactItem c, int initialWidth)
        {
            var pnl = new Panel { Height = 60, Width = initialWidth, BackColor = TG.WindowBg, Cursor = Cursors.Hand };

            var avatar = new AvatarControl { Size = new Size(40, 40), Location = new Point(10, 10) };
            avatar.SetName(c.DisplayName);

            // FIX: tính width đúng = tổng panel - left offset avatar - right btn - margins
            const int btnW = 80, rightMargin = 12, gap = 8, nameLeft = 58;
            var lblName = new Label
            {
                Text = c.DisplayName,
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.TextName,
                AutoSize = false,
                Height = 20,
                Location = new Point(nameLeft, 20),
                Width = Math.Max(0, initialWidth - nameLeft - btnW - rightMargin - gap),
                BackColor = Color.Transparent,
            };

            var lblStatus = new Label
            {
                Text = "🚫 Đã chặn",
                Font = TG.FontRegular(8f),
                ForeColor = Color.FromArgb(0x75, 0x75, 0x75),
                AutoSize = false,
                Height = 24,
                Width = 80,
                Location = new Point(initialWidth - 92, 16),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(0xF5, 0xF5, 0xF5),
            };

            var btnUnblock = new TelegramButton
            {
                Text = "Bỏ chặn",
                Height = 28,
                Width = 80,
                Radius = TG.RadiusSmall,
                Font = TG.FontRegular(8.5f),
                Location = new Point(initialWidth - 92, 16)
            };
            btnUnblock.Click += async (s, e) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(c.BlockId))
                    {
                        MessageBox.Show("BlockId is empty; cannot unblock.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    // FIX: đúng path "blocked" (không phải "block") và đúng tham số blockID (không phải userId)
                    var res = await http.DeleteAsync($"api/friends/blocked/{c.BlockId}");
                    var body = await res.Content.ReadAsStringAsync();

                    if (res.IsSuccessStatusCode)
                    {
                        c.Status = FriendStatus.None;
                        _blockedUsers.Remove(c);
                        LoadBlockedUsers();
                    }
                    else
                    {
                        MessageBox.Show($"Bỏ chặn thất bại ({(int)res.StatusCode}): {body}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Bỏ chặn thất bại: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            pnl.Controls.AddRange(new Control[] { avatar, lblName, btnUnblock });
            pnl.Paint += (s, e) => e.Graphics.DrawLine(new Pen(TG.DividerLight), 58, 59, pnl.Width, 59);

            // Resize handler — đặt button sát phải, label lấp đầy khoảng còn lại
            EventHandler resizeHandler = (s, e) =>
            {
                if (pnl.ClientSize.Width <= 0) return;
                const int rMargin = 12;
                btnUnblock.Left = pnl.ClientSize.Width - btnUnblock.Width - rMargin;
                btnUnblock.Top  = (60 - btnUnblock.Height) / 2;
                int available = btnUnblock.Left - lblName.Left - 8;
                lblName.Width = Math.Max(0, available);
                pnl.Refresh();
            };
            pnl.Resize += resizeHandler;
            pnl.HandleCreated += resizeHandler;
            // Gọi thêm ParentChanged để chắc chắn chạy sau khi add vào panel cha
            pnl.ParentChanged += resizeHandler;

            // Hover effect
            foreach (Control ctrl in pnl.Controls)
            {
                ctrl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
                ctrl.MouseLeave += (s, e) =>
                {
                    if (!pnl.ClientRectangle.Contains(pnl.PointToClient(Control.MousePosition)))
                        pnl.BackColor = TG.WindowBg;
                };
            }
            pnl.MouseEnter += (s, e) => pnl.BackColor = TG.SidebarHover;
            pnl.MouseLeave += (s, e) => pnl.BackColor = TG.WindowBg;

            return pnl;
        }
    }
}