using SecureChat.Client.Components.Chat;
using SecureChat.Client.Forms.Profile;
using SecureChat.Client.Services;
using SecureChat.DTOs;
using SecureChat.Models;
using System;
using System.Collections.Generic;   // List<>, Dictionary<>
using System.Drawing;               // Color, Point, Size, Image, Bitmap
using System.Drawing.Drawing2D;     // SmoothingMode, LinearGradientBrush
using System.Drawing.Imaging;       // PixelFormat, ColorMatrix, ImageAttributes
using System.IO;                    // File, Path, MemoryStream, FileSystemWatcher
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;       // Task.Delay (dùng cho wallpaper reload)
using System.Windows.Forms;         // Form, Panel, Button, Label, ...

namespace SecureChat.Client
{
    public class frmMainChat : Form
    {
        // ── Các panel layout ──────────────────────────
        private Panel _pnlSidebar = null!; // Panel trái rộng 280px chứa danh sách hội thoại
        private Panel _pnlChat; // Panel phải chiếm phần còn lại, hiển thị tin nhắn
        private Panel _pnlSettingsMenu;  // Menu trượt từ trái ra, đè lên chat area
        private bool _settingsVisible = false; // Trạng thái menu đang hiện hay ẩn, mặc định là ẩn
        private System.Windows.Forms.Timer _slideTimer; // Timer tạo hiệu ứng trượt (animation)
        private int _settingsTargetX;  // Tọa độ X đích khi animate menu

        // ── Sidebar controls ───────────────────────
        private Button _btnHamburger; // nút ☰ mở menu
        private TelegramTextBox _tbSearch; // ô tìm kiếm
        private Panel _pnlConvList; // danh sách cuộc trò chuyện

        // ── Chat area controls ─────────────────────

        private Panel _pnlChatHeader; // thanh header trên cùng khu chat

        //private Panel _pnlMessages; // vùng hiển thị bong bóng tin nhắn
        private ChatPanel _pnlMessages;
        private readonly VoicePlaybackService _voicePlaybackService = new();

        private Panel _pnlInputBar; // thanh nhập tin nhắn bên dưới
        private TelegramTextBox _tbMessage; // TextBox gõ tin nhắn
        private Label _lblChatName, _lblChatStatus; // tên và trạng thái người nhận
        private AvatarControl _chatAvatar; //  avatar tròn người nhận

        private ContextMenuStrip _chatMoreMenu;
        private ToolStripMenuItem _mnuMuteNotifications;
        private ToolStripMenuItem _mnuUnmuteNow;
        private ToolStripMenuItem _mnuDisableSound;
        private ToolStripMenuItem _mnuMuteForever;
        private ToolStripMenuItem _mnuMuteFor;
        private ToolStripItem[] _muteOptionItems;
        private bool _notificationsMuted;
        private bool _notificationsSoundEnabled = true;
        private DateTime? _muteUntilUtc;

        private Panel _pnlReplyContext;
        private Label _lblReplySender;
        private Label _lblReplyText;

        // File transfer helper (moves low-level transfer logic out of UI)
        private readonly FileTransferService _fileTransfer = new();

        private readonly HashSet<string> _processedMessageIds = new();
        private readonly object _processedMessageIdsLock = new();


        // ── Settings menu controls ─────────────────
        private Panel _pnlSettingsHeader;

        // ── Conversation data ──────────────────────────────
        private string _activeConvId = string.Empty;
        private string _currentUserId = string.Empty;
        private string _currentDisplayName = string.Empty;
        private string _currentUsername = string.Empty;
        private string _currentEmail = string.Empty;

        private readonly List<(string Id, string Name, string Preview, string Time, int Unread, bool IsGroup)> _convs = new();

        // Key = convId, Value = danh sách tin nhắn của conversation đó
        // Thêm biến lưu trạng thái trả lời tin nhắn
        private string _replyingToMessageId = null;

        // Local audio recorder service (NAudio)
        private SecureChat.Client.Services.AudioRecorderService? _audioRecorder;

        // Khai báo Dictionary với Tuple 5 tham số (Thêm Id ở đầu)
        private readonly Dictionary<string, List<(string Id, string Text, bool Out, string Time, string Sender)>> _allMsgs = new();

        // Cập nhật lại _currentMsgs sang Tuple 5 tham số
        private List<(string Id, string Text, bool Out, string Time, string Sender)> _currentMsgs =>
            _allMsgs.TryGetValue(_activeConvId, out var list) ? list : new();

        private readonly Dictionary<string, bool> _settingsToggles = new(); // công tắc Night mode

        public frmMainChat()
        {
            InitUI();

            // 1. Các thiết lập DoubleBuffer hiện tại của bạn
            this.DoubleBuffered = true;

            // 1. Kích hoạt cho Panel chính (Nơi chứa hình nền và tin nhắn)
            EnableDoubleBuffering(_pnlMessages);

            // 2. Ép vẽ lại toàn bộ khi cuộn để xóa sạch sọc ngang
            // _pnlMessages.Scroll += (s, e) => _pnlMessages.Invalidate(false); // false = không xóa nền cũ trước khi vẽ lại
            // _pnlMessages.MouseWheel += (s, e) => _pnlMessages.Invalidate(false);

            // Hybrid encryption: Generate and register RSA keypair at startup
            this.Load += FrmMainChat_Load;
        }

        private async void FrmMainChat_Load(object? sender, EventArgs e)
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.GetAsync("api/users/me");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var me = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.UserResponse>(json, opts);
                    if (me != null)
                    {
                        _currentUserId = me.UserID;
                        _currentDisplayName = me.DisplayName;
                        _currentUsername = me.Username;
                        _currentEmail = me.Email;
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }

            // Only generate and register if not already present
            var (pub, priv) = SecureChat.Shared.Security.KeyManager.GetKeyPair();
            if (string.IsNullOrEmpty(pub) || string.IsNullOrEmpty(priv))
            {
                var (publicKey, privateKey) = SecureChat.Shared.Security.RSAEncryption.GenerateKeyPair();
                SecureChat.Shared.Security.KeyManager.SetKeyPair(publicKey, privateKey);
                pub = publicKey;
            }
            try
            {
                await SecureChat.Client.Services.ApiClient.Instance.RegisterPublicKeyAsync(pub);
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }

            // Load danh sách hội thoại từ API thật
            await LoadConversationsAsync();
        }

        private async Task LoadConversationsAsync()
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance;
                var response = await http.GetHttpClient().GetAsync("api/conversations");
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.ConversationResponse>>(json, options);
                if (list == null) return;

                _convs.Clear();
                foreach (var c in list)
                {
                    bool isGroup = c.Type == SecureChat.Models.ConversationType.Group;
                    string preview = c.LastMessageID != null ? "..." : "";
                    string time = c.LastActivityAt.HasValue
                        ? c.LastActivityAt.Value.ToLocalTime().ToString("h:mm tt")
                        : c.CreatedAt.ToLocalTime().ToString("h:mm tt");
                    _convs.Add((c.ConversationID, c.Name ?? "Conversation", preview, time, 0, isGroup));
                }

                BeginInvoke(new Action(() =>
                {
                    BuildConvList();
                    if (_convs.Count > 0)
                    {
                        _activeConvId = _convs[0].Id;
                        _ = LoadMessagesAsync(_activeConvId);
                        LoadConversation(_activeConvId);
                    }
                }));
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, $"Không thể tải danh sách hội thoại: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }

        private async Task LoadMessagesAsync(string convId)
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var response = await http.GetAsync($"api/conversations/{convId}/messages");
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MessageResponse>>(json, options);
                if (list == null) return;

                if (!_allMsgs.ContainsKey(convId))
                    _allMsgs[convId] = new();
                else
                    _allMsgs[convId].Clear();

                var (_, privateKey) = SecureChat.Shared.Security.KeyManager.GetKeyPair();

                foreach (var m in list)
                {
                    bool isOut = m.SenderID == _currentUserId;
                    string text = m.Content ?? "";

                    // Thử giải mã AES nếu ContentIV tồn tại và key có trong cache
                    if (!string.IsNullOrEmpty(m.ContentIV) && m.ContentIV != "TBD")
                    {
                        try
                        {
                            if (SecureChat.Shared.Security.KeyManager.TryGetAesKey(m.MessageID, out var aesKey, out _))
                            {
                                text = System.Text.Encoding.UTF8.GetString(
                                    System.Security.Cryptography.Aes.Create()
                                        .CreateDecryptor(aesKey, Convert.FromBase64String(m.ContentIV))
                                        .TransformFinalBlock(
                                            Convert.FromBase64String(m.Content),
                                            0,
                                            Convert.FromBase64String(m.Content).Length
                                        )
                                );
                            }
                        }
                        catch { /* Giữ nguyên ciphertext nếu giải mã thất bại */ }
                    }

                    string time = m.SentAt.ToLocalTime().ToString("h:mm tt");
                    string sender = isOut ? "" : (m.SenderUsername ?? "");
                    _allMsgs[convId].Add((m.MessageID, text, isOut, time, sender));
                }

                BeginInvoke(new Action(() => BuildMessages()));
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, $"Không thể tải tin nhắn: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }

        protected override void OnResize(EventArgs e)
        {
            if (_pnlMessages != null)
            {
                _pnlMessages.SuspendLayout();
                base.OnResize(e);
                _pnlMessages.ResumeLayout(false); // false = không force layout ngay
                BuildMessages();                  // rebuild bubble theo width mới
            }
            else
            {
                base.OnResize(e);
            }
        }

        // Hàm tiện ích dùng Reflection để ép bật DoubleBuffered cho Panel
        private void EnableDoubleBuffering(Control control)
        {
            var type = typeof(Control);
            // Bật bộ đệm kép
            type.InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, control, new object[] { true });

            // Ép Control vẽ tất cả trong một luồng Paint, tránh vẽ nền riêng lẻ gây nhấp nháy
            var method = type.GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(control, new object[] { ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true });
            }
        }

        // ════════════════════════════════════════════
        //  INIT
        // ════════════════════════════════════════════
        private void InitUI()
        {
            Text = "SecureChat";
            Size = new Size(1000, 660);
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterScreen;
            // this.MaximizeBox = false; // Vô hiệu hóa nút phóng to
            FormBorderStyle = FormBorderStyle.FixedSingle;

            BackColor = Color.White;
            Font = TG.FontRegular(9.5f);

            BuildSidebar();
            BuildChatArea();
            BuildSettingsMenu();

            // Thứ tự add: sidebar → chat → settings (settings ở trên cùng)
            Controls.Add(_pnlChat);
            Controls.Add(_pnlSidebar);
            Controls.Add(_pnlSettingsMenu);  // add cuối = hiện trên cùng

            Resize += (s, e) =>
            {
                AdjustLayout();
                UpdateCachedBackground(); // Thêm dòng này để ảnh nền co giãn theo Form
            };
            AdjustLayout();

            // Danh sách hội thoại sẽ được load từ API trong FrmMainChat_Load
            SetupWallpaperWatcher(); // 1. Bắt đầu theo dõi thư mục ảnh

            // 2. Nạp hình nền lần đầu tiên
            // Nên để sau AdjustLayout để _pnlChat và _pnlMessages đã có kích thước chuẩn
            UpdateCachedBackground();
        }

        private void AdjustLayout()
        {
            // Khai báo các hằng số kích thước
            int sw = 300;                        // Sidebar Width
            int smw = 260;                       // Settings Menu Width

            // Thiết lập vị trí và kích thước cho Sidebar và Khung Chat
            // .SetBounds(x, y, rộng, cao)
            // ClientSize.Height: Chiều cao kéo dài bằng toàn bộ chiều cao vùng làm việc của cửa sổ.
            _pnlSidebar.SetBounds(0, 0, sw, ClientSize.Height);
            _pnlChat.SetBounds(sw, 0, ClientSize.Width - sw, ClientSize.Height);

            // Xử lý hiệu ứng trượt (Slide) của Menu Cài đặt
            // Settings menu: slide overlay từ bên TRÁI
            int visibleX = 0; // Khai báo tọa độ X khi menu cài đặt hiển thị là 0 (nằm sát mép trái màn hình).
            int hiddenX = -smw; // Khai báo tọa độ X khi menu cài đặt ẩn đi là -260 (đẩy toàn bộ menu ra ngoài phạm vi nhìn thấy về phía bên trái).
            // Kiểm tra nếu biến trạng thái _settingsVisible đang là false (người dùng không mở menu cài đặt).
            if (!_settingsVisible)
                _pnlSettingsMenu.SetBounds(hiddenX, 0, smw, ClientSize.Height);
            else
                _pnlSettingsMenu.SetBounds(visibleX, 0, smw, ClientSize.Height);
        }

        // ════════════════════════════════════════════
        //  SIDEBAR: Một thanh tiêu đề trên cùng (Header) chứa nút menu và nút chỉnh sửa, một ô tìm kiếm (Search), và danh sách các cuộc hội thoại (Conversation list).
        // ════════════════════════════════════════════
        private void BuildSidebar()
        {
            _pnlSidebar = new Panel { BackColor = Color.White };

            // ── Header xanh ──────────────────────────
            var pnlHeader = new Panel { Height = 52, BackColor = TG.TitleBarBg, Dock = DockStyle.Top };
            // Gắn chặt panel này vào mép trên cùng của sidebar.

            _btnHamburger = new Button
            {
                Text = "☰",
                FlatStyle = FlatStyle.Flat, // Đặt kiểu hiển thị phẳng, không có hiệu ứng nổi 3D của Windows cổ điển.
                Font = TG.FontRegular(14f),
                ForeColor = Color.White,
                Size = new Size(48, 52),
                Location = new Point(0, 0), // Đặt nút ở góc trên cùng bên trái của header.
                BackColor = Color.Transparent, // Nền trong suốt để lộ màu xanh của header.
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter, // Căn giữa biểu tượng "☰".
            };

            _btnHamburger.FlatAppearance.BorderSize = 0; // Loại bỏ đường viền bao quanh nút khi vẽ giao diện phẳng.
            _btnHamburger.Click += (s, e) => ToggleSettingsMenu();

            var lblTitle = new Label
            {
                Text = "SecureChat",
                Font = TG.FontSemiBold(11f),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                Location = new Point(50, 0), // Đặt cách mép trái 50px (để không đè lên nút Hamburger rộng 48px).
                Height = 52,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            var btnEdit = new Button
            {
                Text = "✏",
                FlatStyle = FlatStyle.Flat,
                Font = TG.FontRegular(13f),
                ForeColor = Color.White,
                Size = new Size(40, 52),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            btnEdit.FlatAppearance.BorderSize = 0;

            // pnlHeader.Controls.AddRange(new Control[] { _btnHamburger, lblTitle, btnEdit });
            pnlHeader.Controls.AddRange(new Control[] { _btnHamburger, lblTitle });

            pnlHeader.Resize += (s, e) =>
            {
                lblTitle.Width = pnlHeader.Width - 96;
                btnEdit.Location = new Point(pnlHeader.Width - 42, 0);
            };

            // ── Search ────────────────────────────────
            var pnlSearch = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = Color.White, Padding = new Padding(8, 6, 8, 4) };
            _tbSearch = new TelegramTextBox { Height = 32, Dock = DockStyle.Fill };
            _tbSearch.SetPlaceholder("🔍  Search");
            pnlSearch.Controls.Add(_tbSearch);

            // ── Conversation list ─────────────────────
            _pnlConvList = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            BuildConvList();

            _pnlSidebar.Controls.Add(_pnlConvList);
            _pnlSidebar.Controls.Add(pnlSearch);
            _pnlSidebar.Controls.Add(pnlHeader);
        }

        private void BuildConvList()
        {
            // Xóa bỏ toàn bộ các đối tượng đó để chuẩn bị vẽ lại từ đầu(tránh việc danh sách mới bị đè lên danh sách cũ).
            _pnlConvList.Controls.Clear();

            // Nó dùng để xác định vị trí đặt hàng tiếp theo, giúp các hàng không bị đè lên nhau.
            int y = 0;

            // Duyệt qua từng cuộc trò chuyện trong danh sách dữ liệu.
            foreach (var c in _convs)
            {
                var row = BuildConvRow(c.Id, c.Name, c.Preview, c.Time, c.Unread, c.IsGroup);
                EnableDoubleBuffering(row); // <--- QUAN TRỌNG: 
                row.Location = new Point(0, y);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                row.Width = _pnlConvList.ClientSize.Width;
                _pnlConvList.Controls.Add(row);
                y += 68;
            }
        }

        private Panel BuildConvRow(string id, string name, string preview, string time, int unread, bool isGroup)
        {
            // Tạo một khung chứa có chiều cao cố định là 68px, màu nền trắng và đổi con trỏ chuột thành hình bàn tay (Cursors.Hand) khi rê vào.
            // Thuộc tính Tag được gán bằng id của cuộc trò chuyện để dễ dàng nhận diện.
            var pnl = new Panel { Height = 68, BackColor = Color.White, Tag = id, Cursor = Cursors.Hand };

            // Kiểm tra xem dòng chat này có đang được người dùng chọn (active) hay không bằng cách so sánh Tag với biến toàn cục _activeConvId.
            bool isActive() => (string)pnl.Tag == _activeConvId;

            // Avatar
            var avatar = new AvatarControl { Size = new Size(48, 48), Location = new Point(10, 10) };
            EnableDoubleBuffering(avatar); // <--- QUAN TRỌNG: Control tự vẽ rất cần cái này
            avatar.SetName(name);
            avatar.ShowOnline = false; // nếu có thì có chấm xanh nhỏ ở dưới phải avatar

            // Name + preview
            var lblName = new Label
            {
                Text = name,
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.TextName,
                AutoSize = false,
                AutoEllipsis = true,  
                Height = 22,
                Location = new Point(66, 10),
                BackColor = Color.Transparent,
            };
            // Hiển thị nội dung tin nhắn mới nhất.
            // Thuộc tính AutoEllipsis = true rất quan trọng: nó sẽ tự động thêm dấu ba chấm ... nếu tin nhắn quá dài vượt quá chiều rộng của nhãn.
            var lblPreview = new Label
            {
                Text = preview,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 18,
                Location = new Point(66, 32),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            // Hiển thị thời gian nhắn tin ở góc trên bên phải.
            // Sử dụng Anchor để giữ khoảng cách cố định với mép phải khi co dãn giao diện.
            var lblTime = new Label
            {
                Text = time,
                Font = TG.FontRegular(7.5f),
                ForeColor = TG.TextTime,
                AutoSize = true,
                BackColor = Color.Transparent,
            };
            // Nhãn hiển thị số lượng tin nhắn chưa đọc.
            // Nó chỉ hiện lên khi unread > 0.
            var lblUnread = new Label
            {
                // Nếu > 99 thì cho rộng ra một chút (ví dụ 28px) để chứa vừa dấu "+"
                Size = unread > 99 ? new Size(30, 25) : new Size(22, 22),
                AutoSize = false,
                BackColor = Color.Transparent,


                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = TG.FontSemiBold(8f),
                Visible = unread > 0,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            // Đoạn code này tự tay vẽ (custom draw) một hình tròn màu xanh (TG.Blue) đè lên nhãn và viết số lượng tin nhắn vào giữa.
            // Nếu số lượng lớn hơn 99, nó tự động đổi thành "99+".
            lblUnread.Paint += (s, e) =>
            {
                if (!lblUnread.Visible) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new SolidBrush(TG.Blue);

                // Vẽ hình nền tròn hoặc bầu dục nếu số dài
                e.Graphics.FillEllipse(brush, 0, 0, lblUnread.Width - 1, lblUnread.Height - 1);

                // LOGIC QUAN TRỌNG: Kiểm tra lại biến unread ở đây
                string txt = unread > 99 ? "99+" : unread.ToString();

                using var fore = new SolidBrush(Color.White);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(txt, lblUnread.Font, fore, lblUnread.ClientRectangle, sf);
            };


            // Add controls
            pnl.Controls.AddRange(new Control[] { avatar, lblName, lblPreview, lblTime, lblUnread });

            // Click behavior
            // Định nghĩa hành động khi người dùng bấm vào dòng chat: Cập nhật ID đang kích hoạt, vẽ lại danh sách và tải nội dung tin nhắn của cuộc trò chuyện đó.
            Action doClick = () => { _activeConvId = id; BuildConvList(); LoadConversation(id); };
            pnl.Click += (s, e) => doClick();
            avatar.Click += (s, e) => doClick();

            // Khi rê chuột vào/ra khỏi tấm nền, nếu dòng chat này không ở trạng thái được chọn (!isActive()), nền sẽ đổi sang màu hover (TG.SidebarHover) và ngược lại.
            pnl.MouseEnter += (s, e) => { if (!isActive()) pnl.BackColor = TG.SidebarHover; };
            pnl.MouseLeave += (s, e) => { if (!isActive()) pnl.BackColor = Color.White; };

            // Resize child layout when row width changes
            pnl.Resize += (s, e) =>
            {
                lblName.Width = Math.Max(0, pnl.Width - 66 - 80);

                // NẾU CÓ TIN NHẮN CHƯA ĐỌC: Trừ đi độ rộng của Badge (khoảng 35-40px tính cả lề)
                // NẾU KHÔNG CÓ: Chỉ trừ lề phải 12px
                int previewRightMargin = (unread > 0) ? 40 : 12;
                lblPreview.Width = Math.Max(0, pnl.Width - 66 - previewRightMargin);

                lblTime.Location = new Point(pnl.Width - lblTime.Width - 12, 12);
                lblUnread.Location = new Point(pnl.Width - lblUnread.Width - 12, 34);


                // Update colors for active state
                if (isActive())
                {
                    pnl.BackColor = TG.SidebarActive;
                    lblName.ForeColor = Color.White;
                    lblPreview.ForeColor = Color.FromArgb(220, 240, 255);
                    lblTime.ForeColor = Color.FromArgb(200, 230, 255);
                }
                else
                {
                    pnl.BackColor = Color.White;
                    lblName.ForeColor = TG.TextName;
                    lblPreview.ForeColor = TG.TextSecondary;
                    lblTime.ForeColor = TG.TextTime;
                }

                lblUnread.BringToFront(); // đảm bảo 99+ không bị đè
            };

            // Set width ngay lần đầu render
            int initWidth = _pnlConvList.ClientSize.Width > 0 ? _pnlConvList.ClientSize.Width : 280;
            lblTime.Location = new Point(Math.Max(0, initWidth - lblTime.Width - 12), 12); // thêm dòng này
            lblName.Width = Math.Max(0, initWidth - 66 - 80);
            int initPreviewMargin = (unread > 0) ? 40 : 12;
            lblPreview.Width = Math.Max(0, initWidth - 66 - initPreviewMargin);

            // Truyền sự kiện (Event Propagation)/hover for child controls
            // Trong WinForms, khi bạn click vào một Label nằm bên trong Panel, sự kiện Click của Panel sẽ không tự kích hoạt.
            // Vòng lặp này duyệt qua tất cả các control con để gán đè sự kiện Click và Hover. Mục đích là giúp người dùng click hay rê chuột vào bất cứ điểm nào trên dòng chat(dù là vào chữ hay vào khoảng trống) thì cả dòng chat vẫn phản hồi đồng bộ.
            foreach (Control c in pnl.Controls)
            {
                if (c == avatar) continue; // avatar wired already
                c.Click += (s, e) => doClick();
                c.MouseEnter += (s, e) => { if (!isActive()) pnl.BackColor = TG.SidebarHover; };
                c.MouseLeave += (s, e) => { if (!isActive()) pnl.BackColor = Color.White; };
            }

            pnl.Width = _pnlConvList?.ClientSize.Width ?? pnl.Width;
            // Ép Panel tính toán lại vị trí các control dựa trên các logic resize vừa viết ở trên trước khi trả về kết quả.
            pnl.PerformLayout();

            return pnl;
        }

        // ════════════════════════════════════════════
        //  CHAT AREA
        // ════════════════════════════════════════════

        // Mới thêm
        private Button _btnToggleSidebar;
        private Panel _pnlRightSidebar;
        private bool _isSidebarOpen = false; // Biến phụ để theo dõi trạng thái

        private void BuildChatArea()
        {
            // chứa toàn bộ Header, danh sách tin nhắn và thanh nhập liệu.
            _pnlChat = new Panel { BackColor = Color.White };

            // ── Chat Header ───────────────────────────
            _pnlChatHeader = new Panel { Height = 52, BackColor = Color.White, Dock = DockStyle.Top };
            // vẽ một đường kẻ ngang màu xám/nhạt (TG.Divider) ở dưới cùng để ngăn cách header với vùng tin nhắn.
            _pnlChatHeader.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(TG.Divider), 0, 51, _pnlChatHeader.Width, 51);

            _chatAvatar = new AvatarControl { Size = new Size(36, 36), Location = new Point(12, 8) };
            _lblChatName = new Label
            {
                Font = TG.FontSemiBold(10f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                Height = 22,
                Location = new Point(56, 8),
                BackColor = Color.Transparent,
            };
            _lblChatStatus = new Label
            {
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 20,
                Location = new Point(56, 30),
                BackColor = Color.Transparent,
            };

            // Right buttons: search, video call, more
            var btnSearch = MakeChatHeaderBtn("🔍");
            var btnVideo = MakeChatHeaderBtn("📹");
            var btnMore = MakeChatHeaderBtn("⋮");
            btnMore.Click += (s, e) =>
            {
                EnsureChatMoreMenu();
                _chatMoreMenu.Show(btnMore, new Point(btnMore.Width - _chatMoreMenu.Width, btnMore.Height + 2));
            };

            // Khởi tạo nút và gán vào biến đã khai báo ở trên
            _btnToggleSidebar = MakeChatHeaderBtn("⏪");

            // _pnlChatHeader.Controls.AddRange(new Control[] { _chatAvatar, _lblChatName, _lblChatStatus, btnSearch, btnVideo, btnMore });
            // Thêm vào AddRange (nhớ thêm _btnToggleSidebar)
            _pnlChatHeader.Controls.AddRange(new Control[] { _chatAvatar, _lblChatName, _lblChatStatus, btnSearch, btnVideo, _btnToggleSidebar, btnMore });

            _btnToggleSidebar.Click += (s, e) =>
            {
                /*
                if (_pnlRightSidebar.Visible)
                {
                    _pnlRightSidebar.Visible = false;
                    _btnToggleSidebar.Text = "⏪"; // Đóng rồi thì hiện mũi tên hướng trái để mở lại
                }
                else
                {
                    _pnlRightSidebar.Visible = true;
                    _btnToggleSidebar.Text = "⏩"; // Mở rồi thì hiện mũi tên hướng phải để đóng
                }
                */
            };

            /* _pnlChatHeader.Resize += (s, e) =>
            {

                _lblChatName.Width = _pnlChatHeader.Width - 56 - 130;
                _lblChatStatus.Width = _pnlChatHeader.Width - 56 - 130;
                btnMore.Location = new Point(_pnlChatHeader.Width - 42, 8);
                btnVideo.Location = new Point(_pnlChatHeader.Width - 84, 8);
                btnSearch.Location = new Point(_pnlChatHeader.Width - 126, 8);
            };*/
            _pnlChatHeader.Resize += (s, e) =>
            {
                // Khoảng cách dành cho 4 nút (42px mỗi nút) = 168px
                _lblChatName.Width = _pnlChatHeader.Width - 56 - 170;
                _lblChatStatus.Width = _pnlChatHeader.Width - 56 - 170;

                int w = _pnlChatHeader.Width;
                btnMore.Location = new Point(w - 42, 8);             // Cách mép 42
                _btnToggleSidebar.Location = new Point(w - 84, 8);   // Cách mép 84 (Nút của bạn ở đây)
                btnVideo.Location = new Point(w - 126, 8);           // Cách mép 126
                btnSearch.Location = new Point(w - 168, 8);          // Cách mép 168
            };


            // ── Messages area  - Vùng hiển thị tin nhắn ─────────────────────────
            _pnlMessages = new ChatPanel
            {
                Dock = DockStyle.Fill, // chiếm trọn phần diện tích còn lại
                AutoScroll = true,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(0xDB, 0xE8, 0xD5), // Thêm dòng này
            };


            typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, _pnlMessages, new object[] { true });
            // _pnlMessages.Paint += PaintChatBackground;
            // Click vào vùng chat → đóng settings menu
            _pnlMessages.Click += (s, e) => { if (_settingsVisible) HideSettingsMenu(); };

            // ── Input bar ─────────────────────────────
            BuildInputBar();

            _pnlChat.Controls.Add(_pnlMessages);
            _pnlChat.Controls.Add(_pnlInputBar);
            _pnlChat.Controls.Add(_pnlChatHeader);

            // _pnlMessages.Resize += (s, e) => _pnlMessages.Invalidate(); // bỏ vì đã có PaintChatBackground tự xử lý.
            _pnlMessages.Resize += (s, e) => UpdateCachedBackground();
        }

        private void EnsureChatMoreMenu()
        {
            if (_chatMoreMenu != null) return;

            _chatMoreMenu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                BackColor = Color.White,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 10f),
                Renderer = new ToolStripProfessionalRenderer(new ChatMenuColorTable())
            };

            _mnuMuteNotifications = CreateChatMenuItem("🔕  Mute notifications", (_, __) => ToggleMuteNotificationsQuick());

            _mnuUnmuteNow = CreateChatMenuItem("🔊  Unmute now", (_, __) => UnmuteNow());
            _mnuDisableSound = CreateChatMenuItem("🔇  Disable sound", (_, __) => ToggleDisableSound());
            _mnuMuteForever = CreateChatMenuItem("⛔  Mute forever", (_, __) => SetMuteForever());
            _mnuMuteFor = CreateChatMenuItem("⏳  Mute for...", null);

            _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem("30 minutes", (_, __) => SetMuteFor(TimeSpan.FromMinutes(30))));
            _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem("1 hour", (_, __) => SetMuteFor(TimeSpan.FromHours(1))));
            _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem("8 hours", (_, __) => SetMuteFor(TimeSpan.FromHours(8))));
            _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem("1 day", (_, __) => SetMuteFor(TimeSpan.FromDays(1))));
            _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem("1 week", (_, __) => SetMuteFor(TimeSpan.FromDays(7))));

            _muteOptionItems = new ToolStripItem[]
            {
                _mnuUnmuteNow,
                _mnuDisableSound,
                _mnuMuteForever,
                _mnuMuteFor
            };

            foreach (var item in _muteOptionItems)
                _mnuMuteNotifications.DropDownItems.Add(item);

            var mnuViewInfo = CreateChatMenuItem("ℹ️  View group info", (_, __) => OpenGroupInfo());
            var mnuManageGroup = CreateChatMenuItem("🎛️  Manage group", (_, __) => OpenEditGroupFromChat());
            var mnuCreatePoll = CreateChatMenuItem("📊  Create poll", (_, __) => CreatePoll());
            var mnuClearHistory = CreateChatMenuItem("🧹  Clear history", (_, __) => ClearHistory());
            var mnuDeleteLeave = CreateChatMenuItem("🚪  Delete and leave", (_, __) => DeleteAndLeave(), Color.FromArgb(0xE2, 0x4B, 0x4A));

            _chatMoreMenu.Items.Add(_mnuMuteNotifications);
            _chatMoreMenu.Items.Add(new ToolStripSeparator());
            _chatMoreMenu.Items.Add(mnuViewInfo);
            _chatMoreMenu.Items.Add(mnuManageGroup);
            _chatMoreMenu.Items.Add(mnuCreatePoll);
            _chatMoreMenu.Items.Add(mnuClearHistory);
            _chatMoreMenu.Items.Add(new ToolStripSeparator());
            _chatMoreMenu.Items.Add(mnuDeleteLeave);

            RefreshMuteMenuState();
        }

        private ToolStripMenuItem CreateChatMenuItem(string text, EventHandler onClick, Color? foreColor = null)
        {
            var item = new ToolStripMenuItem
            {
                Text = text,
                ForeColor = foreColor ?? TG.TextPrimary,
                Font = new Font("Segoe UI Emoji", 10f),
                Padding = new Padding(10, 8, 10, 8)
            };
            item.Click += onClick;
            return item;
        }

        private ToolStripMenuItem CreateChatSubMenuItem(string text, EventHandler onClick)
        {
            var item = new ToolStripMenuItem
            {
                Text = text,
                ForeColor = TG.TextPrimary,
                Font = new Font("Segoe UI", 10f),
                Padding = new Padding(8, 6, 8, 6)
            };
            item.Click += onClick;
            return item;
        }

        private void ToggleDisableSound()
        {
            _notificationsSoundEnabled = !_notificationsSoundEnabled;
            RefreshMuteMenuState();
            MessageBox.Show(this,
                _notificationsSoundEnabled ? "Notification sound enabled for this chat." : "Notification sound disabled for this chat.",
                "Notifications",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SetMuteForever()
        {
            // Toggle behavior: choose again => unmute forever off
            if (_notificationsMuted && !_muteUntilUtc.HasValue)
            {
                UnmuteNow();
                return;
            }

            _notificationsMuted = true;
            _muteUntilUtc = null;
            RefreshMuteMenuState();
            MessageBox.Show(this,
                "Notifications are muted forever for this chat.",
                "Notifications",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SetMuteFor(TimeSpan duration)
        {
            _notificationsMuted = true;
            _muteUntilUtc = DateTime.UtcNow.Add(duration);
            RefreshMuteMenuState();
            MessageBox.Show(this,
                $"Notifications are muted for {FormatDuration(duration)}.",
                "Notifications",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 60) return $"{(int)duration.TotalMinutes} minutes";
            if (duration.TotalHours < 24) return $"{(int)duration.TotalHours} hours";
            return $"{(int)duration.TotalDays} days";
        }

        private void RefreshMuteMenuState()
        {
            if (_mnuMuteNotifications == null) return;

            if (_muteUntilUtc.HasValue && DateTime.UtcNow >= _muteUntilUtc.Value)
            {
                _notificationsMuted = false;
                _muteUntilUtc = null;
            }

            // If mute forever -> show direct "Unmute" action without submenu popup.
            if (_notificationsMuted && !_muteUntilUtc.HasValue)
            {
                if (_mnuMuteNotifications.DropDownItems.Count > 0)
                    _mnuMuteNotifications.DropDownItems.Clear();

                _mnuMuteNotifications.Text = "🔊  Unmute";
            }
            else
            {
                if (_mnuMuteNotifications.DropDownItems.Count == 0)
                {
                    foreach (var item in _muteOptionItems)
                        _mnuMuteNotifications.DropDownItems.Add(item);
                }

                _mnuMuteNotifications.Text = _notificationsMuted
                    ? $"🔕  Muted until {_muteUntilUtc.Value.ToLocalTime():HH:mm}"
                    : "🔔  Mute notifications";
            }

            if (_mnuUnmuteNow != null)
                _mnuUnmuteNow.Visible = _notificationsMuted;
            if (_mnuDisableSound != null)
                _mnuDisableSound.Checked = !_notificationsSoundEnabled;
            if (_mnuMuteForever != null)
                _mnuMuteForever.Checked = _notificationsMuted && !_muteUntilUtc.HasValue;
            if (_mnuMuteFor != null)
                _mnuMuteFor.Checked = _notificationsMuted && _muteUntilUtc.HasValue;
        }

        private void ToggleMuteNotificationsQuick()
        {
            // Direct toggle only when menu is in "Unmute" mode.
            if (_notificationsMuted && !_muteUntilUtc.HasValue)
            {
                UnmuteNow();
            }
        }

        private void UnmuteNow()
        {
            _notificationsMuted = false;
            _muteUntilUtc = null;
            RefreshMuteMenuState();

            MessageBox.Show(this,
                "Notifications are enabled for this chat.",
                "Notifications",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void OpenGroupInfo()
        {
            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            var members = new List<SecureChat.Client.Forms.Chat.MemberModel>();
            try
            {
                var res = await http.GetAsync($"api/conversations/{_activeConvId}/members");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MemberResponse>>(json, opts);
                    if (list != null)
                        members = list.Select(m => new SecureChat.Client.Forms.Chat.MemberModel(
            m.User?.DisplayName ?? m.Nickname ?? "Unknown",
            m.User?.ShowOnlineStatus == true ? "online" : "last seen recently",
            m.Role.ToString(),
            null,
            System.Drawing.Color.Gray
        )).ToList();
                }
            }
            catch { }

            using var dlg = new SecureChat.Client.Forms.Chat.frmGroupInfo();
            dlg.LoadGroup(_lblChatName.Text, null, members);
            dlg.SetContext(_activeConvId, _currentDisplayName);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }


        private void OpenEditGroupFromChat()
        {
            using var dlg = new SecureChat.Client.Forms.Chat.frmEditGroup(_lblChatName.Text, _activeConvId);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _lblChatName.Text = dlg.GroupName;
        }

        private void CreatePoll()
        {
            using var dlg = new SecureChat.Client.Forms.Chat.frmCreatePoll();
            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            var pollText = $"📊 {dlg.PollQuestion}";
            if (!string.IsNullOrWhiteSpace(dlg.PollDescription))
                pollText += $"\n{dlg.PollDescription}";

            for (var i = 0; i < dlg.PollOptions.Count; i++)
                pollText += $"\n{i + 1}. {dlg.PollOptions[i]}";

            // Thêm Guid.NewGuid().ToString() vào đầu để làm ID cho tin nhắn chứa Poll
            _currentMsgs.Add((Guid.NewGuid().ToString(), pollText, true, DateTime.Now.ToString("h:mm tt"), ""));
            BuildMessages();
        }

        private void ClearHistory()
        {
            using var dlg = new SecureChat.Client.Forms.Chat.frmClearHistory(_lblChatName.Text);
            if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.DeleteConfirmed)
                return;

            _currentMsgs.Clear();
            BuildMessages();
        }

        private async void DeleteAndLeave()
        {
            // Load members từ API
            var memberNames = new List<string>();
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.GetAsync($"api/conversations/{_activeConvId}/members");
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync();
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MemberResponse>>(json, opts);
                    if (list != null)
                        memberNames = list
                            .Select(m => m.User?.DisplayName ?? m.Nickname ?? "Unknown")
                            .ToList();
                }
            }
            catch { }

            using var dlg = new SecureChat.Client.Forms.Chat.frmLeaveGroup(
                _lblChatName.Text, _currentDisplayName, memberNames);
            if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.LeaveConfirmed)
                return;
            _currentMsgs.Clear();
            BuildMessages();
            _lblChatStatus.Text = $"You left this group · New owner: {dlg.AppointedAdminName}";
        }

        // Cache ảnh nền để không load lại mỗi lần paint
        private Image _wallpaper = null;
        private bool _wallpaperLoaded = false;


        private Image LoadWallpaper()
        {
            if (_wallpaperLoaded) return _wallpaper;
            _wallpaperLoaded = true;

            string imagesDir = Path.Combine(Application.StartupPath, "Resources", "Images");
            string[] candidates = {
            Path.Combine(imagesDir, "chat_bg.jpg"),
            Path.Combine(imagesDir, "chat_bg.png"),
            Path.Combine(imagesDir, "wallpaper.jpg"),
            Path.Combine(imagesDir, "wallpaper.png"),
            Path.Combine(imagesDir, "background.jpg"),
            Path.Combine(imagesDir, "background.png"),
    };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    // read bytes into memory to avoid locking the file on disk
                    var data = File.ReadAllBytes(path);
                    using var ms = new MemoryStream(data);
                    using var img = Image.FromStream(ms);
                    _wallpaper = new Bitmap(img); // clone so stream can be disposed
                    return _wallpaper;
                }
                catch
                {
                    // ignore and try next candidate
                }
            }

            // fallback gradient
            _wallpaper = CreateFallbackWallpaper(800, 600);
            return _wallpaper;
        }

        // add inside frmMainChat class
        private FileSystemWatcher? _wallpaperWatcher;

        // Tự động theo dõi một thư mục ảnh.
        // Khi thêm, xóa hoặc sửa ảnh trong đó, chương trình sẽ biết để cập nhật hình nền ngay lập tức.
        private void SetupWallpaperWatcher()
        {
            try
            {
                // Tạo đường dẫn đến thư mục chứa ảnh. Nó kết hợp: Nơi phần mềm đang chạy (StartupPath) + thư mục Resources + thư mục Images.
                string dir = Path.Combine(Application.StartupPath, "Resources", "Images");

                // Kiểm tra xem thư mục đó có tồn tại không. Nếu không thấy thư mục, thoát luôn (không theo dõi gì cả).
                if (!Directory.Exists(dir)) return;

                _wallpaperWatcher = new FileSystemWatcher(dir)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _wallpaperWatcher.Changed += OnWallpaperFileChanged;
                _wallpaperWatcher.Created += OnWallpaperFileChanged;
                _wallpaperWatcher.Renamed += OnWallpaperFileChanged;
                _wallpaperWatcher.Deleted += OnWallpaperFileChanged;
            }
            catch
            {
                // ignore watcher setup errors
            }
        }

        // Xử lý khi có sự kiện thay đổi file xảy ra.
        // Mục tiêu của nó là lọc đúng file ảnh cần thiết và cập nhật lại giao diện một cách an toàn.
        private void OnWallpaperFileChanged(object sender, FileSystemEventArgs e)
        {
            // only react to the names LoadWallpaper() searches for
            string[] names = { "chat_bg.jpg", "chat_bg.png", "wallpaper.jpg", "wallpaper.png", "background.jpg", "background.png" };
            string file = Path.GetFileName(e.FullPath);
            bool match = false;
            foreach (var n in names) { if (string.Equals(n, file, StringComparison.OrdinalIgnoreCase)) { match = true; break; } }
            if (!match) return;

            // short delay to let writers finish, then reload on UI thread
            Task.Delay(200).ContinueWith(_ =>
            {
                if (IsHandleCreated) BeginInvoke(new Action(ReloadWallpaper));
            }, TaskScheduler.Default);
        }

        // vai trò "dọn dẹp" dữ liệu cũ để chuẩn bị cho việc hiển thị hình ảnh mới
        private void ReloadWallpaper()
        {
            _wallpaperLoaded = false;
            _wallpaper?.Dispose();
            _wallpaper = null;

            // Cập nhật lại bộ đệm ảnh thay vì chỉ Invalidate
            UpdateCachedBackground();

            // _pnlMessages?.Invalidate();
        }

        // Tự tạo ra một hình nền mặc định (màu chuyển sắc - gradient) trong trường hợp chương trình không tìm thấy bất kỳ file ảnh nào trong thư mục.
        private Bitmap CreateFallbackWallpaper(int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            using var brush = new LinearGradientBrush(
                new Rectangle(0, 0, w, h),
                Color.FromArgb(0xDB, 0xE8, 0xD5),
                Color.FromArgb(0xB5, 0xCC, 0xA8),
                LinearGradientMode.Vertical);
            g.FillRectangle(brush, 0, 0, w, h);
            return bmp;
        }

        /*
        private void PaintChatBackground(object sender, PaintEventArgs e)
        {
            var panel = sender as Panel;

            // THÊM DÒNG NÀY — xóa vùng clip trước khi vẽ
            e.Graphics.Clear(Color.FromArgb(0xDB, 0xE8, 0xD5)); // màu fallback gradient

            var img = LoadWallpaper();
            if (img != null)
            {
                // Sử dụng ClientRectangle để lấy vùng hiển thị thực tế
                Rectangle displayRect = panel.ClientRectangle;

                float scaleX = (float)displayRect.Width / img.Width;
                float scaleY = (float)displayRect.Height / img.Height;
                float scale = Math.Max(scaleX, scaleY);

                int drawW = (int)(img.Width * scale);
                int drawH = (int)(img.Height * scale);

                // Căn giữa hình ảnh
                int offsetX = (displayRect.Width - drawW) / 2;
                int offsetY = (displayRect.Height - drawH) / 2;

                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

                // Vẽ hình nền
                e.Graphics.DrawImage(img, offsetX, offsetY, drawW, drawH);

            }
        }
        */

        private Button MakeChatHeaderBtn(string icon)
        {
            var btn = new Button
            {
                Text = icon,
                Font = new Font("Segoe UI Emoji", 13f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 36),
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 0, 0, 0);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 0, 0, 0);
            return btn;
        }

        private string ExtractActualText(string rawText)
        {
            const string replyPrefix = "reply::";
            if (!string.IsNullOrEmpty(rawText) && rawText.StartsWith(replyPrefix))
            {
                var parts = rawText.Substring(replyPrefix.Length).Split(new[] { "::" }, 3, StringSplitOptions.None);
                if (parts.Length == 3) return parts[2]; // Chỉ lấy TinNhắnMới
            }
            return rawText;
        }


        private void BuildInputBar()
        {
            _pnlInputBar = new Panel { Height = 56, Dock = DockStyle.Bottom, BackColor = Color.White };
            _pnlInputBar.Paint += (s, e) => e.Graphics.DrawLine(new Pen(TG.Divider), 0, 0, _pnlInputBar.Width, 0);

            // =========================================================
            // 1. TẠO PANEL REPLY (Bao gồm Icon, Đường viền, Tên, Text)
            // =========================================================
            _pnlReplyContext = new Panel { Dock = DockStyle.Top, Height = 44, Visible = false, BackColor = Color.White };

            // Icon mũi tên quay lại
            var lblReplyIcon = new Label
            {
                Text = "↩",
                Font = new Font("Segoe UI Emoji", 15f), // Font to một chút cho giống icon
                ForeColor = TG.Blue,
                Location = new Point(6, 6), // Nằm ở mép trái
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Đường viền dọc bên trái (Màu xanh accent)
            var pnlAccent = new Panel { Width = 3, Height = 34, BackColor = TG.Blue, Location = new Point(48, 5) };

            // Tên người gửi
            _lblReplySender = new Label
            {
                Font = TG.FontSemiBold(9.5f),
                ForeColor = TG.Blue,
                Location = new Point(56, 4),
                AutoSize = true
            };

            // Nội dung tin nhắn gốc
            _lblReplyText = new Label
            {
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextSecondary,
                Location = new Point(56, 22),
                AutoSize = false,
                Size = new Size(200, 20),
                AutoEllipsis = true
            };

            // Nút [X] để tắt Reply
            var btnCloseReply = new Button
            {
                Text = "✕",
                Font = TG.FontRegular(10f),
                FlatStyle = FlatStyle.Flat,
                ForeColor = TG.TextHint,
                Size = new Size(30, 30),
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnCloseReply.FlatAppearance.BorderSize = 0;
            btnCloseReply.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnCloseReply.FlatAppearance.MouseDownBackColor = Color.Transparent;

            // Sự kiện tắt Reply
            btnCloseReply.Click += (s, e) => { _replyingToMessageId = null; _pnlReplyContext.Visible = false; _pnlInputBar.Height = 56; };

            // Thêm TẤT CẢ vào Panel Reply
            _pnlReplyContext.Controls.AddRange(new Control[] { pnlAccent, _lblReplySender, _lblReplyText, btnCloseReply, lblReplyIcon });

            // Tự động giãn dòng text theo chiều rộng Form
            _pnlReplyContext.Resize += (s, e) =>
            {
                btnCloseReply.Location = new Point(_pnlReplyContext.Width - 40, 7);
                _lblReplyText.Width = _pnlReplyContext.Width - 110;
            };

            // =========================================================
            // 2. TẠO KHUNG NHẬP CHAT BÌNH THƯỜNG (Có TextBox, Nút gửi...)
            // =========================================================
            var btnAttach = MakeInputBtn("📎");
            btnAttach.Click += async (s, e) =>
            {
                try
                {
                    using var ofd = new OpenFileDialog { Multiselect = false };
                    if (ofd.ShowDialog(this) != DialogResult.OK) return;

                    var path = ofd.FileName;
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

                    string encryptedPath;
                    byte[] aesKey;
                    byte[] aesIv;
                    try
                    {
                        (encryptedPath, aesKey, aesIv) = await SecureChat.Client.Services.VoiceEncryptionService.EncryptAsync(path);
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Upload error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                        return;
                    }

                    // Compute local SHA-256 before upload
                    string localSha = string.Empty;
                    try
                    {
                        localSha = await FileService.ComputeSha256Async(encryptedPath).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Không thể tính hash file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                        return;
                    }

                    // Upload file via multipart/form-data to API
                    try
                    {
                        var client = ApiClient.Create();
                        using var fs = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var content = new MultipartFormDataContent();
                        var streamContent = new StreamContent(fs);
                        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        content.Add(streamContent, "file", Path.GetFileName(encryptedPath));

                        var resp = await client.PostAsync("api/files/upload", content).ConfigureAwait(false);
                        var respStr = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                        {
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Upload failed: {respStr}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                            return;
                        }

                        // Parse response JSON
                        var opts = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                        };
                        var doc = System.Text.Json.JsonDocument.Parse(respStr);
                        var root = doc.RootElement;
                        string url = root.GetProperty("url").GetString() ?? string.Empty;
                        string fileName = Path.GetFileName(path);
                        long fileSize = root.GetProperty("fileSize").GetInt64();
                        string sha = root.TryGetProperty("sha256", out var shaEl) ? shaEl.GetString() ?? localSha : localSha;

                        // Build attachment and send message creation to server (use ApiClient.Instance to include JWT)
                        var fileNameInStorage = Path.GetFileName(url);
                        var encodedFileName = Uri.EscapeDataString(fileName);
                        var fileType = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(fileType)) fileType = "application/octet-stream";

                        // Build canonical payload and include it in Content so server-persisted message has the payload the UI expects
                        string canonicalPayload = $"file::{url}::{encodedFileName}::{fileSize}::{sha}";

                        CreateAttachmentRequest attachment;
                        try
                        {
                            attachment = await CreateHybridEncryptedAttachmentAsync(
                                _activeConvId,
                                url,
                                encodedFileName,
                                fileNameInStorage,
                                fileType,
                                sha,
                                fileSize,
                                null,
                                null,
                                null,
                                null,
                                null,
                                null,
                                aesKey,
                                aesIv);
                        }
                        catch (InvalidOperationException ex)
                        {
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                            return;
                        }
                        catch (ArgumentException ex)
                        {
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                            return;
                        }

                        var sendReq = new SendMessageRequest(
                            Type: MessageType.File,
                            Content: canonicalPayload,
                            ContentIV: null,
                            ReplyToID: null,
                            OriginalSenderID: null,
                            Attachments: new List<CreateAttachmentRequest> { attachment },
                            MentionedMemberIDs: null);

                        var (okMsg, msgRes, msgErr) = await ApiClient.Instance.PostAsync<SendMessageRequest, SecureChat.DTOs.MessageResponse>($"api/conversations/{_activeConvId}/messages", sendReq);
                        if (!okMsg || msgRes == null)
                        {
                            var errMsg = string.IsNullOrWhiteSpace(msgErr) ? "Failed to create message on server." : msgErr;
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, errMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                        }
                        else
                        {
                            // Build UI payload from returned attachment data (server returns encoded filename as we sent it)
                            var att = msgRes.Attachments != null && msgRes.Attachments.Count > 0 ? msgRes.Attachments[0] : null;
                            if (att != null)
                            {
                                if (TryTrackMessageId(msgRes.MessageID))
                                {
                                    HandleHybridEncryptedAttachment(msgRes.MessageID, att);
                                    string payload = $"file::{att.FileURL}::{att.FileName}::{att.FileSize}::{att.FileHash}";
                                    _currentMsgs.Add((msgRes.MessageID, payload, true, msgRes.SentAt.ToString("h:mm tt"), msgRes.SenderUsername ?? ""));
                                    this.BeginInvoke(new Action(() => BuildMessages()));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Upload error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                    }
                    finally
                    {
                        if (!string.IsNullOrWhiteSpace(encryptedPath) && File.Exists(encryptedPath))
                        {
                            try { File.Delete(encryptedPath); } catch { }
                        }
                    }
                }
                catch { }
            };
            _tbMessage = new TelegramTextBox { Height = 36 };
            _tbMessage.SetPlaceholder("Write a message...");
            _tbMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return && !e.Shift) { e.SuppressKeyPress = true; SendMessage(); } };

            var btnEmoji = MakeInputBtn("😊");
            var btnMic = MakeInputBtn("🎤");
            btnMic.Click += async (s, e) =>
            {
                try
                {
                    if (_audioRecorder == null) _audioRecorder = new SecureChat.Client.Services.AudioRecorderService();

                    if (!_audioRecorder.IsRecording)
                    {
                        _audioRecorder.StartRecording();
                        btnMic.Text = "■";
                    }
                    else
                    {
                        var wavPath = _audioRecorder.StopRecording();
                        btnMic.Text = "🎤";
                        // Encrypt the wav file before upload
                        try
                        {
                            var (encryptedPath, key, iv) = await SecureChat.Client.Services.VoiceEncryptionService.EncryptAsync(wavPath);
                            // Compute SHA-256 of encrypted file
                            string localSha = string.Empty;
                            try
                            {
                                localSha = await FileService.ComputeSha256Async(encryptedPath).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Không thể tính hash file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                                return;
                            }

                            // Upload encrypted file using ApiClient.Instance (JWT)
                            try
                            {
                                using var fs = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                using var content = new MultipartFormDataContent();
                                var streamContent = new StreamContent(fs);
                                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                                content.Add(streamContent, "file", Path.GetFileName(encryptedPath));

                                var (okUpload, respStr, uploadErr) = await ApiClient.Instance.PostMultipartAsync("api/files/upload", content);
                                if (!okUpload)
                                {
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Upload failed: {uploadErr}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                                    return;
                                }

                                // Parse response JSON
                                var opts = new System.Text.Json.JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true,
                                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                                };
                                var doc = System.Text.Json.JsonDocument.Parse(respStr);
                                var root = doc.RootElement;
                                string url = root.GetProperty("url").GetString() ?? string.Empty;
                                string fileName = root.GetProperty("fileName").GetString() ?? Path.GetFileName(encryptedPath);
                                long fileSize = root.GetProperty("fileSize").GetInt64();
                                string sha = root.TryGetProperty("sha256", out var shaEl) ? shaEl.GetString() ?? localSha : localSha;

                                // Duration is not computed here; set to 0 for now (can be improved later)
                                string duration = "0";
                                var fileNameInStorage = Path.GetFileName(url);
                                var encodedFileName = Uri.EscapeDataString(fileName);
                                var fileType = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
                                if (string.IsNullOrWhiteSpace(fileType)) fileType = "application/octet-stream";

                                // Build canonical voice payload
                                string canonicalPayload = $"voice::{url}::{encodedFileName}::{duration}::{sha}";

                                CreateAttachmentRequest attachment;
                                try
                                {
                                    attachment = await CreateHybridEncryptedAttachmentAsync(
                                        _activeConvId,
                                        url,
                                        encodedFileName,
                                        fileNameInStorage,
                                        fileType,
                                        sha,
                                        fileSize,
                                        null,
                                        null,
                                        null,
                                        0,
                                        null,
                                        null,
                                        key,
                                        iv);
                                }
                                catch (InvalidOperationException ex)
                                {
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                    return;
                                }
                                catch (ArgumentException ex)
                                {
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                    return;
                                }

                                var sendReq = new SendMessageRequest(
                                    Type: MessageType.File, // Or MessageType.Audio if available
                                    Content: canonicalPayload,
                                    ContentIV: null,
                                    ReplyToID: null,
                                    OriginalSenderID: null,
                                    Attachments: new List<CreateAttachmentRequest> { attachment },
                                    MentionedMemberIDs: null);

                                var (okMsg, msgRes, msgErr) = await ApiClient.Instance.PostAsync<SendMessageRequest, SecureChat.DTOs.MessageResponse>($"api/conversations/{_activeConvId}/messages", sendReq);
                                if (!okMsg || msgRes == null)
                                {
                                    var errMsg = string.IsNullOrWhiteSpace(msgErr) ? "Failed to create message on server." : msgErr;
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, errMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                }
                                else
                                {
                                    var att = msgRes.Attachments != null && msgRes.Attachments.Count > 0 ? msgRes.Attachments[0] : null;
                                    if (att != null)
                                    {
                                        if (TryTrackMessageId(msgRes.MessageID))
                                        {
                                            HandleHybridEncryptedAttachment(msgRes.MessageID, att);
                                            SecureChat.Shared.Security.KeyManager.CacheAesKey(msgRes.MessageID, key, iv);
                                            string payload = $"voice::{att.FileURL}::{att.FileName}::{duration}::{att.FileHash}";
                                            _currentMsgs.Add((msgRes.MessageID, payload, true, msgRes.SentAt.ToString("h:mm tt"), msgRes.SenderUsername ?? ""));
                                            this.BeginInvoke(new Action(() => BuildMessages()));
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Upload error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                            }
                            finally
                            {
                                if (!string.IsNullOrWhiteSpace(encryptedPath) && File.Exists(encryptedPath))
                                {
                                    try { File.Delete(encryptedPath); } catch { }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Voice encryption error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Recording error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            };
            var btnSend = new TelegramButton { Text = "↑", Height = 36, Width = 36, Font = TG.FontSemiBold(14f), Radius = 18, Visible = false };
            btnSend.Click += (s, e) => SendMessage();

            _tbMessage.TextChanged += (s, e) => { bool hasText = !string.IsNullOrWhiteSpace(_tbMessage.Text); btnSend.Visible = hasText; btnMic.Visible = !hasText; };

            // Gom nhóm ô nhập liệu vào một Panel dưới cùng
            var pnlInputControls = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.Transparent };
            pnlInputControls.Controls.AddRange(new Control[] { btnAttach, _tbMessage, btnEmoji, btnMic, btnSend });

            pnlInputControls.Resize += (s, e) =>
            {
                int y = 10;
                btnAttach.Location = new Point(8, y);
                btnEmoji.Location = new Point(Math.Max(0, pnlInputControls.Width - 84), y);
                btnMic.Location = new Point(Math.Max(0, pnlInputControls.Width - 44), y);
                btnSend.Location = new Point(Math.Max(0, pnlInputControls.Width - 44), y);
                _tbMessage.SetBounds(btnAttach.Right + 6, y, Math.Max(0, btnEmoji.Left - btnAttach.Right - 12), 36);
            };

            // Nạp cả Panel Reply và Panel Input vào Input Bar tổng
            _pnlInputBar.Controls.Add(_pnlReplyContext);
            _pnlInputBar.Controls.Add(pnlInputControls);
        }

        private Button MakeInputBtn(string icon)
        {
            var btn = new Button
            {
                Text = icon,
                // 1. Giảm nhẹ font từ 14f xuống 13f
                Font = new Font("Segoe UI Emoji", 13f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 36),
                BackColor = Color.Transparent,
                ForeColor = TG.Blue,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,

                // 2. Dùng Padding đẩy phần ruột (chữ) lên trên 4px, cách đều đáy
                Padding = new Padding(0, 0, 0, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 0, 0, 0);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 0, 0, 0);

            // (Tùy chọn) Thêm dòng này để nét render của emoji mượt và chuẩn viền hơn
            btn.UseCompatibleTextRendering = true;

            return btn;
        }

        // ════════════════════════════════════════════
        //  SETTINGS MENU (slide overlay từ trái)
        // ════════════════════════════════════════════
        private void BuildSettingsMenu()
        {
            int smw = 260;
            _pnlSettingsMenu = new Panel
            {
                BackColor = Color.White,
                Visible = true,   // luôn visible, chỉ di chuyển X
                Width = smw,
                Left = -smw,      // bắt đầu ẩn ngoài màn hình bên trái
            };
            // Border trái để phân cách với chat area
            _pnlSettingsMenu.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(TG.Divider), 0, 0, 0, _pnlSettingsMenu.Height);

            // ── Slide animation timer ─────────────────
            _slideTimer = new System.Windows.Forms.Timer { Interval = 12 };
            _slideTimer.Tick += (s, e) =>
            {
                int cur = _pnlSettingsMenu.Left;
                int target = _settingsTargetX;
                int step = (target - cur) / 3;
                if (Math.Abs(step) < 2) step = target > cur ? 2 : -2;

                int next = cur + step;
                if ((step > 0 && next >= target) || (step < 0 && next <= target))
                {
                    _pnlSettingsMenu.Left = target;
                    _slideTimer.Stop();
                    // Nếu vừa ẩn xong thì không cần làm gì thêm
                }
                else
                {
                    _pnlSettingsMenu.Left = next;
                }
            };

            // ── Header với avatar user ─────────────────
            _pnlSettingsHeader = new Panel
            {
                Height = 120,
                BackColor = TG.TitleBarBg,
                Dock = DockStyle.Top,
            };

            var btnClose = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                Font = TG.FontRegular(12f),
                ForeColor = Color.White,
                Size = new Size(36, 36),
                Location = new Point(8, 8),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => HideSettingsMenu();

            var userAvatar = new AvatarControl { Size = new Size(56, 56), Location = new Point(14, 52) };
            userAvatar.SetName(_currentDisplayName);

            var lblUserName = new Label
            {
                Text = _currentDisplayName,
                Font = TG.FontSemiBold(11f),
                ForeColor = Color.White,
                AutoSize = false,
                Height = 22,
                Location = new Point(80, 52),
                BackColor = Color.Transparent,
            };
            var lblEmojiStatus = new Label
            {
                Text = "Set Emoji Status",
                Font = TG.FontRegular(8.5f),
                ForeColor = Color.FromArgb(160, 220, 255),
                AutoSize = false,
                Height = 16,
                Location = new Point(80, 76),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };

            var btnChevron = new Button
            {
                Text = "▾",
                FlatStyle = FlatStyle.Flat,
                Font = TG.FontRegular(11f),
                ForeColor = Color.White,
                Size = new Size(28, 28),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            btnChevron.FlatAppearance.BorderSize = 0;

            // ── Add Account row (ẩn mặc định, hiện khi bấm chevron) ──
            var pnlAddAccount = new Panel
            {
                Height = 48,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false,
                Dock = DockStyle.Top,
            };
            pnlAddAccount.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                // Vẽ circle xanh với dấu +
                var rect = new Rectangle(14, 12, 24, 24);
                using var brush = new SolidBrush(TG.Blue);
                e.Graphics.FillEllipse(brush, rect);
                using var pen = new Pen(Color.White, 2f);
                e.Graphics.DrawLine(pen, 26, 18, 26, 30); // dọc
                e.Graphics.DrawLine(pen, 20, 24, 32, 24); // ngang
                // Divider
                e.Graphics.DrawLine(new Pen(TG.DividerLight), 56, 47, pnlAddAccount.Width, 47);
            };
            var lblAddAccount = new Label
            {
                Text = "Add Account",
                Font = TG.FontRegular(10f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                Height = 48,
                Location = new Point(56, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
            };
            pnlAddAccount.Controls.Add(lblAddAccount);
            pnlAddAccount.Resize += (s, e) => lblAddAccount.Width = Math.Max(0, pnlAddAccount.Width - 56);
            pnlAddAccount.MouseEnter += (s, e) => pnlAddAccount.BackColor = TG.SidebarHover;
            pnlAddAccount.MouseLeave += (s, e) => pnlAddAccount.BackColor = Color.White;
            lblAddAccount.MouseEnter += (s, e) => pnlAddAccount.BackColor = TG.SidebarHover;
            lblAddAccount.MouseLeave += (s, e) => pnlAddAccount.BackColor = Color.White;

            // Toggle chevron + hiện/ẩn Add Account
            Action toggleAccountPanel = () =>
            {
                pnlAddAccount.Visible = !pnlAddAccount.Visible;
                btnChevron.Text = pnlAddAccount.Visible ? "▲" : "▾";
            };
            btnChevron.Click += (s, e) => toggleAccountPanel();
            userAvatar.Click += (s, e) => toggleAccountPanel();
            lblUserName.Click += (s, e) => toggleAccountPanel();

            _pnlSettingsHeader.Controls.AddRange(new Control[] { btnClose, userAvatar, lblUserName, lblEmojiStatus, btnChevron });
            _pnlSettingsHeader.Resize += (s, e) =>
            {
                lblUserName.Width = _pnlSettingsHeader.Width - 84 - 36;
                lblEmojiStatus.Width = _pnlSettingsHeader.Width - 84 - 36;
                btnChevron.Location = new Point(_pnlSettingsHeader.Width - 36, 52);
            };

            // ── Menu items ────────────────────────────
            var menuItems = new (string Emoji, string Label, bool HasToggle)[]
{
    ("👤", "My Profile",      false),
    ("👥", "New Group",       false),
    ("📣", "New Channel",     false),
    ("🪪", "Contacts",        false),
    ("📞", "Calls",           false),
    ("🔖", "Saved Messages",  false),
    ("⚙️", "Settings",        false),
    ("🌙", "Night Mode",      true),
};

            var pnlMenuList = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            pnlMenuList.Resize += (s, e) =>
            {
                foreach (Control c in pnlMenuList.Controls)
                    c.Width = pnlMenuList.ClientSize.Width;
            };
            int my = 8;
            foreach (var item in menuItems)
            {
                string key = item.Label;
                bool toggleOn = _settingsToggles.TryGetValue(key, out bool v) ? v : false;

                var row = BuildSettingsRow(item.Emoji, key, item.HasToggle, toggleOn, newState => _settingsToggles[key] = newState);
                row.Location = new Point(0, my);
                row.Width = smw;
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                pnlMenuList.Controls.Add(row);
                my += 48;

                row.Click += (s, e) => OnSettingsMenuClick(key);
                foreach (Control c in row.Controls)
                    c.Click += (s, e) => OnSettingsMenuClick(key);
            }

            var lblVersion = new Label
            {
                Text = "SecureChat v1.0 · NT106 Nhóm 6",
                Font = TG.FontRegular(7.5f),
                ForeColor = TG.TextHint,
                AutoSize = false,
                Height = 20,
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
            };

            _pnlSettingsMenu.Controls.Add(pnlMenuList);
            _pnlSettingsMenu.Controls.Add(lblVersion);
            _pnlSettingsMenu.Controls.Add(pnlAddAccount);   // dock Top, nằm dưới header
            _pnlSettingsMenu.Controls.Add(_pnlSettingsHeader);
        }

        private Panel BuildSettingsRow(string emoji, string label, bool hasToggle, bool initialOn, Action<bool>? onToggle = null)
        {
            var pnl = new Panel { Height = 48, BackColor = Color.White, Cursor = Cursors.Hand };

            var lblEmoji = new Label
            {
                Text = emoji,
                Font = new Font("Segoe UI Emoji", 14f),
                Size = new Size(40, 48),
                Location = new Point(14, 0),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            var lblLabel = new Label
            {
                Text = label,
                Font = TG.FontRegular(10f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                Height = 48,
                Location = new Point(56, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
            };

            pnl.Controls.AddRange(new Control[] { lblEmoji, lblLabel });
            lblEmoji.Click += (s, e) => OnSettingsMenuClick(label);
            pnl.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(new Pen(TG.DividerLight), 56, 47, pnl.Width, 47);
            };
            pnl.Resize += (s, e) =>
            {
                // clamp width to avoid negative values when panel is very narrow
                lblLabel.Width = Math.Max(0, pnl.Width - 56 - (hasToggle ? 56 : 12));
            };

            if (hasToggle)
            {
                bool on = initialOn;
                var toggle = new Panel { Size = new Size(44, 24), BackColor = Color.Transparent, Cursor = Cursors.Hand };
                toggle.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var r = new Rectangle(0, 2, 40, 20);
                    using var brush = new SolidBrush(on ? TG.Blue : Color.FromArgb(0xCC, 0xCC, 0xCC));
                    e.Graphics.FillPath(brush, RoundedPanel.GetRoundedPath(r, 10));
                    int cx = on ? 22 : 2;
                    using var white = new SolidBrush(Color.White);
                    e.Graphics.FillEllipse(white, cx, 4, 16, 16);
                };
                toggle.Click += (s, e) =>
                {
                    on = !on;
                    onToggle?.Invoke(on); // update class-level store (or caller)
                    toggle.Invalidate();
                };
                pnl.Controls.Add(toggle);
                pnl.Resize += (s, e) => toggle.Location = new Point(Math.Max(0, pnl.Width - 52), 12);
            }

            return pnl;
        }

        private async void OnSettingsMenuClick(string label)
        {
            HideSettingsMenu();
            switch (label)
            {
                case "My Profile":
                    {
                        try
                        {
                            var profile = new SecureChat.Client.Models.ProfileModel
                            {
                                FullName = _currentDisplayName,
                                PhoneNumber = _currentEmail,
                                Username = _currentUsername,
                                AvatarPath = string.Empty,
                                StatusText = "online"
                            };
                            using var myprofile = new SecureChat.Client.Forms.Profile.frmMyProfile(profile);
                            myprofile.StartPosition = FormStartPosition.CenterParent;
                            myprofile.ShowDialog(this);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                case "New Group":
                    {
                        try
                        {
                            using var dlg = new SecureChat.Client.Forms.Chat.frmCreateGroup();
                            dlg.StartPosition = FormStartPosition.CenterParent;
                            dlg.ShowDialog(this);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                case "New Channel":
                    {
                        MessageBox.Show(this, "Create Channel feature coming soon.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                case "Contacts":
                    {
                        try
                        {
                            using var contacts = new frmContacts();
                            contacts.StartPosition = FormStartPosition.CenterParent;
                            contacts.ShowDialog(this);

                            // Nếu user bấm 💬 mở chat với bạn bè
                            if (!string.IsNullOrEmpty(contacts.PendingOpenConversationId))
                            {
                                var convId = contacts.PendingOpenConversationId;
                                if (!_convs.Any(c => c.Id == convId))
                                    await LoadConversationsAsync();

                                _activeConvId = convId;
                                BuildConvList();
                                _ = LoadMessagesAsync(convId);
                                LoadConversation(convId);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                case "Calls":
                    {
                        MessageBox.Show(this, "Calls feature coming soon.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                case "Saved Messages":
                    {
                        MessageBox.Show(this, "Saved messages feature coming soon.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    }
                case "Settings":
                    {
                        try
                        {
                            var profile = new SecureChat.Client.Models.ProfileModel { FullName = _currentDisplayName };
                            using var settings = new SecureChat.Client.Forms.Settings.frmSettings(profile);
                            settings.StartPosition = FormStartPosition.CenterParent;
                            var dr = settings.ShowDialog(this);
                            if (dr == DialogResult.No)
                            {
                                // 1. Xóa Access Token để không gọi API được nữa
                                ApiClient.Instance.SetAccessToken(null);
                                SecureChat.Shared.Security.KeyManager.Clear();
                                lock (_processedMessageIdsLock)
                                {
                                    _processedMessageIds.Clear();
                                }

                                // 2. Mở lại Form Login
                                var loginForm = new frmLoginRegister(); // Thay bằng tên Form Login của bạn
                                loginForm.Show();

                                // 3. Đóng Form Main hiện tại
                                this.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                    /*case "Night Mode":
                        {
                            bool current = _settingsToggles.TryGetValue("Night Mode", out var v) ? v : false;
                            _settingsToggles["Night Mode"] = !current;
                            UpdateThemeForNightMode(!current);
                            break;

                        }*/
            }
        }

        // ════════════════════════════════════════════
        //  SETTINGS MENU TOGGLE
        // ════════════════════════════════════════════
        private void ToggleSettingsMenu()
        {
            if (_settingsVisible) HideSettingsMenu();
            else ShowSettingsMenu();
        }


        private void ShowSettingsMenu()
        {
            _settingsVisible = true;
            int smw = _pnlSettingsMenu.Width;
            // Nếu đang ẩn hoàn toàn bên trái, đảm bảo Left = -smw để animation mượt
            if (_pnlSettingsMenu.Left <= -smw)
                _pnlSettingsMenu.Left = -smw;
            _settingsTargetX = 0; // vị trí khi hiện (dán vào mép trái của form)
            _pnlSettingsMenu.BringToFront();
            _slideTimer.Start();
        }

        private void HideSettingsMenu()
        {
            _settingsVisible = false;
            _settingsTargetX = -_pnlSettingsMenu.Width;   // trượt ra ngoài bên trái
            _slideTimer.Start();
        }

        // ════════════════════════════════════════════
        //  LOAD CONVERSATION
        // ════════════════════════════════════════════
        private void LoadConversation(string convId)
        {
            var conv = _convs.Find(c => c.Id == convId);
            if (conv == default) return;

            _activeConvId = convId;
            _chatAvatar.SetName(conv.Name);
            _lblChatName.Text = conv.Name;
            _lblChatStatus.Text = conv.IsGroup ? $"{conv.Name}" : "last seen recently";

            // Nếu chưa có tin nhắn trong cache thì load từ API, ngược lại build luôn
            if (!_allMsgs.ContainsKey(convId) || _allMsgs[convId].Count == 0)
                _ = LoadMessagesAsync(convId);
            else
                BuildMessages();
        }

        // --- Modified methods and new helpers: replace the existing BuildMessages and BuildBubble implementations
        //     and add the helper methods shown below into the frmMainChat class.

        private void BuildMessages()
        {
            // 1. CHÈN VÀO ĐÂY: Tạm dừng vẽ để tránh "nháy" khi xóa/thêm hàng loạt control
            _pnlMessages.SuspendLayout();

            // BẮT BUỘC: Reset thanh cuộn về top trước khi thao tác
            _pnlMessages.AutoScrollPosition = new Point(0, 0);

            _pnlMessages.Controls.Clear();

            int y = 8;

            // Date separator (docked to top so it resizes/centers automatically)
            var sep = new Label
            {
                Text = "March 10",
                Font = TG.FontRegular(8.5f),
                ForeColor = Color.White,
                AutoSize = false,
                Height = 24,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(8, 2, 8, 2),
            };

            sep.Location = new Point(0, y);
            sep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            sep.Width = Math.Max(0, _pnlMessages.ClientSize.Width - _pnlMessages.Padding.Horizontal);

            _pnlMessages.Controls.Add(sep);
            y += sep.Height + 4;

            var bubbles = new List<Control>();

            // Iterate with index to support actions that modify a specific message
            for (int i = 0; i < _currentMsgs.Count; i++)
            {
                var msg = _currentMsgs[i];
                bool isGroup = _convs.Find(c => c.Id == _activeConvId).IsGroup;
                var bubble = BuildBubble(msg.Text, msg.Out, msg.Time, msg.Sender, isGroup, msg.Id);

                // Make bubble respect panel resizing
                bubble.Location = new Point(0, y);
                bubble.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                bubble.Width = Math.Max(0, _pnlMessages.ClientSize.Width - _pnlMessages.Padding.Horizontal);
                _pnlMessages.Controls.Add(bubble);
                bubbles.Add(bubble);
                y += bubble.Height + 4;
            }

            _pnlMessages.ResumeLayout(true);

            if (bubbles.Count > 0)
                _pnlMessages.ScrollControlIntoView(bubbles[^1]);
        }

        // Updated BuildBubble signature: added messageIndex to identify which message the context menu acts on
        private Panel BuildBubble(string text, bool isOut, string time,
                           string sender = "", bool isGroup = false, string messageId = "")
        {
            // voice::url::name::duration::sha256
            const string voicePrefix = "voice::";
            if (!string.IsNullOrEmpty(text) && text.StartsWith(voicePrefix, StringComparison.Ordinal))
            {
                var payload = text.Substring(voicePrefix.Length);
                var parts = payload.Split(new[] { "::" }, StringSplitOptions.None);
                if (parts.Length < 4) return new Panel { BackColor = Color.Transparent };

                string url = parts.Length > 0 ? parts[0] : string.Empty;
                string fileName = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "Voice message";
                string duration = parts.Length > 2 ? parts[2] : "0";
                string expectedSha256 = parts.Length > 3 ? parts[3] : string.Empty;

                var panel = new Panel { BackColor = Color.Transparent };
                var fileCtrl = new ucFileBubble
                {
                    FileName = string.IsNullOrWhiteSpace(fileName) ? "Voice message" : fileName,
                    FileSize = $"{duration}s",
                    IsOutgoing = isOut,
                    Top = 4,
                };


                fileCtrl.FileClicked += async (s, e) =>
                {
                    // Hybrid encryption: Use KeyManager for AES key lookup
                    if (!SecureChat.Shared.Security.KeyManager.TryGetAesKey(messageId, out var key, out var iv))
                    {
                        BeginInvoke(new Action(() => MessageBox.Show(this, "Voice key not available for this message.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                        return;
                    }
                    try
                    {
                        await _voicePlaybackService.PlayAsync(url, expectedSha256, key, iv);
                    }
                    catch (Exception ex)
                    {
                        BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                    }
                };

                fileCtrl.Anchor = isOut ? AnchorStyles.Right : AnchorStyles.Left;
                fileCtrl.Width = Math.Min(360, Math.Max(220, (int)(_pnlMessages.ClientSize.Width * 0.45f)));
                panel.Height = fileCtrl.Height + 8;
                panel.Resize += (s, e) =>
                {
                    int leftOffset = (!isOut && isGroup) ? 44 : 10;
                    if (isOut)
                    {
                        fileCtrl.Left = Math.Max(10, panel.ClientSize.Width - fileCtrl.Width - 10);
                    }
                    else
                    {
                        fileCtrl.Left = leftOffset;
                    }
                };

                panel.Controls.Add(fileCtrl);

                panel.PerformLayout();
                return panel;
            }

            // file::url::name::size
            const string filePrefix = "file::";
            if (!string.IsNullOrEmpty(text) && text.StartsWith(filePrefix, StringComparison.Ordinal))
            {
                var payload = text.Substring(filePrefix.Length);
                var parts = payload.Split(new[] { "::" }, StringSplitOptions.None);
                string url = parts.Length > 0 ? parts[0] : "";
                string fileName = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                string fileSize = parts.Length > 2 ? parts[2] : "";
                // optional expected sha256 provided by server: url|fileName|fileSize|sha256
                string expectedSha256 = parts.Length > 3 ? parts[3] : string.Empty;

                var panel = new Panel { BackColor = Color.Transparent };
                var fileCtrl = new ucFileBubble
                {
                    FileName = fileName,
                    FileSize = fileSize,
                    IsOutgoing = isOut,
                    Top = 4,
                };

                fileCtrl.Anchor = isOut ? AnchorStyles.Right : AnchorStyles.Left;
                fileCtrl.Width = Math.Min(360, Math.Max(220, (int)(_pnlMessages.ClientSize.Width * 0.45f)));
                panel.Height = fileCtrl.Height + 8;
                panel.Resize += (s, e) =>
                {
                    int leftOffset = (!isOut && isGroup) ? 44 : 10;
                    if (isOut)
                    {
                        fileCtrl.Left = Math.Max(10, panel.ClientSize.Width - fileCtrl.Width - 10);
                    }
                    else
                    {
                        fileCtrl.Left = leftOffset;
                    }
                };

                fileCtrl.FileClicked += async (s, e) =>
                {
                    using var sfd = new SaveFileDialog
                    {
                        FileName = fileName,
                        Filter = "All Files|*.*",
                        Title = "Save File"
                    };

                    if (sfd.ShowDialog(this) != DialogResult.OK)
                        return;

                    var destination = sfd.FileName;
                    var cts = new CancellationTokenSource();

                    void OnCanceled(object? sender2, EventArgs e2)
                    {
                        cts.Cancel();
                    }

                    fileCtrl.DownloadCanceled += OnCanceled;

                    try
                    {
                        await fileCtrl.StartDownloadAsync(async progress =>
                        {
                            await _fileTransfer.DownloadAsync(url, destination, progress, cts.Token).ConfigureAwait(false);
                        }).ConfigureAwait(false);

                        // After successful download, if server provided expected sha256, verify integrity via FileTransferService
                        if (!string.IsNullOrWhiteSpace(expectedSha256))
                        {
                            try
                            {
                                bool ok = await _fileTransfer.VerifyAsync(destination, expectedSha256).ConfigureAwait(false);
                                if (ok)
                                {
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, "Tải xuống hoàn tất.", "Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                                }
                                else
                                {
                                    // Integrity check failed - attempt to remove the downloaded file
                                    try
                                    {
                                        if (File.Exists(destination))
                                        {
                                            try
                                            {
                                                File.Delete(destination);
                                                this.BeginInvoke(new Action(() => MessageBox.Show(this, "File tải về không khớp hash kiểm tra. File có thể đã bị thay đổi hoặc lỗi truyền tải. File đã được xóa.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                            }
                                            catch (Exception delEx)
                                            {
                                                this.BeginInvoke(new Action(() => MessageBox.Show(this, $"File tải về không khớp hash kiểm tra. Không thể xóa file: {delEx.Message}", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                            }
                                        }
                                        else
                                        {
                                            this.BeginInvoke(new Action(() => MessageBox.Show(this, "File tải về không khớp hash kiểm tra. File có thể đã bị thay đổi hoặc lỗi truyền tải.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // If anything unexpected happens, show integrity warning and include exception info
                                        this.BeginInvoke(new Action(() => MessageBox.Show(this, $"File tải về không khớp hash kiểm tra. Lỗi khi xử lý file: {ex.Message}", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // Hash computation failed — notify user but keep file
                                this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Không thể kiểm tra hash: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                            }
                        }
                        else
                        {
                            // No expected hash provided — notify download completed
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, "Tải xuống hoàn tất.", "Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (File.Exists(destination))
                        {
                            try { File.Delete(destination); } catch { }
                            this.BeginInvoke(new Action(() => MessageBox.Show(this, "Download cancelled.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                        }
                    }
                    catch (Exception ex)
                    {
                        this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Failed to download file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                    }
                    finally
                    {
                        fileCtrl.DownloadCanceled -= OnCanceled;
                        cts.Dispose();
                    }
                };

                panel.Controls.Add(fileCtrl);

                panel.PerformLayout();
                return panel;
            }

            var pnl = new Panel { BackColor = Color.Transparent };
            int pad = 12;

            const int avatarAreaW = 44;
            int leftOffset = (!isOut && isGroup) ? avatarAreaW : 10;
            int maxW = 360;
            if (_pnlMessages != null && _pnlMessages.ClientSize.Width > 0)
                maxW = Math.Max(220, (int)(_pnlMessages.ClientSize.Width * 0.66f) - _pnlMessages.Padding.Horizontal);

            // --- XỬ LÝ TEXT CHO REPLY ---
            string replySender = null;
            string replyText = null;
            string actualText = text; // Mặc định là text thường

            const string replyPrefix = "reply::";
            if (!string.IsNullOrEmpty(text) && text.StartsWith(replyPrefix))
            {
                var parts = text.Substring(replyPrefix.Length).Split(new[] { "::" }, 3, StringSplitOptions.None);
                if (parts.Length == 3)
                {
                    replySender = parts[0];
                    replyText = ExtractActualText(parts[1]); // Tránh lỗi reply lồng reply
                    actualText = parts[2];
                }
            }

            // Chỉ đo kích thước của phần chữ mới
            SizeF sz;
            using (var g = _pnlMessages.CreateGraphics())
            {
                sz = g.MeasureString(actualText, TG.FontRegular(9.5f), maxW - pad * 2);
            }

            int statusHeight = 16;
            int senderHeight = (!isOut && isGroup && !string.IsNullOrEmpty(sender)) ? 19 : 0;
            int replyBlockHeight = (replySender != null) ? 38 : 0; // Cao 38px cho khối trích dẫn

            // Đảm bảo chiều rộng bubble đủ chứa tên người reply nếu text mới quá ngắn
            int minBw = (replySender != null) ? 160 : 100;
            int bw = Math.Min(maxW, Math.Max((int)sz.Width + pad * 2 + 10, minBw));

            int bh = (int)sz.Height + pad * 2 + statusHeight + senderHeight + replyBlockHeight;
            pnl.Height = bh + 16;

            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                int x = isOut ? pnl.ClientSize.Width - bw - 10 : leftOffset;
                int y = 4;
                Color bg = isOut ? TG.MsgOutBg : TG.MsgInBg;

                // Vẽ Bóng (Shadow) và Hình nền (Bubble)
                using var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
                using var shadowPath = RoundedPanel.GetRoundedPath(new Rectangle(x + 1, y + 2, bw, bh), TG.RadiusBubble);
                e.Graphics.FillPath(shadowBrush, shadowPath);

                using var bubblePath = RoundedPanel.GetRoundedPath(new Rectangle(x, y, bw, bh), TG.RadiusBubble);
                e.Graphics.FillPath(new SolidBrush(bg), bubblePath);

                // Vẽ đuôi bong bóng
                var tail = isOut
                    ? new[] { new Point(x + bw, y + bh - 8), new Point(x + bw + 5, y + bh), new Point(x + bw, y + bh) }
                    : new[] { new Point(x, y + bh - 8), new Point(x - 5, y + bh), new Point(x, y + bh) };
                e.Graphics.FillPolygon(new SolidBrush(bg), tail);

                float currentY = y + pad;

                // 1. Vẽ Tên người gửi (Nếu là chat nhóm)
                bool showSender = !isOut && isGroup && !string.IsNullOrEmpty(sender);
                if (showSender)
                {
                    using var senderBrush = new SolidBrush(SenderNameColor(sender));
                    e.Graphics.DrawString(sender, TG.FontSemiBold(8f), senderBrush, x + pad, currentY);
                    currentY += 19;
                }

                // 2. VẼ KHỐI TRÍCH DẪN (REPLY BLOCK)
                // 2. VẼ KHỐI TRÍCH DẪN (REPLY BLOCK)
                if (replySender != null)
                {
                    // ---> MỚI: VẼ NỀN TỐI MỜ CHO KHỐI REPLY <---
                    // Dùng màu đen với độ trong suốt 15/255 để dìm màu nền hiện tại xuống một chút
                    using var replyBgBrush = new SolidBrush(Color.FromArgb(15, 0, 0, 0));
                    var replyBgRect = new Rectangle(x + pad, (int)currentY, bw - pad * 2, 34);
                    using var replyBgPath = RoundedPanel.GetRoundedPath(replyBgRect, 5); // Bo góc 5px cho mềm mại
                    e.Graphics.FillPath(replyBgBrush, replyBgPath);

                    // Viền xanh dọc
                    using var accentBrush = new SolidBrush(TG.Blue);
                    // Vẽ đè viền xanh lên mép trái của nền vừa tạo, thò thụt 2px cho đẹp
                    e.Graphics.FillRectangle(accentBrush, x + pad, currentY + 2, 3, 30);

                    // Tên người được trả lời
                    using var replySenderBrush = new SolidBrush(TG.Blue);
                    e.Graphics.DrawString(replySender, TG.FontSemiBold(8.5f), replySenderBrush, x + pad + 8, currentY);

                    // Text cũ (rút gọn nếu dài)
                    var replyTextRect = new RectangleF(x + pad + 8, currentY + 16, bw - pad * 2 - 12, 16);
                    using var sfReply = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    e.Graphics.DrawString(replyText, TG.FontRegular(8.5f), new SolidBrush(TG.TextSecondary), replyTextRect, sfReply);

                    // Tăng khoảng cách đẩy Y xuống cho text mới thoáng hơn
                    currentY += 40;
                }

                // 3. Text tin nhắn chính (Sử dụng actualText)
                var textRect = new RectangleF(x + pad, currentY, bw - pad * 2, sz.Height);
                e.Graphics.DrawString(actualText, TG.FontRegular(9.5f), new SolidBrush(TG.TextPrimary), textRect);

                // 4. Thời gian và dấu tick ✓✓
                var timeSz = e.Graphics.MeasureString(time, TG.FontRegular(7.5f));
                float tx = x + bw - timeSz.Width - pad - (isOut ? 26 : 0);
                float ty = y + bh - timeSz.Height - 6;
                e.Graphics.DrawString(time, TG.FontRegular(7.5f), new SolidBrush(TG.TextTime), tx, ty);

                if (isOut)
                {
                    float tickX = x + bw - pad - 22;
                    using var tickFont = new Font("Segoe UI Symbol", 8f, FontStyle.Bold);
                    e.Graphics.DrawString("✓✓", tickFont, new SolidBrush(TG.Blue), tickX, ty - 1);
                }
            };

            // Tạo Context Menu
            var actions = new SecureChat.Client.Forms.Chat.MessageActions
            {
                Reply = id => OnReplyMessage(id),
                Forward = id => OnForwardMessage(id),
                Copy = id => OnCopyMessage(id),
                Edit = isOut ? (id => OnEditMessage(id)) : null,
                Pin = id => OnPinMessage(id),
                React = id => OnReactMessage(id),
                Delete = id => OnDeleteMessage(id, isOut)
            };
            pnl.ContextMenuStrip = SecureChat.Client.Forms.Chat.frmRightClickMessageMenu.Create(messageId, actions, null);

            if (!isOut && isGroup)
            {
                var av = new AvatarControl { Size = new Size(32, 32), Location = new Point(6, 4) };
                av.SetName(sender);
                pnl.Controls.Add(av);
            }

            return pnl;
        }

        // --- Helper action implementations ---



        private (DialogResult Result, bool IsChecked) ShowTelegramDialog(string title, string checkboxText, string confirmText, Color confirmColor)
        {
            using var dlg = new Form
            {
                Text = title,
                Size = new Size(320, 180), // Tăng chiều cao lên chút
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowIcon = false,
                BackColor = Color.White,
                Font = TG.FontRegular(10f)
            };

            // Xóa Title rườm rà bên trong, chỉ để chữ ở Form Header (Text = title)

            var chkAlso = new CheckBox
            {
                Text = checkboxText,
                Location = new Point(20, 30), // Đẩy lên trên
                AutoSize = true,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Standard // Giúp checkbox phẳng hơn
            };

            // Nút Cancel
            var btnCancel = new Button
            {
                Text = "CANCEL", // Đổi thành in hoa
                Location = new Point(120, 90),
                Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat,
                ForeColor = TG.Blue,
                Cursor = Cursors.Hand,
                Font = TG.FontSemiBold(9.5f)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(10, TG.Blue); // Hover mờ mờ
            btnCancel.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, TG.Blue);
            btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; };

            // Nút Confirm
            var btnConfirm = new Button
            {
                Text = confirmText.ToUpper(), // In hoa
                Location = new Point(210, 90),
                Size = new Size(80, 36),
                FlatStyle = FlatStyle.Flat,
                ForeColor = confirmColor,
                Cursor = Cursors.Hand,
                Font = TG.FontSemiBold(9.5f)
            };
            btnConfirm.FlatAppearance.BorderSize = 0;
            btnConfirm.FlatAppearance.MouseOverBackColor = Color.FromArgb(10, confirmColor);
            btnConfirm.FlatAppearance.MouseDownBackColor = Color.FromArgb(20, confirmColor);
            btnConfirm.Click += (s, e) => { dlg.DialogResult = DialogResult.Yes; };

            // Mẹo xóa viền Focus của WinForms: Focus vào 1 label ẩn
            var hiddenFocus = new Label { Size = new Size(0, 0) };
            dlg.Shown += (s, e) => hiddenFocus.Focus();

            dlg.Controls.AddRange(new Control[] { chkAlso, btnCancel, btnConfirm, hiddenFocus });
            dlg.AcceptButton = btnConfirm;
            dlg.CancelButton = btnCancel;

            var result = dlg.ShowDialog(this);
            return (result, chkAlso.Checked);
        }



        // Lưu ý: Nếu báo lỗi ở InputBox, bạn cần click chuột phải vào project Client -> Add -> Reference -> Tick chọn Microsoft.VisualBasic nhé
        private async void OnForwardMessage(string messageId)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            var msg = _currentMsgs[msgIndex];

            // Mở Form hiển thị danh sách hội thoại
            using var dlg = new SecureChat.Client.Forms.Chat.frmForwardMessage(_convs);
            if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedConversationId))
                return;

            string targetConversationId = dlg.SelectedConversationId;

            // Gói dữ liệu gửi lên Server
            var req = new SendMessageRequest(
                Type: MessageType.Text,
                Content: $"[Forwarded] {msg.Text}", // Đánh dấu là tin nhắn chuyển tiếp
                ContentIV: "TBD",
                ReplyToID: null,
                OriginalSenderID: msg.Sender,
                Attachments: null,
                MentionedMemberIDs: null
            );

            // Gọi API post tới cuộc trò chuyện được chọn
            var (ok, _, err) = await ApiClient.Instance.PostAsync<SendMessageRequest, MessageResponse>($"api/conversations/{targetConversationId}/messages", req);
            if (ok)
            {
                MessageBox.Show(this, "Đã chuyển tiếp tin nhắn thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, $"Lỗi forward: {err}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnReplyMessage(string messageId)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            var msg = _currentMsgs[msgIndex];
            _replyingToMessageId = msg.Id;

            _lblReplySender.Text = string.IsNullOrEmpty(msg.Sender) ? "You" : msg.Sender;

            // GỌI HÀM EXTRACT ĐỂ HIỂN THỊ TEXT TRONG THANH REPLY
            _lblReplyText.Text = ExtractActualText(msg.Text);

            _pnlReplyContext.Visible = true;
            _pnlInputBar.Height = 56 + 44;
            _tbMessage.Focus();
        }

        private void OnCopyMessage(string messageId)
        {
            var msg = _currentMsgs.Find(m => m.Id == messageId);
            if (msg.Id != null) Clipboard.SetText(ExtractActualText(msg.Text));
        }

        private void OnEditMessage(string messageId)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            var msg = _currentMsgs[msgIndex];
            if (!msg.Out) return;

            // HIỂN THỊ TEXT ĐÃ LỌC CHO NGƯỜI DÙNG SỬA
            if (TryShowEditDialog(ExtractActualText(msg.Text), out var newText))
            {
                // Kiểm tra xem tin gốc có phải là tin reply không để giữ lại phần quote
                string finalNewText = newText;
                if (msg.Text.StartsWith("reply::"))
                {
                    var parts = msg.Text.Substring(7).Split(new[] { "::" }, 3, StringSplitOptions.None);
                    if (parts.Length == 3) finalNewText = $"reply::{parts[0]}::{parts[1]}::{newText}";
                }

                _currentMsgs[msgIndex] = (msg.Id, finalNewText, msg.Out, msg.Time, msg.Sender);
                BuildMessages();
            }
        }

        private async void OnDeleteMessage(string messageId, bool isOut)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            bool deleteForEveryone = false;

            if (isOut)
            {
                var dialog = ShowTelegramDialog("Delete message?", "Also delete for other user", "Delete", Color.FromArgb(0xE2, 0x4B, 0x4A));
                if (dialog.Result != DialogResult.Yes) return;
                deleteForEveryone = dialog.IsChecked;
            }
            else
            {
                var dialog = ShowTelegramDialog("Delete message?", "Delete just for me", "Delete", Color.FromArgb(0xE2, 0x4B, 0x4A));
                if (dialog.Result != DialogResult.Yes) return;
            }

            if (deleteForEveryone)
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.DeleteAsync($"api/conversations/{_activeConvId}/messages/{messageId}");
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show(this, $"Lỗi thu hồi: {err}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _currentMsgs.RemoveAt(msgIndex);
            BuildMessages();
        }

        private async void OnPinMessage(string messageId)
        {
            var dialog = ShowTelegramDialog("Pin message?", "Pin for both sides", "Pin", TG.Blue);
            if (dialog.Result != DialogResult.Yes) return;

            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            var res = await http.PostAsync(
                $"api/conversations/{_activeConvId}/messages/{messageId}/pin",
                null);

            if (res.IsSuccessStatusCode)
                MessageBox.Show(this, "Đã ghim tin nhắn.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                MessageBox.Show(this, $"Lỗi ghim: {err}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnReactMessage(string messageId)
        {
            MessageBox.Show("Tính năng thả cảm xúc (React) đang được phát triển!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private bool TryTrackMessageId(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return false;

            lock (_processedMessageIdsLock)
            {
                return _processedMessageIds.Add(messageId);
            }
        }

        // Simple modal edit dialog (returns true if user clicked OK)
        private bool TryShowEditDialog(string currentText, out string newText)
        {
            newText = currentText;
            using var dlg = new Form()
            {
                Text = "Edit message",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Size = new Size(480, 200)
            };

            var tb = new TextBox()
            {
                Multiline = true,
                Text = currentText,
                Dock = DockStyle.Top,
                Height = 100,
                Font = TG.FontRegular(9.5f)
            };

            var btnOk = new Button() { Text = "OK", DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            var btnCancel = new Button() { Text = "Cancel", DialogResult = DialogResult.Cancel, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };

            btnOk.Location = new Point(dlg.ClientSize.Width - 180, tb.Bottom + 20);
            btnCancel.Location = new Point(dlg.ClientSize.Width - 90, tb.Bottom + 20);

            dlg.Controls.Add(tb);
            dlg.Controls.Add(btnOk);
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;

            var dr = dlg.ShowDialog(this);
            if (dr == DialogResult.OK)
            {
                newText = tb.Text;
                return true;
            }
            return false;
        }

        private async Task<CreateAttachmentRequest> CreateHybridEncryptedAttachmentAsync(
            string receiverId,
            string fileUrl,
            string fileName,
            string fileNameInStorage,
            string fileType,
            string fileHash,
            long fileSize,
            int? width,
            int? height,
            string? thumbnailUrl,
            int? durationSecs,
            string? fileIv,
            string? thumbnailIv,
            byte[] aesKey,
            byte[] aesIv)
        {
            if (string.IsNullOrWhiteSpace(receiverId))
                throw new InvalidOperationException("Receiver id is required.");
            if (string.IsNullOrWhiteSpace(fileUrl))
                throw new InvalidOperationException("File URL is required.");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new InvalidOperationException("File name is required.");
            if (string.IsNullOrWhiteSpace(fileType))
                throw new InvalidOperationException("File type is required.");
            ArgumentNullException.ThrowIfNull(aesKey);
            ArgumentNullException.ThrowIfNull(aesIv);
            if (aesKey.Length == 0)
                throw new InvalidOperationException("AES key is required.");
            if (aesIv.Length == 0)
                throw new InvalidOperationException("AES IV is required.");

            var receiverPublicKey = await ApiClient.Instance.GetPublicKeyAsync(receiverId);
            if (string.IsNullOrWhiteSpace(receiverPublicKey))
                throw new InvalidOperationException("Receiver public key not available.");

            byte[] encryptedAesKey = SecureChat.Shared.Security.RSAEncryption.Encrypt(aesKey, receiverPublicKey);
            byte[] encryptedAesIv = SecureChat.Shared.Security.RSAEncryption.Encrypt(aesIv, receiverPublicKey);

            return new CreateAttachmentRequest(
                fileUrl,
                fileName,
                fileNameInStorage,
                fileType,
                fileHash,
                fileSize,
                width,
                height,
                thumbnailUrl,
                durationSecs,
                fileIv,
                thumbnailIv,
                Convert.ToBase64String(encryptedAesKey),
                Convert.ToBase64String(encryptedAesIv),
                receiverId);
        }

        private void HandleHybridEncryptedAttachment(string messageId, AttachmentResponse attachment)
        {
            if (string.IsNullOrWhiteSpace(messageId) || attachment is null)
                return;
            if (!string.IsNullOrWhiteSpace(attachment.ReceiverId) && attachment.ReceiverId != _currentUserId)
                return;
            if (string.IsNullOrWhiteSpace(attachment.EncryptedAesKey) || string.IsNullOrWhiteSpace(attachment.EncryptedAesIv))
                return;

            var (_, privateKey) = SecureChat.Shared.Security.KeyManager.GetKeyPair();
            if (string.IsNullOrWhiteSpace(privateKey))
                return;

            try
            {
                byte[] aesKey = SecureChat.Shared.Security.RSAEncryption.Decrypt(Convert.FromBase64String(attachment.EncryptedAesKey), privateKey);
                byte[] aesIv = SecureChat.Shared.Security.RSAEncryption.Decrypt(Convert.FromBase64String(attachment.EncryptedAesIv), privateKey);
                SecureChat.Shared.Security.KeyManager.CacheAesKey(messageId, aesKey, aesIv);
            }
            catch (FormatException ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
        }

        // ════════════════════════════════════════════
        //  SEND MESSAGE
        // ════════════════════════════════════════════
        private void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(_tbMessage.Text)) return;
            string text = _tbMessage.Text.Trim();
            _tbMessage.Text = "";

            string finalMessageText = text;

            // Nếu đang trong chế độ Reply, lấy thông tin tin nhắn cũ gộp vào
            if (!string.IsNullOrEmpty(_replyingToMessageId))
            {
                var origMsg = _currentMsgs.Find(m => m.Id == _replyingToMessageId);
                if (origMsg.Id != null) // Tồn tại
                {
                    string origSender = string.IsNullOrEmpty(origMsg.Sender) ? "You" : origMsg.Sender;
                    // Format: reply::SenderName::OriginalText::NewText
                    finalMessageText = $"reply::{origSender}::{origMsg.Text}::{text}";
                }
            }

            _currentMsgs.Add((Guid.NewGuid().ToString(), finalMessageText, true, DateTime.Now.ToString("h:mm tt"), ""));

            // Dọn dẹp giao diện sau khi gửi
            _pnlReplyContext.Visible = false;
            _pnlInputBar.Height = 56;
            _replyingToMessageId = null;

            BuildMessages();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _slideTimer?.Stop();
            _slideTimer?.Dispose();
            _wallpaperWatcher?.Dispose();
            _wallpaper?.Dispose();
            _chatMoreMenu?.Dispose();
            SecureChat.Shared.Security.KeyManager.Clear();
            lock (_processedMessageIdsLock)
            {
                _processedMessageIds.Clear();
            }
            base.OnFormClosed(e);
        }

        private sealed class ChatMenuColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(0xF2, 0xF5, 0xF9);
            public override Color MenuItemBorder => Color.FromArgb(0xD9, 0xE1, 0xEB);
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color SeparatorDark => Color.FromArgb(0xEA, 0xEE, 0xF3);
            public override Color SeparatorLight => Color.FromArgb(0xEA, 0xEE, 0xF3);
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
        }

        private Image LoadAndTintIcon(string fileName, Color tint)
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "Resources", "Icons", fileName);
                if (!File.Exists(path)) return null;

                // Load into memory to avoid file locks
                byte[] data = File.ReadAllBytes(path);
                using var ms = new MemoryStream(data);
                using var srcImg = Image.FromStream(ms);

                // Ensure we have a 32bpp ARGB bitmap (preserve alpha)
                var src = new Bitmap(srcImg.Width, srcImg.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(src))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(srcImg, new Rectangle(0, 0, srcImg.Width, srcImg.Height));
                }

                // Prepare result bitmap
                var result = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(result))
                using (var attr = new ImageAttributes())
                {
                    // Multiply each color channel by tint (preserves icon alpha)
                    float r = tint.R / 255f;
                    float gC = tint.G / 255f;
                    float b = tint.B / 255f;

                    var matrix = new ColorMatrix(new float[][]
                    {
                new float[] { r, 0, 0, 0, 0 },
                new float[] { 0, gC, 0, 0, 0 },
                new float[] { 0, 0, b, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 }
                    });

                    attr.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    g.Clear(Color.Transparent);
                    g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                                0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
                }

                src.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAndTintIcon failed for '{fileName}': {ex}");
                return null;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Bật WS_EX_COMPOSITED (0x02000000)
                // Ép toàn bộ control trên form phải vẽ bằng Double Buffer
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        private static readonly Color[] _senderPalette =
{
    Color.FromArgb(0xE5, 0x55, 0x45),
    Color.FromArgb(0xF4, 0x8C, 0x4A),
    Color.FromArgb(0x70, 0xBB, 0x4D),
    Color.FromArgb(0x20, 0x9E, 0xD9),
    Color.FromArgb(0x9B, 0x59, 0xB6),
    Color.FromArgb(0xE9, 0x67, 0xA8),
    Color.FromArgb(0x17, 0xA5, 0x89),
};

        private static Color SenderNameColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return _senderPalette[0];
            int hash = 0;
            foreach (char c in name) hash = hash * 31 + c;
            return _senderPalette[Math.Abs(hash) % _senderPalette.Length];
        }

        internal class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true); // Bỏ SupportsTransparentBackColor
                UpdateStyles();
            }
        }

        private Bitmap _cachedBackground;

        private void UpdateCachedBackground()
        {
            // Kiểm tra nếu Panel chưa có kích thước hoặc bị ẩn thì không làm gì cả
            if (_pnlMessages.Width <= 0 || _pnlMessages.Height <= 0) return;

            // 1. Tạo một Bitmap mới khớp hoàn toàn với kích thước hiện tại của Panel
            Bitmap newBmp = new Bitmap(_pnlMessages.Width, _pnlMessages.Height);

            using (Graphics g = Graphics.FromImage(newBmp))
            {
                // 2. Tô nền bằng màu mặc định trước (phòng trường hợp ảnh không phủ hết hoặc lỗi)
                g.Clear(Color.FromArgb(0xDB, 0xE8, 0xD5));

                // 3. Lấy ảnh wallpaper (sử dụng hàm LoadWallpaper bạn đã viết)
                var img = LoadWallpaper();
                if (img != null)
                {
                    // Sử dụng lại logic "Center Crop" chuyên nghiệp của bạn
                    float scaleX = (float)_pnlMessages.Width / img.Width;
                    float scaleY = (float)_pnlMessages.Height / img.Height;
                    float scale = Math.Max(scaleX, scaleY);

                    int drawW = (int)(img.Width * scale);
                    int drawH = (int)(img.Height * scale);

                    int offsetX = (_pnlMessages.Width - drawW) / 2;
                    int offsetY = (_pnlMessages.Height - drawH) / 2;

                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;

                    g.DrawImage(img, offsetX, offsetY, drawW, drawH);
                }
            }

            // 4. Cập nhật BackgroundImage cho Panel
            // Giải phóng ảnh cũ để tránh tràn bộ nhớ (Memory Leak)
            var oldBmp = _pnlMessages.BackgroundImage;
            // Xóa 2 dòng code này đi:
            // _pnlMessages.BackgroundImage = newBmp;
            // _pnlMessages.BackgroundImageLayout = ImageLayout.None;

            // Thay bằng 2 dòng này:
            _pnlMessages.CachedWallpaper = newBmp;
            _pnlMessages.Invalidate(); // Ra lệnh cho Panel vẽ lại nền lập tức

            if (oldBmp != null) oldBmp.Dispose();
        }

    }

    public class ChatPanel : Panel
    {
        public Bitmap CachedWallpaper { get; set; }

        public ChatPanel()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (CachedWallpaper != null)
            {
                var state = e.Graphics.Save();
                e.Graphics.ResetTransform();
                e.Graphics.DrawImage(CachedWallpaper, 0, 0);
                e.Graphics.Restore(state);
            }
            else
            {
                var state = e.Graphics.Save();
                e.Graphics.ResetTransform();
                e.Graphics.Clear(this.BackColor);
                e.Graphics.Restore(state);
            }
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            const int WM_ERASEBKGND = 0x0014;
            const int WM_VSCROLL = 0x0115;
            const int WM_HSCROLL = 0x0114;
            const int WM_MOUSEWHEEL = 0x020A;

            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL)
            {
                this.Invalidate();
            }
        }
    }
}