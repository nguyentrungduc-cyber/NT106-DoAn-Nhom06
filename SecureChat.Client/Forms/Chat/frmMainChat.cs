using SecureChat.Client.Components.Chat;
using SecureChat.Client.Forms.Profile;
using SecureChat.Client.Services;
using SecureChat.DTOs;
using SecureChat.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;   // List<>, Dictionary<>
using System.Drawing;               // Color, Point, Size, Image, Bitmap
using System.Drawing.Drawing2D;     // SmoothingMode, LinearGradientBrush
using System.Drawing.Imaging;       // PixelFormat, ColorMatrix, ImageAttributes
using System.Linq;
using System.IO;                    // File, Path, MemoryStream, FileSystemWatcher
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;       // Task.Delay (dùng cho wallpaper reload)
using System.Windows.Forms;         // Form, Panel, Button, Label, ...
using SecureChat.Client.Forms.Chat;
using SecureChat.Client.Components.Group;

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
        private Panel _pnlEmptyState; // trạng thái trống khi chưa có cuộc trò chuyện
        private Label _lblEmptyState;
        private Button _btnNewMessage;

        // ── Chat area controls ─────────────────────

        private Panel _pnlChatHeader; // thanh header trên cùng khu chat

        //private Panel _pnlMessages; // vùng hiển thị bong bóng tin nhắn
        private ChatPanel _pnlMessages;
        private readonly VoicePlaybackService _voicePlaybackService = new(); // shared singleton – passed into each ucAudioBubble

        private Panel _pnlInputBar; // thanh nhập tin nhắn bên dưới
        private TelegramTextBox _tbMessage; // TextBox gõ tin nhắn
        private Label _lblChatName, _lblChatStatus; // tên và trạng thái người nhận
        private AvatarControl _chatAvatar; //  avatar tròn người nhận
        private Panel _pnlChatEmpty;
        private Label _lblChatEmpty;

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
        private SecureChat.Client.Services.RealTime.SignalRClient? _signalRClient;

        private readonly HashSet<string> _processedMessageIds = new();
        private readonly object _processedMessageIdsLock = new();
        private readonly HashSet<string> _hiddenMessageIds = new();

        // Typing indicator state
        private System.Windows.Forms.Timer? _typingDebounceTimer;
        private string? _typingDebounceConvId;
        private DateTime _lastTypingSent = DateTime.MinValue;
        private readonly Dictionary<string, HashSet<string>> _typingUsernames = new();
        private readonly object _typingLock = new();

        // Sidebar header controls (updated after user data loads)
        private AvatarControl _settingsAvatar;
        private Label _lblSettingsUserName;

        // Sync tin nhắn từ MariaDB (server) -> Client
        private readonly SecureChat.Client.Services.Api.MessageService _messageService = new();
        private readonly SecureChat.Client.Services.MessageDecryptor _decryptor = new();
        private readonly Dictionary<string, string> _myMemberIdByConv = new();
        private readonly HashSet<string> _syncedConversations = new();
        private const int MessageSyncPageSize = 50;


        // ── Settings menu controls ─────────────────
        private Panel _pnlSettingsHeader;

        // ── Conversation data ──────────────────────────────
        private string _activeConvId = string.Empty;
        private string _currentUserId = string.Empty;
        private string _currentDisplayName = string.Empty;
        private string _currentUsername = string.Empty;
        private string _currentEmail = string.Empty;

        private readonly List<(string Id, string Name, string Preview, string Time, int Unread, bool IsGroup)> _convs = new();

        private readonly Dictionary<string, Panel> _convRowCache = new();

        // Key = convId, Value = danh sách tin nhắn của conversation đó
        // Thêm biến lưu trạng thái trả lời tin nhắn
        private string _replyingToMessageId = null;

        // Self-destruct timer (seconds) - null means no self-destruct
        private int? _selfDestructSeconds = null;
        private Label _lblSelfDestructTimer;

        // Message expiration service
        private readonly MessageExpirationService _expirationService = new();

        // Timer to refresh UI for countdown display
        private System.Windows.Forms.Timer? _countdownRefreshTimer;

        // Local audio recorder service (NAudio)
        private SecureChat.Client.Services.AudioRecorderService? _audioRecorder;

        // Khai báo Dictionary với Tuple 5 tham số (Thêm Id ở đầu)

        // Sau khi sync từ MariaDB, dictionary này được điền runtime
        // bằng MessageDecryptor.ProcessAsync (E2EE: server không thấy plaintext).

        private readonly Dictionary<string, List<(string Id, string Text, bool Out, string Time, string Sender)>> _allMsgs = new();
        // Track delivery status cho từng messageId
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SecureChat.DTOs.DeliveryStatus> _msgDelivery = new();
        private readonly Dictionary<string, DateTime> _messageDates = new();



        // Cập nhật lại _currentMsgs sang Tuple 5 tham số
        // Lưu ý: trả về list được lưu trong _allMsgs để các thao tác Add/Update
        // (gửi tin, nhận realtime) được phản ánh trực tiếp.
        private List<(string Id, string Text, bool Out, string Time, string Sender)> _currentMsgs
        {
            get
            {
                if (string.IsNullOrEmpty(_activeConvId))
                    return new List<(string, string, bool, string, string)>();

                if (!_allMsgs.TryGetValue(_activeConvId, out var list))
                {
                    list = new List<(string Id, string Text, bool Out, string Time, string Sender)>();
                    _allMsgs[_activeConvId] = list;
                }
                return list;
            }
        }

        private readonly Dictionary<string, bool> _settingsToggles = new();
        private readonly ConcurrentDictionary<string, string> _senderDisplayNameMap = new();
        private readonly ConcurrentDictionary<string, string> _usernameToUserId = new();
        private readonly ConcurrentDictionary<string, string> _forwardMetadata = new();
        private readonly ConcurrentDictionary<string, string> _forwardOriginalSenderId = new();
        private readonly HashSet<string> _pinnedMessageIds = new();
        private Panel _pnlPinnedBar = null!;
        private Label _lblPinnedText = null!;
        private Button _btnUnpin = null!;
        private Panel _pnlPinnedPopup = null!;
        private Panel _pnlPinnedBottomBar = null!;
        private Label _lblPinnedBottomText = null!;
        private bool _isPinnedPopupOpen = false;

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
                        _decryptor.CurrentUserId = me.UserID;
                        _decryptor.CurrentUsername = me.Username;
                    }
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }

            UpdateSettingsHeaderUI();

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
            // Sync danh sách conversation từ MariaDB.
            await SyncConversationsAsync();

            await InitializeSignalRAsync();

            // Start message expiration service
            _expirationService.MessageExpired += OnMessageExpired;
            _expirationService.Start();

            // Start countdown refresh timer (refresh UI every second to update countdown display)
            _countdownRefreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _countdownRefreshTimer.Tick += (s, e) =>
            {
                // Only refresh if there are tracked messages and active conversation
                if (_expirationService.TrackedMessageCount > 0 && !string.IsNullOrWhiteSpace(_activeConvId))
                {
                    BeginInvoke(new Action(() => _pnlMessages?.Invalidate()));
                }
            };
            _countdownRefreshTimer.Start();

            // Load danh sách hội thoại từ API thật
            await LoadConversationsAsync();
        }

        private async Task LoadConversationsAsync()
        {
            try
            {
                var (ok, list, _) = await _messageService.GetMyConversationsAsync();
                if (!ok || list is null) return;

                BeginInvoke(new Action(() =>
                {
                    // Lưu preview hiện tại trước khi clear
                    var existingPreviews = new Dictionary<string, string>();
                    var existingTimes = new Dictionary<string, string>();
                    foreach (var ec in _convs)
                    {
                        existingPreviews[ec.Id] = ec.Preview;
                        existingTimes[ec.Id] = ec.Time;
                    }

                    _convs.Clear();
                    foreach (var c in list)
                    {
                        bool isGroup = c.Type == SecureChat.Models.ConversationType.Group;
                        string time = c.LastActivityAt.HasValue
                            ? c.LastActivityAt.Value.ToLocalTime().ToString("h:mm tt")
                            : c.CreatedAt.ToLocalTime().ToString("h:mm tt");

                        // Giữ preview cũ nếu có, nếu không thì dùng ... nếu có LastMessageID
                        string preview = existingPreviews.TryGetValue(c.ConversationID, out var oldPreview)
                            ? oldPreview
                            : (c.LastMessageID != null ? "..." : string.Empty);

                        // Giữ thời gian cũ nếu không có LastActivityAt mới
                        if (c.LastActivityAt == null && existingTimes.TryGetValue(c.ConversationID, out var oldTime))
                            time = oldTime;

                        _convs.Add((c.ConversationID, c.Name ?? "Conversation", preview, time, 0, isGroup));
                    }
                    BuildConvList();
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
                    // Cache AES key từ attachment nếu có
                    if (m.Attachments != null)
                    {
                        foreach (var att in m.Attachments)
                            HandleHybridEncryptedAttachment(m.MessageID, att);
                    }

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
                    if (!isOut && !string.IsNullOrEmpty(sender) && !string.IsNullOrEmpty(m.SenderDisplayName))
                        _senderDisplayNameMap[sender] = m.SenderDisplayName;
                    if (!string.IsNullOrEmpty(sender) && !string.IsNullOrEmpty(m.SenderUserID))
                        _usernameToUserId[sender] = m.SenderUserID;
                    if (!string.IsNullOrEmpty(m.OriginalSenderID) && !string.IsNullOrEmpty(m.OriginalSenderName))
                    {
                        _forwardMetadata[m.MessageID] = m.OriginalSenderName;
                        _forwardOriginalSenderId[m.MessageID] = m.OriginalSenderID!;
                    }
                    _messageDates[m.MessageID] = m.SentAt.ToLocalTime();
                    _allMsgs[convId].Add((m.MessageID, text, isOut, time, sender));
                    if (isOut)
                        _msgDelivery[m.MessageID] = m.Delivery;
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

            // Thứ tự add: chat → right sidebar → sidebar → settings (settings ở trên cùng)
            Controls.Add(_pnlChat);
            Controls.Add(_pnlRightSidebar);
            Controls.Add(_pnlSidebar);
            Controls.Add(_pnlSettingsMenu);  // add cuối = hiện trên cùng

            Resize += (s, e) =>
            {
                AdjustLayout();
                UpdateCachedBackground(); // Thêm dòng này để ảnh nền co giãn theo Form
            };
            AdjustLayout();

            // Load mock data trước để UI có nội dung đẹp ngay khi mở form.
            // Sau khi SyncConversationsAsync (trong FrmMainChat_Load) chạy xong:
            //  - Nếu server có conversation thật -> mock bị replace.
            //  - Nếu server trả rỗng / lỗi -> UI vẫn giữ mock (mark là 'đã sync')
            //    để không gọi API per-conv (sẽ 401/404).
            LoadConversation(_activeConvId);

            // Danh sách hội thoại sẽ được load từ API trong FrmMainChat_Load

            SetupWallpaperWatcher(); // 1. Bắt đầu theo dõi thư mục ảnh

            // 2. Nạp hình nền lần đầu tiên
            // Nên để sau AdjustLayout để _pnlChat và _pnlMessages đã có kích thước chuẩn
            UpdateCachedBackground();
        }

        private void AdjustLayout()
        {
            int sw = 300;                        // Sidebar Width
            int smw = 260;                       // Settings Menu Width
            int rsw = _isSidebarOpen ? 300 : 0;  // Right Sidebar Width

            _pnlSidebar.SetBounds(0, 0, sw, ClientSize.Height);
            _pnlChat.SetBounds(sw, 0, ClientSize.Width - sw - rsw, ClientSize.Height);

            if (_isSidebarOpen)
            {
                _pnlRightSidebar.SetBounds(
                    ClientSize.Width - 300, 0, 300, ClientSize.Height);
                _pnlRightSidebar.Visible = true;
            }
            else
            {
                _pnlRightSidebar.Visible = false;
            }

            if (!_settingsVisible)
                _pnlSettingsMenu.SetBounds(-smw, 0, smw, ClientSize.Height);
            else
                _pnlSettingsMenu.SetBounds(0, 0, smw, ClientSize.Height);
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

            _pnlEmptyState = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Visible = false };
            _lblEmptyState = new Label
            {
                Text = "You have no conversations yet.",
                Font = TG.FontSemiBold(10f),
                ForeColor = TG.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            _btnNewMessage = new Button
            {
                Text = "New Message",
                FlatStyle = FlatStyle.Flat,
                BackColor = TG.Blue,
                ForeColor = Color.White,
                Font = TG.FontSemiBold(10f),
                Size = new Size(220, 38),
                Cursor = Cursors.Hand
            };
            _btnNewMessage.FlatAppearance.BorderSize = 0;
            _btnNewMessage.Click += async (s, e) =>
            {
                try
                {
                    await OpenNewMessageAsync();
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(this, ex.Message, "New message", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            _pnlEmptyState.Controls.Add(_lblEmptyState);
            _pnlEmptyState.Controls.Add(_btnNewMessage);
            _pnlEmptyState.Resize += (_, __) => LayoutEmptyState();

            BuildConvList();

            _pnlSidebar.Controls.Add(_pnlEmptyState);
            _pnlSidebar.Controls.Add(_pnlConvList);
            _pnlSidebar.Controls.Add(pnlSearch);
            _pnlSidebar.Controls.Add(pnlHeader);

            UpdateEmptyStateUI();
        }

        private void BuildConvList()
        {
            _convRowCache.Clear();
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

            UpdateEmptyStateUI();
        }

        private void UpdateEmptyStateUI()
        {
            bool hasConversations = _convs.Count > 0;
            _pnlConvList.Visible = hasConversations;
            _pnlEmptyState.Visible = !hasConversations;

            if (!hasConversations)
                LayoutEmptyState();
        }

        private void LayoutEmptyState()
        {
            if (_pnlEmptyState.Width == 0 || _pnlEmptyState.Height == 0)
                return;

            _lblEmptyState.Location = new Point(
                Math.Max(0, (_pnlEmptyState.Width - _lblEmptyState.Width) / 2),
                Math.Max(0, (_pnlEmptyState.Height / 2) - _lblEmptyState.Height - 20));

            _btnNewMessage.Location = new Point(
                Math.Max(0, (_pnlEmptyState.Width - _btnNewMessage.Width) / 2),
                Math.Max(0, _pnlEmptyState.Height - _btnNewMessage.Height - 20));
        }

        private async Task OpenNewMessageAsync()
        {
            using var dlg = new SecureChat.Client.frmContacts();
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);

            var targetConvId = dlg.PendingOpenConversationId;
            if (string.IsNullOrEmpty(targetConvId)) return;

            // Fetch trực tiếp conversation vừa tạo, không cần sync toàn bộ
            var (ok, conv, _) = await _messageService.GetConversationAsync(targetConvId);
            if (!ok || conv == null) return;

            BeginInvoke(new Action(() =>
            {
                // Thêm vào sidebar nếu chưa có
                if (!_convs.Any(c => c.Id == targetConvId))
                {
                    bool isGroup = conv.Type == ConversationType.Group;
                    string display = !string.IsNullOrWhiteSpace(conv.Name)
                        ? conv.Name!
                        : "Direct chat";
                    string time = conv.LastActivityAt?.ToLocalTime().ToString("h:mm tt")
                        ?? string.Empty;
                    _convs.Insert(0, (conv.ConversationID, display, string.Empty, time, 0, isGroup));
                }

                _activeConvId = targetConvId;
                BuildConvList();
                LoadConversation(targetConvId);
            }));
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
                Name = "lblPreview",
                Text = preview,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Height = 22,
                Location = new Point(66, 32),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            // Hiển thị thời gian nhắn tin ở góc trên bên phải.
            // Sử dụng Anchor để giữ khoảng cách cố định với mép phải khi co dãn giao diện.
            var lblTime = new Label
            {
                Name = "lblTime",
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
            pnl.PerformLayout();

            _convRowCache[id] = pnl;
            return pnl;
        }

        // ════════════════════════════════════════════
        //  CHAT AREA
        // ════════════════════════════════════════════

        // Mới thêm
        private Button _btnToggleSidebar;
        private Panel _pnlRightSidebar;
        private Panel _sbBody;
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
            btnVideo.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_activeConvId))
                {
                    MessageBox.Show("Please select a conversation first.", "Video Call", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_signalRClient == null || !_signalRClient.IsConnected)
                {
                    MessageBox.Show("SignalR is not connected. Please wait...", "Video Call", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    var createPayload = new { type = 1 };
                    var json = System.Text.Json.JsonSerializer.Serialize(createPayload);
                    var http = ApiClient.Instance.GetHttpClient();
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var response = await http.PostAsync($"api/conversations/{_activeConvId}/calls", content);
                    var responseStr = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Cannot start call: {responseStr}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var callData = System.Text.Json.JsonSerializer.Deserialize<CallResponse>(responseStr, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
                    if (callData == null)
                    {
                        MessageBox.Show("Invalid server response.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    await _signalRClient.NotifyCallIncomingAsync(_activeConvId, callData.CallID, _currentDisplayName, 1);

                    var callForm = new Forms.Call.frmVideoCall(_lblChatName.Text, callData.CallID, _activeConvId, _signalRClient);
                    callForm.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot start call: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
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
                _isSidebarOpen = !_isSidebarOpen;
                _btnToggleSidebar.Text = _isSidebarOpen ? "⏩" : "⏪";

                if (_isSidebarOpen)
                    _ = LoadRightSidebarContentAsync();

                AdjustLayout();
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

            _pnlChatEmpty = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible = false
            };

            _lblChatEmpty = new Label
            {
                AutoSize = true,
                BackColor = Color.FromArgb(220, 255, 255, 255),
                ForeColor = TG.TextSecondary,
                Font = TG.FontSemiBold(9.5f),
                Padding = new Padding(12, 6, 12, 6)
            };
            _pnlChatEmpty.Controls.Add(_lblChatEmpty);
            _pnlChatEmpty.Resize += (_, __) => LayoutChatEmptyState();


            typeof(Panel).InvokeMember("DoubleBuffered",
            System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, _pnlMessages, new object[] { true });
            // _pnlMessages.Paint += PaintChatBackground;
            // Click vào vùng chat → đóng settings menu
            _pnlMessages.Click += (s, e) => { if (_settingsVisible) HideSettingsMenu(); };

            // ── Pinned message bar (Messenger-style) ─────
            _lblPinnedText = new Label
            {
                AutoSize = false,
                Height = 28,
                Font = TG.FontRegular(10f),
                ForeColor = TG.TextPrimary,
                BackColor = Color.Transparent,
                Left = 8,
                Top = 0,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
            };
            _btnUnpin = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(20, 20),
                Cursor = Cursors.Hand,
                ForeColor = TG.TextSecondary,
            };
            _btnUnpin.FlatAppearance.BorderSize = 0;
            _btnUnpin.Click += (s, e) => { _isPinnedPopupOpen = false; _pnlPinnedPopup.Visible = false; OnUnpinMessage(); };
            _pnlPinnedBar = new Panel
            {
                Height = 28,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(0xE8, 0xEA, 0xED),
                Visible = false,
                Cursor = Cursors.Hand,
            };
            var downArrow = new Label
            {
                Text = "▾",
                Font = TG.FontRegular(8f),
                AutoSize = true,
                ForeColor = TG.TextSecondary,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _pnlPinnedBar.Controls.Add(_lblPinnedText);
            _pnlPinnedBar.Controls.Add(downArrow);
            _pnlPinnedBar.Controls.Add(_btnUnpin);
            void LayoutPinBar()
            {
                // Layout từ phải sang trái: [✕ 24px] [▾ 24px] [text phần còn lại]
                const int btnW   = 24;
                const int arrowW = 24;
                const int pad    = 6;

                _btnUnpin.Size     = new Size(btnW, 20);
                _btnUnpin.Location = new Point(_pnlPinnedBar.Width - pad - btnW,
                                               (_pnlPinnedBar.Height - _btnUnpin.Height) / 2);

                downArrow.Location = new Point(_btnUnpin.Left - arrowW - 2,
                                               (_pnlPinnedBar.Height - downArrow.Height) / 2);

                _lblPinnedText.Width = Math.Max(80, downArrow.Left - 12);
            }
            _pnlPinnedBar.Resize += (_, __) => LayoutPinBar();
            _pnlPinnedBar.VisibleChanged += (_, __) => { if (_pnlPinnedBar.Visible) LayoutPinBar(); };

            // Toggle popup on bar click
            void TogglePinnedPopup(object? s, EventArgs e)
            {
                if (_pinnedMessageIds.Count == 0) return;
                _isPinnedPopupOpen = !_isPinnedPopupOpen;
                if (_isPinnedPopupOpen) RebuildPinnedPopup();
                _pnlPinnedPopup.Visible = _isPinnedPopupOpen;
                downArrow.Text = _isPinnedPopupOpen ? "▴" : "▾";
            }
            _lblPinnedText.Click += TogglePinnedPopup;
            downArrow.Click += TogglePinnedPopup;
            _pnlPinnedBar.Click += TogglePinnedPopup;

            // ── Pinned popup dropdown ─────────────────────
            _pnlPinnedPopup = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Visible = false,
                Height = 0,
            };
            // Click outside popup to close
            _pnlMessages.MouseClick += (s, e) => { _isPinnedPopupOpen = false; _pnlPinnedPopup.Visible = false; };
            _pnlChatHeader.Click += (s, e) => { _isPinnedPopupOpen = false; _pnlPinnedPopup.Visible = false; };

            // ── Pinned message bottom indicator ────────
            _lblPinnedBottomText = new Label
            {
                Text = "Pinned message",
                Font = TG.FontRegular(9f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                Location = new Point(8, 3),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _pnlPinnedBottomBar = new Panel
            {
                Height = 24,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(0xE8, 0xEA, 0xED),
                Visible = false,
                Cursor = Cursors.Hand,
            };
            _pnlPinnedBottomBar.Controls.Add(_lblPinnedBottomText);
            void OnBottomBarClick(object? s, EventArgs e)
            {
                if (_pinnedMessageIds.Count == 0) return;
                string firstId = _pinnedMessageIds.First();
                ScrollToMessage(firstId);
            }
            _pnlPinnedBottomBar.Click += OnBottomBarClick;
            _lblPinnedBottomText.Click += OnBottomBarClick;

            // ── Input bar ─────────────────────────────
            BuildInputBar();

            _pnlChat.Controls.Add(_pnlPinnedPopup);
            _pnlChat.Controls.Add(_pnlPinnedBar);
            _pnlChat.Controls.Add(_pnlMessages);
            _pnlChat.Controls.Add(_pnlChatEmpty);
            _pnlChat.Controls.Add(_pnlPinnedBottomBar);
            _pnlChat.Controls.Add(_pnlInputBar);
            _pnlChat.Controls.Add(_pnlChatHeader);

            // ── Right Sidebar (profile / group info) ────────
            _pnlRightSidebar = new Panel { Width = 300, BackColor = Color.White, Visible = false };

            var sbHeader = new Panel { Height = 52, Dock = DockStyle.Top, BackColor = Color.White };
            sbHeader.Paint += (_, e) => e.Graphics.DrawLine(new Pen(TG.Divider), 0, 51, sbHeader.Width, 51);

            var sbClose = new Button
            {
                Text = "⏩",
                Font = new Font("Segoe UI Emoji", 13f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 36),
                BackColor = Color.Transparent,
                ForeColor = TG.TextSecondary,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(252, 8)
            };
            sbClose.FlatAppearance.BorderSize = 0;
            sbClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 0, 0, 0);
            sbClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 0, 0, 0);
            sbClose.Click += (_, _) => { _isSidebarOpen = false; _btnToggleSidebar.Text = "⏪"; AdjustLayout(); };

            sbHeader.Controls.Add(new Label
            {
                Text = "Info",
                Font = TG.FontSemiBold(10f),
                ForeColor = TG.TextPrimary,
                AutoSize = false,
                Height = 22,
                Width = 230,
                Location = new Point(12, 14),
                BackColor = Color.Transparent
            });
            sbHeader.Controls.Add(sbClose);

            _sbBody = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };

            _pnlRightSidebar.Controls.Add(_sbBody);
            _pnlRightSidebar.Controls.Add(sbHeader);

            // _pnlMessages.Resize += (s, e) => _pnlMessages.Invalidate(); // bỏ vì đã có PaintChatBackground tự xử lý.
            _pnlMessages.Resize += (s, e) => UpdateCachedBackground();

            UpdateChatEmptyStateUI();
        }

        private void EnsureChatMoreMenu()
        {
            if (_chatMoreMenu == null)
            {
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
            }

            _chatMoreMenu.Items.Clear();

            var currentConv = _convs.Find(c => c.Id == _activeConvId);
            bool isGroup = currentConv.IsGroup;

            _chatMoreMenu.Items.Add(_mnuMuteNotifications);

            if (isGroup)
            {
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem("ℹ️  View group info", (_, __) => OpenGroupInfo()));
                _chatMoreMenu.Items.Add(CreateChatMenuItem("🎛️  Manage group", (_, __) => OpenEditGroupFromChat()));
                _chatMoreMenu.Items.Add(CreateChatMenuItem("🧹  Clear history", (_, __) => ClearHistory()));
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem("🚪  Delete and leave", (_, __) => DeleteAndLeave(), Color.FromArgb(0xE2, 0x4B, 0x4A)));
            }
            else
            {
                _chatMoreMenu.Items.Add(CreateChatMenuItem("👤  View Profile", (_, __) => ViewProfile()));
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem("🗑  Clear History", (_, __) => ClearHistoryPrivate()));
                _chatMoreMenu.Items.Add(CreateChatMenuItem("🗑  Delete Chat", (_, __) => DeleteChat(), Color.FromArgb(0xE2, 0x4B, 0x4A)));
            }

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
                    var opts = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MemberResponse>>(json, opts);
                    if (list != null)
                        members = list.Select(m => new SecureChat.Client.Forms.Chat.MemberModel(
    m.User?.DisplayName ?? m.Nickname ?? "Unknown",
    "last seen recently",
    m.Role.ToString(),
    null,
    TG.GetAvatarColor(m.User?.DisplayName ?? "?")
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

        private async void ClearHistory()
        {
            using var dlg = new SecureChat.Client.Forms.Chat.frmClearHistory(_lblChatName.Text);
            if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.DeleteConfirmed)
                return;

            if (!await TryRemoveConversationOnServerAsync(_activeConvId))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                MessageBox.Show(this,
                    resources.GetString("ChatDeleteFailed") ?? "Unable to delete the conversation right now.",
                    resources.GetString("ChatDeleteTitle") ?? "Clear history",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RemoveConversationLocal(_activeConvId);
        }

        private async void DeleteAndLeave()
        {
            // Kiểm tra xem có phải group không
            var currentConv = _convs.Find(c => c.Id == _activeConvId);
            bool isGroup = currentConv.IsGroup;

            // Nếu là group, hiện dialog leave group với appoint admin
            if (isGroup)
            {
                var memberNames = new List<string>();
                try
                {
                    var http = ApiClient.Instance.GetHttpClient();
                    var res = await http.GetAsync($"api/conversations/{_activeConvId}/members");
                    if (res.IsSuccessStatusCode)
                    {
                        var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var list = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MemberResponse>>(
                            await res.Content.ReadAsStringAsync(), opts);
                        if (list != null)
                            memberNames = list.Select(m => m.User?.DisplayName ?? m.Nickname ?? "Unknown").ToList();
                    }
                }
                catch { }
                using var dlg = new frmLeaveGroup(_lblChatName.Text, _currentDisplayName, memberNames.ToArray());
                if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.LeaveConfirmed)
                    return;
            }
            else
            {
                // Nếu là chat 1-1, chỉ confirm delete (không cần appoint admin)
                var result = MessageBox.Show(this,
                    $"Bạn có chắc chắn muốn xóa cuộc trò chuyện với {_lblChatName.Text}?\n\nTất cả tin nhắn sẽ bị xóa vĩnh viễn.",
                    "Xóa cuộc trò chuyện",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;
            }

            if (!await TryRemoveConversationOnServerAsync(_activeConvId))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                string errorTitle = isGroup
                    ? (resources.GetString("ChatLeaveTitle") ?? "Leave group")
                    : (resources.GetString("ChatDeleteTitle") ?? "Delete conversation");
                string errorMessage = isGroup
                    ? (resources.GetString("ChatLeaveFailed") ?? "Unable to leave the group right now.")
                    : "Không thể xóa cuộc trò chuyện. Vui lòng thử lại.";

                MessageBox.Show(this,
                    errorMessage,
                    errorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RemoveConversationLocal(_activeConvId);
        }

        private async Task<bool> TryRemoveConversationOnServerAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return false;

            var (deletedOk, deletedErr) = await ApiClient.Instance.DeleteAsync($"api/conversations/{conversationId}");
            if (deletedOk)
                return true;

            var (leftOk, _, leftErr) = await ApiClient.Instance.PostAsync<object, object>(
                $"api/conversations/{conversationId}/leave", new { });

            if (leftOk)
                return true;

            if (!string.IsNullOrWhiteSpace(leftErr))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                MessageBox.Show(this,
                    leftErr,
                    resources.GetString("ChatLeaveTitle") ?? "Leave conversation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(deletedErr))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                MessageBox.Show(this,
                    deletedErr,
                    resources.GetString("ChatDeleteTitle") ?? "Delete conversation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return false;
        }

        private void RemoveConversationLocal(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return;

            // Xóa conversation khỏi danh sách
            _convs.RemoveAll(c => c.Id == conversationId);

            // Xóa tất cả tin nhắn của conversation
            _allMsgs.Remove(conversationId);

            // Xóa khỏi cache sync
            _syncedConversations.Remove(conversationId);
            _myMemberIdByConv.Remove(conversationId);

            // Xóa conversation key khỏi decryptor cache
            _decryptor.ForgetConversation(conversationId);

            // Xóa processed message IDs của conversation này
            lock (_processedMessageIdsLock)
            {
                // Không thể xóa specific message IDs vì không có mapping conversationId -> messageIds
                // Nhưng khi conversation bị xóa, các message IDs cũ sẽ không còn ý nghĩa
            }

            // Nếu conversation đang active bị xóa, chuyển sang conversation khác hoặc empty state
            if (_activeConvId == conversationId)
            {
                _activeConvId = string.Empty;

                // Clear current messages
                _currentMsgs.Clear();
                _forwardMetadata.Clear();
                _forwardOriginalSenderId.Clear();

                // Clear pinned messages — conversation/pins no longer exist
                _pinnedMessageIds.Clear();
                UpdatePinnedBar();
                
                // Chọn conversation đầu tiên nếu còn
                if (_convs.Count > 0)
                {
                    _activeConvId = _convs[0].Id;
                }
            }

            // Rebuild UI
            BuildConvList();

            // Load conversation mới hoặc hiện empty state
            if (!string.IsNullOrWhiteSpace(_activeConvId))
                LoadConversation(_activeConvId);
            else
                UpdateChatEmptyStateUI();
        }

        private async void ViewProfile()
        {
            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            try
            {
                var res = await http.GetAsync($"api/conversations/{_activeConvId}/members");
                if (!res.IsSuccessStatusCode)
                {
                    MessageBox.Show(this, "Unable to load profile information.", "View Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var opts = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var members = System.Text.Json.JsonSerializer.Deserialize<List<SecureChat.DTOs.MemberResponse>>(
                    await res.Content.ReadAsStringAsync(), opts);

                if (members == null || members.Count < 2)
                {
                    MessageBox.Show(this, "Unable to find the other user in this conversation.", "View Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var other = members.FirstOrDefault(m => m.UserID != _currentUserId) ?? members[0];
                if (other.User == null)
                {
                    MessageBox.Show(this, "User information is not available.", "View Profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dlg = new frmUserProfile(
                    other.User.DisplayName ?? "Unknown",
                    other.User.Username ?? "unknown",
                    other.User.Email,
                    other.User.BioText
                );
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading profile: {ex.Message}", "View Profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ClearHistoryPrivate()
        {
            var conv = _convs.Find(c => c.Id == _activeConvId);
            string otherName = conv.Name ?? "the other user";

            var result = MessageBox.Show(this,
                "Clear chat history?",
                "Clear History",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            bool alsoForOther = MessageBox.Show(this,
                $"Also clear for {otherName}?",
                "Clear History",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

            try
            {
                if (alsoForOther)
                {
                    var (clearOk, _, clearErr) = await ApiClient.Instance.PostAsync<object, object>(
                        $"api/conversations/{_activeConvId}/clear", new { });
                    if (!clearOk)
                    {
                        MessageBox.Show(this,
                            $"Failed to clear chat on server: {clearErr}",
                            "Clear History",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }

                foreach (var msg in _currentMsgs)
                {
                    _messageDates.Remove(msg.Id);
                    _forwardMetadata.TryRemove(msg.Id, out _);
                    _forwardOriginalSenderId.TryRemove(msg.Id, out _);
                }
                _currentMsgs.Clear();

                // Clear pinned messages — they reference deleted messages
                _pinnedMessageIds.Clear();
                UpdatePinnedBar();

                // Refresh sidebar: clear preview text and timestamp.
                // RefreshConversationItem updates both the _convs tuple
                // and the visible Label controls in the cached row.
                RefreshConversationItem(_activeConvId, string.Empty, true, string.Empty, string.Empty);

                BuildMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error clearing history: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DeleteChat()
        {
            var conv = _convs.Find(c => c.Id == _activeConvId);
            string otherName = conv.Name ?? "the other user";

            var result = MessageBox.Show(this,
                "Delete this chat?",
                "Delete Chat",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            bool alsoForOther = MessageBox.Show(this,
                $"Also delete for {otherName}?",
                "Delete Chat",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

            bool serverOk;
            if (alsoForOther)
            {
                var (deletedOk, deletedErr) = await ApiClient.Instance.DeleteAsync(
                    $"api/conversations/{_activeConvId}");
                serverOk = deletedOk;
                if (!serverOk)
                {
                    MessageBox.Show(this,
                        $"Unable to delete the conversation: {deletedErr}",
                        "Delete Chat",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                var (leftOk, _, leftErr) = await ApiClient.Instance.PostAsync<object, object>(
                    $"api/conversations/{_activeConvId}/leave", new { });
                serverOk = leftOk;
                if (!serverOk)
                {
                    MessageBox.Show(this,
                        $"Unable to leave the conversation: {leftErr}",
                        "Delete Chat",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            RemoveConversationLocal(_activeConvId);
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
            // Strip forward prefix (backward compat)
            const string forwardPrefix = "[Forwarded from ";
            if (!string.IsNullOrEmpty(rawText) && rawText.StartsWith(forwardPrefix))
            {
                int endBracket = rawText.IndexOf(']');
                if (endBracket > forwardPrefix.Length)
                {
                    string rest = rawText.Substring(endBracket + 2).TrimStart();
                    return ExtractActualText(rest);
                }
            }

            const string replyPrefix = "reply::";
            if (!string.IsNullOrEmpty(rawText) && rawText.StartsWith(replyPrefix))
            {
                string payload = rawText.Substring(replyPrefix.Length);
                int lastSep = payload.LastIndexOf("::");
                if (lastSep > 0)
                    return payload.Substring(lastSep + 2);
            }
            return rawText;
        }

        private string StripForwardPrefix(string text)
        {
            const string forwardPrefix = "[Forwarded from ";
            if (!string.IsNullOrEmpty(text) && text.StartsWith(forwardPrefix))
            {
                int endBracket = text.IndexOf(']');
                if (endBracket > forwardPrefix.Length)
                {
                    string rest = text.Substring(endBracket + 2).TrimStart();
                    return rest;
                }
            }
            return text;
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
                            string errorMessage = "Không thể upload file lên server.";
                            if (!string.IsNullOrWhiteSpace(respStr))
                            {
                                try
                                {
                                    var errorDoc = System.Text.Json.JsonDocument.Parse(respStr);
                                    if (errorDoc.RootElement.TryGetProperty("message", out var msgProp))
                                    {
                                        errorMessage = $"Upload thất bại: {msgProp.GetString()}";
                                    }
                                    else if (errorDoc.RootElement.TryGetProperty("error", out var errProp))
                                    {
                                        errorMessage = $"Upload thất bại: {errProp.GetString()}";
                                    }
                                    else
                                    {
                                        errorMessage = $"Upload thất bại (HTTP {(int)resp.StatusCode}): {respStr}";
                                    }
                                }
                                catch
                                {
                                    errorMessage = $"Upload thất bại (HTTP {(int)resp.StatusCode}): {respStr}";
                                }
                            }
                            else
                            {
                                errorMessage = $"Upload thất bại với mã lỗi HTTP {(int)resp.StatusCode}.";
                            }

                            this.BeginInvoke(new Action(() => MessageBox.Show(this, errorMessage, "Lỗi Upload File", MessageBoxButtons.OK, MessageBoxIcon.Error)));
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

                        // Truncate tên file nếu quá 64 ký tự (giới hạn server)
                        if (fileName.Length > 64)
                        {
                            string ext = Path.GetExtension(fileName);           // ví dụ ".docx"
                            string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                            fileName = nameOnly.Substring(0, 64 - ext.Length) + ext;
                        }

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
                            MentionedMemberIDs: null,
                            ExpiresAfterSeconds: _selfDestructSeconds);

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
                                    SecureChat.Shared.Security.KeyManager.CacheAesKey(msgRes.MessageID, aesKey, aesIv);
                                    string payload = $"file::{att.FileURL}::{att.FileName}::{att.FileSize}::{att.FileHash}";
                                    _messageDates[msgRes.MessageID] = msgRes.SentAt.ToLocalTime();
                                    _currentMsgs.Add((msgRes.MessageID, payload, true, msgRes.SentAt.ToString("h:mm tt"), msgRes.SenderUsername ?? ""));
                                    if (msgRes.ExpiresAt.HasValue)
                                        _expirationService.TrackMessage(msgRes.MessageID, msgRes.ExpiresAt.Value);
                                    this.BeginInvoke(new Action(() => BuildMessages()));
                                }
                            }

                            if (_signalRClient is not null)
                            {
                                try
                                {
                                    await _signalRClient.SendMessageAsync(_activeConvId, msgRes);
                                }
                                catch (Exception ex)
                                {
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "SignalR", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
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

            // Self-destruct timer button
            var btnTimer = MakeInputBtn("⏱");
            btnTimer.Click += (s, e) =>
            {
                // Show context menu to select self-destruct time
                var menu = new ContextMenuStrip();
                menu.Items.Add("Không tự hủy", null, (_, __) => { _selfDestructSeconds = null; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("5 giây", null, (_, __) => { _selfDestructSeconds = 5; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add("10 giây", null, (_, __) => { _selfDestructSeconds = 10; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add("30 giây", null, (_, __) => { _selfDestructSeconds = 30; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add("1 phút", null, (_, __) => { _selfDestructSeconds = 60; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add("5 phút", null, (_, __) => { _selfDestructSeconds = 300; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add("1 giờ", null, (_, __) => { _selfDestructSeconds = 3600; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add("1 ngày", null, (_, __) => { _selfDestructSeconds = 86400; UpdateTimerButtonText(btnTimer); });
                menu.Show(btnTimer, new Point(0, btnTimer.Height));
            };

            _tbMessage = new TelegramTextBox { Height = 36 };
            _tbMessage.SetPlaceholder("Write a message...");
            _tbMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return && !e.Shift) { e.SuppressKeyPress = true; SendMessage(); } };

            var btnEmoji = MakeInputBtn("😊");
            btnEmoji.Click += (s, e) =>
            {
                using var picker = new SecureChat.Client.Forms.Chat.frmReactionPicker();
                var btn = (Control)s!;
                var screenPt = btn.PointToScreen(new Point(0, 0));
                int x = Math.Max(0, screenPt.X + btn.Width - picker.Width);
                int y = screenPt.Y - picker.Height - 4;
                if (y < 0) y = screenPt.Y + btn.Height + 4;
                picker.StartPosition = FormStartPosition.Manual;
                picker.Location = new Point(x, y);
                if (picker.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(picker.SelectedReaction))
                {
                    _tbMessage.Text += picker.SelectedReaction;
                    _tbMessage.Focus();
                }
            };
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
                                string durationLocal = SecureChat.Client.Services.AudioRecorderService.GetDurationSeconds(wavPath).ToString();

                                using var fs = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                using var content = new MultipartFormDataContent();
                                var streamContent = new StreamContent(fs);
                                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                                content.Add(streamContent, "file", Path.GetFileName(encryptedPath));
                                content.Add(new System.Net.Http.StringContent(durationLocal), "duration");

                                var (okUpload, respStr, uploadErr) = await ApiClient.Instance.PostMultipartAsync("api/voice/upload", content);
                                if (!okUpload)
                                {
                                    string errorMessage = "Không thể upload file lên server.";
                                    if (!string.IsNullOrWhiteSpace(uploadErr))
                                    {
                                        errorMessage = $"Upload thất bại: {uploadErr}";
                                    }
                                    else if (!string.IsNullOrWhiteSpace(respStr))
                                    {
                                        try
                                        {
                                            var errorDoc = System.Text.Json.JsonDocument.Parse(respStr);
                                            if (errorDoc.RootElement.TryGetProperty("message", out var msgProp))
                                            {
                                                errorMessage = $"Upload thất bại: {msgProp.GetString()}";
                                            }
                                            else if (errorDoc.RootElement.TryGetProperty("error", out var errProp))
                                            {
                                                errorMessage = $"Upload thất bại: {errProp.GetString()}";
                                            }
                                            else
                                            {
                                                errorMessage = $"Upload thất bại: {respStr}";
                                            }
                                        }
                                        catch
                                        {
                                            errorMessage = $"Upload thất bại: {respStr}";
                                        }
                                    }

                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, errorMessage, "Lỗi Upload Voice", MessageBoxButtons.OK, MessageBoxIcon.Error)));
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

                                // Truncate
                                if (fileName.Length > 64)
                                {
                                    string ext = Path.GetExtension(fileName);
                                    string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                                    fileName = nameOnly.Substring(0, 64 - ext.Length) + ext;
                                }

                                long fileSize = root.GetProperty("fileSize").GetInt64();
                                string sha = root.TryGetProperty("sha256", out var shaEl) ? shaEl.GetString() ?? localSha : localSha;
                                string duration = root.TryGetProperty("duration", out var durEl) && durEl.ValueKind == System.Text.Json.JsonValueKind.Number
                                    ? durEl.GetInt32().ToString()
                                    : durationLocal;
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
                                    MentionedMemberIDs: null,
                                    ExpiresAfterSeconds: _selfDestructSeconds);

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
                                            _messageDates[msgRes.MessageID] = msgRes.SentAt.ToLocalTime();
                                            _currentMsgs.Add((msgRes.MessageID, payload, true, msgRes.SentAt.ToString("h:mm tt"), msgRes.SenderUsername ?? ""));
                                            if (msgRes.ExpiresAt.HasValue)
                                                _expirationService.TrackMessage(msgRes.MessageID, msgRes.ExpiresAt.Value);
                                            this.BeginInvoke(new Action(() => BuildMessages()));
                                        }
                                    }

                                    if (_signalRClient is not null)
                                    {
                                        try
                                        {
                                            await _signalRClient.SendMessageAsync(_activeConvId, msgRes);
                                        }
                                        catch (Exception ex)
                                        {
                                            this.BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "SignalR", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
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

            _tbMessage.TextChanged += (s, e) =>
            {
                bool hasText = !string.IsNullOrWhiteSpace(_tbMessage.Text);
                btnSend.Visible = hasText;
                btnMic.Visible = !hasText;

                if (string.IsNullOrWhiteSpace(_activeConvId) || _signalRClient == null || !_signalRClient.IsConnected)
                    return;

                var currentConv = _activeConvId;
                if (hasText)
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastTypingSent).TotalSeconds >= 3)
                    {
                        _lastTypingSent = now;
                        _ = _signalRClient.NotifyTypingAsync(currentConv);
                    }

                    if (_typingDebounceTimer == null)
                    {
                        _typingDebounceTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                        _typingDebounceTimer.Tick += (_, __) =>
                        {
                            _typingDebounceTimer.Stop();
                            var targetConv = _typingDebounceConvId;
                            if (!string.IsNullOrWhiteSpace(targetConv))
                                _ = _signalRClient?.NotifyStoppedTypingAsync(targetConv);
                        };
                    }
                    _typingDebounceConvId = currentConv;
                    _typingDebounceTimer.Stop();
                    _typingDebounceTimer.Start();
                }
                else
                {
                    if (_typingDebounceTimer != null)
                    {
                        _typingDebounceTimer.Stop();
                    }
                    _typingDebounceConvId = null;
                    _ = _signalRClient.NotifyStoppedTypingAsync(currentConv);
                }
            };

            // Gom nhóm ô nhập liệu vào một Panel dưới cùng
            var pnlInputControls = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Color.Transparent };
            pnlInputControls.Controls.AddRange(new Control[] { btnAttach, btnTimer, _tbMessage, btnEmoji, btnMic, btnSend });

            pnlInputControls.Resize += (s, e) =>
            {
                int y = 10;
                btnAttach.Location = new Point(8, y);
                btnTimer.Location = new Point(btnAttach.Right + 2, y);
                btnEmoji.Location = new Point(Math.Max(0, pnlInputControls.Width - 84), y);
                btnMic.Location = new Point(Math.Max(0, pnlInputControls.Width - 44), y);
                btnSend.Location = new Point(Math.Max(0, pnlInputControls.Width - 44), y);
                _tbMessage.SetBounds(btnTimer.Right + 6, y, Math.Max(0, btnEmoji.Left - btnTimer.Right - 12), 36);
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
                Font = new Font("Segoe UI Emoji", 13f),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(36, 36),
                ForeColor = TG.Blue,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                UseVisualStyleBackColor = true,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 0, 0, 0);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 0, 0, 0);

            return btn;
        }

        private void UpdateTimerButtonText(Button btnTimer)
        {
            if (_selfDestructSeconds.HasValue)
            {
                // Show timer with number
                if (_selfDestructSeconds.Value < 60)
                    btnTimer.Text = $"⏱{_selfDestructSeconds.Value}s";
                else if (_selfDestructSeconds.Value < 3600)
                    btnTimer.Text = $"⏱{_selfDestructSeconds.Value / 60}m";
                else if (_selfDestructSeconds.Value < 86400)
                    btnTimer.Text = $"⏱{_selfDestructSeconds.Value / 3600}h";
                else
                    btnTimer.Text = $"⏱{_selfDestructSeconds.Value / 86400}d";

                btnTimer.ForeColor = Color.FromArgb(255, 87, 34); // Orange color for active timer
                btnTimer.Font = new Font("Segoe UI Emoji", 10f);
            }
            else
            {
                btnTimer.Text = "⏱";
                btnTimer.ForeColor = TG.Blue;
                btnTimer.Font = new Font("Segoe UI Emoji", 13f);
            }
        }

        private string FormatRemainingTime(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}s";
            else if (seconds < 3600)
                return $"{seconds / 60}m";
            else if (seconds < 86400)
                return $"{seconds / 3600}h";
            else
                return $"{seconds / 86400}d";
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

            _settingsAvatar = new AvatarControl { Size = new Size(56, 56), Location = new Point(14, 52) };
            _settingsAvatar.SetName(_currentDisplayName);

            _lblSettingsUserName = new Label
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
            _settingsAvatar.Click += (s, e) => toggleAccountPanel();
            _lblSettingsUserName.Click += (s, e) => toggleAccountPanel();

            _pnlSettingsHeader.Controls.AddRange(new Control[] { btnClose, _settingsAvatar, _lblSettingsUserName, lblEmojiStatus, btnChevron });
            _pnlSettingsHeader.Resize += (s, e) =>
            {
                _lblSettingsUserName.Width = _pnlSettingsHeader.Width - 84 - 36;
                lblEmojiStatus.Width = _pnlSettingsHeader.Width - 84 - 36;
                btnChevron.Location = new Point(_pnlSettingsHeader.Width - 36, 52);
            };

            // ── Menu items ────────────────────────────
            var menuItems = new (string Emoji, string Label, bool HasToggle)[]
{
    ("👤", "My Profile",      false),
    ("👥", "New Group",       false),
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
                                Email = _currentEmail,
                                Username = _currentUsername,
                                AvatarPath = string.Empty,
                                StatusText = "online"
                            };
                            using var myprofile = new SecureChat.Client.Forms.Profile.frmMyProfile(profile);
                            myprofile.StartPosition = FormStartPosition.CenterParent;
                            if (myprofile.ShowDialog(this) == DialogResult.OK)
                            {
                                var http = ApiClient.Instance.GetHttpClient();
                                var req = new { displayName = profile.FullName };
                                var json = System.Text.Json.JsonSerializer.Serialize(req);
                                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                                var res = await http.PatchAsync("api/users/me", content);
                                if (res.IsSuccessStatusCode)
                                {
                                    _currentDisplayName = profile.FullName;
                                    _currentUsername = profile.Username;
                                    _currentEmail = profile.Email;
                                    UpdateSettingsHeaderUI();
                                }
                            }
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
                            if (dlg.ShowDialog(this) != DialogResult.OK) break;
                            if (string.IsNullOrWhiteSpace(dlg.ResultGroupName))
                            {
                                MessageBox.Show(this, "Group name is required.", "Tạo nhóm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                break;
                            }
                            if (dlg.ResultMemberIds == null || dlg.ResultMemberIds.Count == 0)
                            {
                                MessageBox.Show(this, "Please select members for the group.", "Tạo nhóm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                break;
                            }

                            var http = ApiClient.Instance.GetHttpClient();

                            // Build Members payload expected by server: list of { UserID, EncryptedKey }
                            var members = new List<object>();
                            foreach (var id in dlg.ResultMemberIds)
                                members.Add(new { UserID = id, EncryptedKey = "TBD" });

                            // Ensure current user is included as a member
                            if (!string.IsNullOrWhiteSpace(_currentUserId) && !dlg.ResultMemberIds.Contains(_currentUserId))
                                members.Insert(0, new { UserID = _currentUserId, EncryptedKey = "TBD" });

                            var payload = new
                            {
                                Type = SecureChat.Models.ConversationType.Group, // serializes to numeric enum
                                Name = dlg.ResultGroupName,
                                AvatarUrl = (string?)null,
                                Members = members
                            };

                            var json = System.Text.Json.JsonSerializer.Serialize(payload);
                            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                            var res = await http.PostAsync("api/conversations", content);

                            if (!res.IsSuccessStatusCode)
                            {
                                string body = string.Empty;
                                try { body = await res.Content.ReadAsStringAsync(); } catch { /* ignore */ }

                                var msg = $"Tạo nhóm thất bại. HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
                                if (!string.IsNullOrWhiteSpace(body))
                                    msg += $"\n\nServer response:\n{body}";

                                MessageBox.Show(this, msg, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                System.Diagnostics.Debug.WriteLine($"Create group failed: {msg}");
                                break;
                            }

                            // success — refresh conversations
                            await SyncConversationsAsync();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
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

                                // Dùng LoadConversationsAsync thay vì SyncConversationsAsync
                                // vì Sync populate _convs bên trong BeginInvoke (async), không kịp cho code bên dưới
                                await LoadConversationsAsync();

                                _activeConvId = convId;
                                BuildConvList();
                                UpdateEmptyStateUI();    // show pnlConvList, ẩn pnlEmptyState
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
                            var profile = new SecureChat.Client.Models.ProfileModel
                            {
                                FullName = _currentDisplayName,
                                Email = _currentEmail,
                                Username = _currentUsername,
                                AvatarPath = string.Empty,
                                StatusText = "online"
                            };
                            using var settings = new SecureChat.Client.Forms.Settings.frmSettings(profile);
                            settings.StartPosition = FormStartPosition.CenterParent;
                            var dr = settings.ShowDialog(this);
                            if (dr == DialogResult.OK)
                            {
                                _currentDisplayName = profile.FullName;
                                _currentUsername = profile.Username;
                                _currentEmail = profile.Email;
                                UpdateSettingsHeaderUI();
                            }
                            else if (dr == DialogResult.No)
                            {
                                // 1. Xóa Access Token để không gọi API được nữa
                                ApiClient.Instance.SetAccessToken(null);
                                SecureChat.Shared.Security.KeyManager.Clear();
                                lock (_processedMessageIdsLock)
                                {
                                    _processedMessageIds.Clear();
                                }

                                // 2. Tìm login form cũ đang ẩn và hiện lại
                                var oldLogin = Application.OpenForms.OfType<frmLoginRegister>().FirstOrDefault();
                                if (oldLogin != null)
                                {
                                    oldLogin.Show();
                                }
                                else
                                {
                                    var loginForm = new frmLoginRegister();
                                    loginForm.Show();
                                }

                                // 3. Ẩn Form Main thay vì đóng để không kích hoạt FormClosed cascade
                                this.Hide();
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

        private void UpdateSettingsHeaderUI()
        {
            if (_settingsAvatar != null)
                _settingsAvatar.SetName(_currentDisplayName);
            if (_lblSettingsUserName != null)
                _lblSettingsUserName.Text = _currentDisplayName;
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
        private async void LoadConversation(string convId)
        {
            if (string.IsNullOrWhiteSpace(convId))
            {
                UpdateChatEmptyStateUI();
                return;
            }

            var conv = _convs.Find(c => c.Id == convId);
            if (conv == default)
                return;

            // Cleanup typing state from previous conversation
            var prevConv = _activeConvId;
            if (!string.IsNullOrWhiteSpace(prevConv) && prevConv != convId)
            {
                _typingDebounceTimer?.Stop();
                _typingDebounceConvId = null;
                _ = _signalRClient?.NotifyStoppedTypingAsync(prevConv);
            }

            // Close right sidebar when switching conversation
            if (_isSidebarOpen)
                CloseRightSidebar();

            _activeConvId = convId;
            UpdateChatEmptyStateUI();

            _chatAvatar.SetName(conv.Name);
            _lblChatName.Text = conv.Name;
            RestoreChatStatus();

            // Đảm bảo có list (rỗng nếu chưa sync) trước khi vẽ.
            BuildMessages();

            // Sync tin nhắn từ MariaDB (chỉ làm 1 lần / conv) rồi join SignalR group
            // để nhận realtime cho các tin sau đó.
            await SyncMessagesForActiveConversationAsync(convId);
            await JoinConversationSignalRAsync(convId);
            await LoadPinsAsync(convId);

            // Đánh dấu tất cả tin chưa đọc là đã đọc
            _ = Task.Run(async () =>
            {
                try
                {
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    if (_allMsgs.TryGetValue(convId, out var msgs))
                    {
                        foreach (var (id, _, isOut, _, _) in msgs)
                        {
                            if (!isOut) // chỉ mark read cho tin người khác gửi
                                await http.PostAsync($"api/conversations/{convId}/messages/{id}/read", null);
                        }
                    }
                }
                catch { }
            });
        }

        // ════════════════════════════════════════════
        //  RIGHT SIDEBAR
        // ════════════════════════════════════════════

        private void CloseRightSidebar()
        {
            _isSidebarOpen = false;
            _btnToggleSidebar.Text = "⏪";
            AdjustLayout();
        }

        private async Task LoadRightSidebarContentAsync()
        {
            if (string.IsNullOrWhiteSpace(_activeConvId))
                return;

            var conv = _convs.Find(c => c.Id == _activeConvId);
            if (conv == default)
                return;

            try
            {
                var http = ApiClient.Instance.GetHttpClient();
                var response = await http.GetAsync($"api/conversations/{_activeConvId}/members");

                if (!response.IsSuccessStatusCode)
                    return;

                var json = await response.Content.ReadAsStringAsync();
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var members = System.Text.Json.JsonSerializer.Deserialize<List<MemberResponse>>(json, options);

                if (members == null || members.Count == 0)
                    return;

                var isGroup = conv.IsGroup;

                BeginInvoke(new Action(() =>
                {
                    _sbBody.SuspendLayout();
                    _sbBody.Controls.Clear();

                    if (isGroup)
                        BuildGroupSidebar(_sbBody, members);
                    else
                        BuildDirectSidebar(_sbBody, members);

                    _sbBody.ResumeLayout();
                }));
            }
            catch { }
        }

        private void BuildDirectSidebar(Panel body, List<MemberResponse> members)
        {
            var other = members.FirstOrDefault(m => m.UserID != _currentUserId)?.User;
            if (other == null)
            {
                if (members.Count > 0)
                    other = members[0].User;
                if (other == null) return;
            }

            int y = 24;

            // Large avatar (centered)
            var avatar = new AvatarControl
            {
                Size = new Size(100, 100),
                Location = new Point(100, y)
            };
            avatar.SetName(other.DisplayName ?? other.Username);
            body.Controls.Add(avatar);
            y += 118;

            // Display name (centered, fixed width)
            AppendCenteredLabel(body, other.DisplayName ?? other.Username,
                TG.FontSemiBold(16f), TG.TextPrimary, ref y);
            y += 6;

            // Username (centered, fixed width)
            AppendCenteredLabel(body, $"@{other.Username}",
                TG.FontRegular(12f), TG.TextSecondary, ref y);
            y += 24;

            // Divider
            AppendDivider(body, ref y);
            y += 8;

            // Email
            if (!string.IsNullOrWhiteSpace(other.Email))
                AppendInfoRow(body, "📧", other.Email, ref y);

            // Bio
            if (!string.IsNullOrWhiteSpace(other.BioText))
                AppendInfoRow(body, "ℹ️", other.BioText, ref y);

            body.AutoScrollMinSize = new Size(0, y + 20);
        }

        private void BuildGroupSidebar(Panel body, List<MemberResponse> members)
        {
            var conv = _convs.Find(c => c.Id == _activeConvId);

            int y = 24;

            // Large group avatar (centered)
            var avatar = new AvatarControl
            {
                Size = new Size(100, 100),
                Location = new Point(100, y)
            };
            avatar.SetName(conv.Name);
            body.Controls.Add(avatar);
            y += 118;

            // Group name (centered, fixed width)
            AppendCenteredLabel(body, conv.Name,
                TG.FontSemiBold(16f), TG.TextPrimary, ref y);
            y += 6;

            // Member count (centered, fixed width)
            AppendCenteredLabel(body, $"{members.Count} member{(members.Count != 1 ? "s" : "")}",
                TG.FontRegular(12f), TG.TextSecondary, ref y);
            y += 24;

            // Divider
            AppendDivider(body, ref y);
            y += 8;

            // "Members" section title
            body.Controls.Add(new Label
            {
                Text = "MEMBERS",
                Font = TG.FontSemiBold(9f),
                ForeColor = TG.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Size = new Size(260, 14),
                Location = new Point(20, y)
            });
            y += 22;

            // Member list
            foreach (var m in members)
            {
                var displayName = m.User?.DisplayName ?? m.Nickname ?? "Unknown";
                var status = m.User?.ShowOnlineStatus == true ? "online" : "offline";
                var role = m.Role.ToString();

                var item = new ucGroupMemberItem
                {
                    Dock = DockStyle.None,
                    Width = 300,
                    Margin = Padding.Empty,
                    Location = new Point(0, y),
                    BackColor = Color.Transparent
                };
                item.DisplayName = displayName;
                item.Status = status;
                item.Role = role;
                item.SetInitial(displayName.Length > 0
                    ? displayName[0].ToString().ToUpperInvariant()
                    : "?");
                body.Controls.Add(item);
                y += item.Height;
            }

            body.AutoScrollMinSize = new Size(0, y + 20);
        }

        private static void AppendCenteredLabel(Panel body, string text, Font font, Color color, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(280, 0),
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(280, 0)
            };
            if (lbl.Height < 24) lbl.Height = 24;
            lbl.Location = new Point(10, y);
            body.Controls.Add(lbl);
            y += lbl.Height;
        }

        private static void AppendDivider(Panel body, ref int y)
        {
            body.Controls.Add(new Panel
            {
                Height = 1,
                Width = 260,
                BackColor = TG.Divider,
                Location = new Point(20, y)
            });
            y += 1;
        }

        private static void AppendInfoRow(Panel body, string icon, string text, ref int y)
        {
            var lbl = new Label
            {
                Text = $"{icon}  {text}",
                Font = TG.FontRegular(11f),
                ForeColor = TG.TextPrimary,
                AutoSize = true,
                MaximumSize = new Size(260, 0),
                BackColor = Color.Transparent,
                Location = new Point(24, y)
            };
            body.Controls.Add(lbl);
            y += lbl.Height + 10;
        }

        /// <summary>
        /// GET /api/conversations và build sidebar từ dữ liệu thật.
        /// </summary>
        private async Task SyncConversationsAsync()
        {
            var (ok, list, _) = await _messageService.GetMyConversationsAsync();

            if (!ok || list is null)
            {
                BeginInvoke(new Action(UpdateChatEmptyStateUI));
                return;
            }

            BeginInvoke(new Action(() =>
            {
                // Có data thật -> drop mock và replace.
                _convs.Clear();
                _allMsgs.Clear();
                _forwardMetadata.Clear();
                _forwardOriginalSenderId.Clear();
                _syncedConversations.Clear();
                _myMemberIdByConv.Clear();

                foreach (var c in list)
                {
                    string display = !string.IsNullOrWhiteSpace(c.Name)
                        ? c.Name!
                        : (c.Type == ConversationType.Group ? "Group" : "Direct chat");
                    string time = c.LastActivityAt?.ToLocalTime().ToString("h:mm tt") ?? string.Empty;
                    bool isGroup = c.Type == ConversationType.Group;
                    _convs.Add((c.ConversationID, display, string.Empty, time, 0, isGroup));
                }

                BuildConvList();

                if (_convs.Count > 0)
                {
                    _activeConvId = _convs[0].Id;
                    BuildConvList();
                    LoadConversation(_activeConvId);
                }
                else
                {
                    _activeConvId = string.Empty;
                    BuildConvList();
                    UpdateChatEmptyStateUI();
                }
            }));
        }

        /// <summary>
        /// Sync messages cho conversation đang active. Idempotent — chỉ pull
        /// 1 lần / conv (sau đó SignalR sẽ đẩy realtime). Gọi lại không gây
        /// duplicate nhờ <see cref="TryTrackMessageId"/>.
        /// </summary>
        private async Task SyncMessagesForActiveConversationAsync(string convId)
        {
            if (string.IsNullOrWhiteSpace(convId))
                return;
            if (_syncedConversations.Contains(convId))
                return;

            // Cần biết MemberID của user hiện tại trong conv này để xác định "isOut".
            var (memOk, me, _) = await _messageService.GetMyMembershipAsync(convId);
            string? myMemberId = (memOk && me is not null) ? me.MemberID : null;
            if (!string.IsNullOrWhiteSpace(myMemberId))
                _myMemberIdByConv[convId] = myMemberId!;

            if (string.IsNullOrWhiteSpace(myMemberId))
            {
                BeginInvoke(new Action(() =>
                    MessageBox.Show(this, "Không thể xác định thành viên hiện tại trong cuộc trò chuyện.",
                        "Sync messages", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                return;
            }

            var (ok, list, err) = await FetchAllMessagesAsync(convId);
            if (!ok || list is null)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!string.IsNullOrWhiteSpace(err))
                        MessageBox.Show(this, err, "Sync messages", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
                return;
            }

            var decrypted = new List<SecureChat.Client.Services.DecryptedMessage>();
            foreach (var msg in list)
            {
                if (!TryTrackMessageId(msg.MessageID))
                    continue;

                var dm = await _decryptor.ProcessAsync(msg, myMemberId);
                decrypted.Add(dm);

                // Track message expiration nếu có ExpiresAt
                if (msg.ExpiresAt.HasValue)
                {
                    _expirationService.TrackMessage(msg.MessageID, msg.ExpiresAt.Value);
                }
            }

            decrypted.Sort((a, b) => a.Raw.SentAt.CompareTo(b.Raw.SentAt));

            _syncedConversations.Add(convId);

            BeginInvoke(new Action(() =>
            {
                if (!_allMsgs.TryGetValue(convId, out var existing))
                {
                    existing = new List<(string Id, string Text, bool Out, string Time, string Sender)>();
                    _allMsgs[convId] = existing;
                }

                // Bỏ qua tin nhắn đã xóa local
                decrypted.RemoveAll(d => _hiddenMessageIds.Contains(d.Id));

                // Prepend phần đã sync (giữ thứ tự cũ -> mới) lên các tin đã add cục bộ.
                if (existing.Count == 0)
                {
                    foreach (var dm in decrypted)
                    {
                        if (!dm.Out && !string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(dm.SenderDisplayName))
                            _senderDisplayNameMap[dm.Sender] = dm.SenderDisplayName;
                        if (!string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(dm.Raw.SenderUserID))
                            _usernameToUserId[dm.Sender] = dm.Raw.SenderUserID;
                        if (!string.IsNullOrEmpty(dm.Raw.OriginalSenderID) && !string.IsNullOrEmpty(dm.Raw.OriginalSenderName))
                        {
                            _forwardMetadata[dm.Id] = dm.Raw.OriginalSenderName;
                            _forwardOriginalSenderId[dm.Id] = dm.Raw.OriginalSenderID!;
                        }
                        existing.Add((dm.Id, dm.Text, dm.Out, dm.Time, dm.Sender));
                    }
                }
                else
                {
                    var merged = new List<(string Id, string Text, bool Out, string Time, string Sender)>();
                    foreach (var dm in decrypted)
                    {
                        if (!dm.Out && !string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(dm.SenderDisplayName))
                            _senderDisplayNameMap[dm.Sender] = dm.SenderDisplayName;
                        if (!string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(dm.Raw.SenderUserID))
                            _usernameToUserId[dm.Sender] = dm.Raw.SenderUserID;
                        if (!string.IsNullOrEmpty(dm.Raw.OriginalSenderID) && !string.IsNullOrEmpty(dm.Raw.OriginalSenderName))
                        {
                            _forwardMetadata[dm.Id] = dm.Raw.OriginalSenderName;
                            _forwardOriginalSenderId[dm.Id] = dm.Raw.OriginalSenderID!;
                        }
                        merged.Add((dm.Id, dm.Text, dm.Out, dm.Time, dm.Sender));
                    }
                    foreach (var m in existing)
                        if (!decrypted.Exists(d => d.Id == m.Id) && !_hiddenMessageIds.Contains(m.Id))
                            merged.Add(m);
                    _allMsgs[convId] = merged;
                }

                if (decrypted.Count > 0)
                    UpdateConversationPreview(convId, decrypted[^1]);

                if (convId == _activeConvId)
                    BuildMessages();
            }));
        }

        private async Task<(bool Ok, List<MessageResponse>? Data, string Err)> FetchAllMessagesAsync(string convId)
        {
            var all = new List<MessageResponse>();
            DateTime? before = null;
            DateTime? lastBefore = null;
            string lastError = string.Empty;

            while (true)
            {
                var (ok, page, err) = await _messageService.GetMessagesAsync(convId, MessageSyncPageSize, before);
                if (!ok || page is null)
                {
                    lastError = err;
                    return (false, null, lastError);
                }

                if (page.Count == 0)
                    break;

                all.AddRange(page);

                var oldest = page[^1];
                var nextBefore = oldest.SentAt.AddTicks(1);
                if (lastBefore.HasValue && nextBefore <= lastBefore.Value)
                    break;

                before = nextBefore;
                lastBefore = nextBefore;

                if (page.Count < MessageSyncPageSize)
                    break;
            }

            return (true, all, string.Empty);
        }

        private string GetSidebarPreviewText(string rawText, string? messageId = null)
        {
            // Kiểm tra forward metadata
            if (!string.IsNullOrEmpty(messageId) && _forwardMetadata.ContainsKey(messageId))
                return "Forwarded message";

            // Fallback: text prefix (backward compat)
            const string forwardPrefix = "[Forwarded from ";
            if (!string.IsNullOrEmpty(rawText) && rawText.StartsWith(forwardPrefix))
                return "Forwarded message";

            string cleaned = ExtractActualText(rawText);
            if (cleaned.StartsWith("voice::")) return "Voice message";
            if (cleaned.StartsWith("file::")) return "File";
            return cleaned;
        }

        private void RefreshConversationItem(string convId, string rawText, bool isOut, string senderName, string timeStr, int? unread = null, string? messageId = null)
        {
            int idx = _convs.FindIndex(c => c.Id == convId);
            if (idx < 0) return;

            var c = _convs[idx];
            string displayText = GetSidebarPreviewText(rawText, messageId);
            string preview = string.IsNullOrEmpty(senderName) || isOut
                ? displayText
                : $"{senderName}: {displayText}";

            _convs.RemoveAt(idx);
            _convs.Insert(0, (convId, c.Name, preview, timeStr, unread ?? c.Unread, c.IsGroup));

            Panel? row = null;
            if (_convRowCache.TryGetValue(convId, out var cached) && _pnlConvList.Controls.Contains(cached))
                row = cached;

            if (row != null)
            {
                foreach (Control ctrl in row.Controls)
                {
                    if (ctrl is Label lbl)
                    {
                        if (lbl.Name == "lblPreview") lbl.Text = preview;
                        else if (lbl.Name == "lblTime") lbl.Text = timeStr;
                    }
                }
                _pnlConvList.Controls.SetChildIndex(row, 0);
            }
            else
            {
                row = BuildConvRow(convId, c.Name, preview, timeStr, unread ?? c.Unread, c.IsGroup);
                EnableDoubleBuffering(row);
                _pnlConvList.Controls.Add(row);
                _pnlConvList.Controls.SetChildIndex(row, 0);
            }

            int y = 0;
            foreach (Control ctrl in _pnlConvList.Controls)
            {
                if (ctrl is Panel pnl)
                {
                    pnl.Location = new Point(0, y);
                    y += 68;
                }
            }
        }

        private void UpdateConversationPreview(string convId, SecureChat.Client.Services.DecryptedMessage latest)
        {
            string senderForPreview = latest.Out ? "" : (!string.IsNullOrEmpty(latest.SenderDisplayName) ? latest.SenderDisplayName : latest.Sender);
            RefreshConversationItem(convId, latest.Text, latest.Out, senderForPreview,
                latest.Raw.SentAt.ToLocalTime().ToString("h:mm tt"), messageId: latest.Id);
        }

        private void RefreshSidebarPreview()
        {
            if (string.IsNullOrWhiteSpace(_activeConvId)) return;
            var msgs = _currentMsgs;
            if (msgs.Count == 0) return;

            var latest = msgs[^1];
            string senderForPreview = latest.Out ? "" : latest.Sender;
            RefreshConversationItem(_activeConvId, latest.Text, latest.Out, senderForPreview, latest.Time);
        }

        private void BuildMessages()
        {
            _pnlMessages.SuspendLayout();

            _pnlMessages.AutoScrollPosition = new Point(0, 0);

            _pnlMessages.Controls.Clear();

            int y = 8;

            var bubbles = new List<Control>();

            string? lastDateKey = null;

            for (int i = 0; i < _currentMsgs.Count; i++)
            {
                if (_hiddenMessageIds.Contains(_currentMsgs[i].Id))
                    continue;

                var msg = _currentMsgs[i];

                if (!_messageDates.TryGetValue(msg.Id, out var msgDate))
                    msgDate = DateTime.Today;

                string dateKey = msgDate.ToString("yyyy-MM-dd");

                if (dateKey != lastDateKey)
                {
                    string displayDate = FormatDateHeader(msgDate);
                    var sep = new Label
                    {
                        Text = displayDate,
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

                    lastDateKey = dateKey;
                }

                bool isGroup = _convs.Find(c => c.Id == _activeConvId).IsGroup;
                string bubbleSender = msg.Out ? "" :
                    (_senderDisplayNameMap.TryGetValue(msg.Sender, out var dn) && !string.IsNullOrEmpty(dn) ? dn : msg.Sender);
                var bubble = BuildBubble(msg.Text, msg.Out, msg.Time, bubbleSender, isGroup, msg.Id);
                bubble.Tag = msg.Id;

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

        private static string FormatDateHeader(DateTime date)
        {
            var today = DateTime.Today;
            if (date.Date == today)
                return "Today";
            if (date.Date == today.AddDays(-1))
                return "Yesterday";
            if (date.Year == today.Year)
                return date.ToString("MMMM dd");
            return date.ToString("MMMM dd, yyyy");
        }

        private void UpdateChatEmptyStateUI()
        {
            bool hasActiveConversation = !string.IsNullOrWhiteSpace(_activeConvId)
                && _convs.Exists(c => c.Id == _activeConvId);

            _pnlChatHeader.Visible = hasActiveConversation;
            _pnlInputBar.Visible = hasActiveConversation;
            _pnlMessages.Visible = hasActiveConversation;
            _pnlChatEmpty.Visible = !hasActiveConversation;

            // Hide pinned bar when no conversation is active
            if (!hasActiveConversation)
            {
                _pnlPinnedBar.Visible = false;
                _pnlPinnedBottomBar.Visible = false;
                _pnlPinnedPopup.Visible = false;
                _isPinnedPopupOpen = false;
            }

            if (_pnlChatEmpty.Visible)
            {
                _pnlMessages.Controls.Clear();
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                _lblChatEmpty.Text = resources.GetString("ChatEmptySelectMessage")
                    ?? "Select a chat to start messaging";
                LayoutChatEmptyState();
            }
        }

        private void LayoutChatEmptyState()
        {
            if (!_pnlChatEmpty.Visible)
                return;

            _lblChatEmpty.Location = new Point(
                Math.Max(0, (_pnlChatEmpty.Width - _lblChatEmpty.Width) / 2),
                Math.Max(0, (_pnlChatEmpty.Height - _lblChatEmpty.Height) / 2));
        }

        private Panel WrapForwardIfNeeded(Panel inner, string messageId, bool isOut)
        {
            if (string.IsNullOrEmpty(messageId) || !_forwardMetadata.TryGetValue(messageId, out var fwdName))
                return inner;

            string fwdDisplayName = fwdName;
            if (!string.IsNullOrEmpty(_currentUserId))
            {
                bool isSelf = false;
                if (_forwardOriginalSenderId.TryGetValue(messageId, out var origId))
                    isSelf = origId == _currentUserId;
                else if (_usernameToUserId.TryGetValue(fwdName, out var fwdUid))
                    isSelf = fwdUid == _currentUserId;
                if (isSelf)
                    fwdDisplayName = "You";
            }

            var wrapper = new Panel { BackColor = Color.Transparent };

            string fwdFullText = $"Forwarded from {fwdDisplayName}";
            int maxWidth = Math.Max(1, inner.Width);
            int fwdLabelHeight;
            var measureSize = TextRenderer.MeasureText(fwdFullText, TG.FontRegular(8.5f),
                new Size(maxWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            fwdLabelHeight = measureSize.Height + 4;

            var fwdLabel = new Label
            {
                Text = fwdFullText,
                Font = TG.FontRegular(8.5f),
                ForeColor = TG.TextSecondary,
                AutoSize = false,
                Size = new Size(inner.Width, fwdLabelHeight),
                Location = new Point(0, 0),
                BackColor = Color.Transparent,
            };

            int labelHeight = fwdLabel.Height + 4;
            inner.Top = labelHeight;
            inner.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            wrapper.Height = inner.Height + labelHeight;
            wrapper.Width = inner.Width;

            wrapper.Controls.Add(fwdLabel);
            wrapper.Controls.Add(inner);

            wrapper.Resize += (s, e) =>
            {
                int w = wrapper.ClientSize.Width;
                fwdLabel.Width = w;
                var resizeMeasure = TextRenderer.MeasureText(fwdFullText, TG.FontRegular(8.5f),
                    new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                fwdLabel.Height = resizeMeasure.Height + 4;
                inner.Width = w;
                inner.Top = fwdLabel.Height + 4;
                wrapper.Height = inner.Height + fwdLabel.Height + 4;
            };

            return wrapper;
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

                // ── ucAudioBubble: Play/Pause + seekbar + duration ──────────────
                var audioBubble = new SecureChat.Client.Components.Chat.ucAudioBubble(_voicePlaybackService);

                // Resolve AES key now (hoặc defer khi bấm Play — service tự handle)
                if (SecureChat.Shared.Security.KeyManager.TryGetAesKey(messageId, out var aesKey, out var aesIv))
                {
                    double.TryParse(duration, out double durationSec);
                    audioBubble.SetVoiceInfo(messageId, url, expectedSha256, aesKey ?? Array.Empty<byte>(), aesIv ?? Array.Empty<byte>(), durationSec, isOut);
                }
                else
                {
                    // Key chưa có (tin nhắn cũ, chưa trao đổi key) — fallback hiển thị cơ bản
                    double.TryParse(duration, out double durationSec);
                    audioBubble.SetVoiceInfo(messageId, url, expectedSha256,
                        Array.Empty<byte>(), Array.Empty<byte>(), durationSec, isOut);
                }

                audioBubble.Top    = 4;
                audioBubble.Anchor = AnchorStyles.None;
                panel.Height       = audioBubble.Height + 8;
                panel.Resize += (s, e) =>
                {
                    int leftOffset  = (!isOut && isGroup) ? 44 : 10;
                    int rightMargin = 12;
                    int maxW        = panel.ClientSize.Width - leftOffset - rightMargin;
                    audioBubble.Width = Math.Min(360, Math.Max(260, maxW));
                    if (isOut)
                        audioBubble.Left = panel.ClientSize.Width - audioBubble.Width - rightMargin;
                    else
                        audioBubble.Left = leftOffset;
                };

                panel.Controls.Add(audioBubble);

                panel.PerformLayout();
                return WrapForwardIfNeeded(panel, messageId, isOut);
            }

            // file::url::name::size
            const string filePrefix = "file::";
            if (!string.IsNullOrEmpty(text) && text.StartsWith(filePrefix, StringComparison.Ordinal))
            {
                var payload = text.Substring(filePrefix.Length);
                var parts = payload.Split(new[] { "::" }, StringSplitOptions.None);

                string url = parts.Length > 0 ? parts[0] : "";
                // Ghép baseUrl nếu URL là relative (mỗi client tự ghép theo server của họ)
                // Nếu URL chứa localhost, replace bằng server thật của client này
                if (!string.IsNullOrEmpty(url) && url.Contains("://localhost"))
                {
                    var uri = new Uri(url);
                    var baseUri = new Uri(SecureChat.Client.Services.ApiClient.Instance.GetBaseUrl());
                    url = baseUri.Scheme + "://" + baseUri.Host + ":" + baseUri.Port + uri.PathAndQuery;
                }
                else if (!string.IsNullOrEmpty(url) && !url.StartsWith("http"))
                {
                    url = SecureChat.Client.Services.ApiClient.Instance.GetBaseUrl() + url;
                }

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
                        string encryptedTemp = Path.Combine(Path.GetTempPath(), $"file_dl_{Guid.NewGuid():N}.dat");
                        try
                        {
                            await fileCtrl.StartDownloadAsync(async progress =>
                            {
                                await _fileTransfer.DownloadAsync(url, encryptedTemp, progress, cts.Token).ConfigureAwait(false);
                            }).ConfigureAwait(false);

                            // After successful download, verify SHA-256 integrity
                            if (!string.IsNullOrWhiteSpace(expectedSha256))
                            {
                                try
                                {
                                    bool ok = await _fileTransfer.VerifyAsync(encryptedTemp, expectedSha256).ConfigureAwait(false);
                                    if (!ok)
                                    {
                                        this.BeginInvoke(new Action(() => MessageBox.Show(this, "File tải về không khớp hash kiểm tra. File có thể đã bị thay đổi hoặc lỗi truyền tải.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    this.BeginInvoke(new Action(() => MessageBox.Show(this, $"Không thể kiểm tra hash: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                                    return;
                                }
                            }

                            // Decrypt file using cached AES key
                            if (SecureChat.Shared.Security.KeyManager.TryGetAesKey(messageId, out var key, out var iv))
                            {
                                string decrypted = await SecureChat.Client.Services.VoiceEncryptionService.DecryptAsync(encryptedTemp, key, iv).ConfigureAwait(false);
                                if (File.Exists(destination))
                                    File.Delete(destination);
                                File.Move(decrypted, destination);
                                this.BeginInvoke(new Action(() => MessageBox.Show(this, "Tải xuống hoàn tất.", "Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                            }
                            else
                            {
                                // No AES key found — save encrypted file as-is
                                if (File.Exists(destination))
                                    File.Delete(destination);
                                File.Move(encryptedTemp, destination);
                                this.BeginInvoke(new Action(() => MessageBox.Show(this, "Tải xuống hoàn tất (không thể giải mã).", "Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                            }
                        }
                        finally
                        {
                            if (File.Exists(encryptedTemp))
                                try { File.Delete(encryptedTemp); } catch { }
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
                return WrapForwardIfNeeded(panel, messageId, isOut);
            }

            var pnl = new Panel { BackColor = Color.Transparent };
            int pad = 12;

            const int avatarAreaW = 44;
            int leftOffset = (!isOut && isGroup) ? avatarAreaW : 10;
            int maxW = 360;
            if (_pnlMessages != null && _pnlMessages.ClientSize.Width > 0)
                maxW = Math.Max(220, (int)(_pnlMessages.ClientSize.Width * 0.66f) - _pnlMessages.Padding.Horizontal);

            // --- XỬ LÝ FORWARD ---
            string forwardSender = null;
            string actualText = text;

            // Ưu tiên _forwardMetadata (forward mới, không có prefix trong text)
            if (!string.IsNullOrEmpty(messageId) && _forwardMetadata.TryGetValue(messageId, out var fwdName))
            {
                forwardSender = fwdName;
            }
            else
            {
                // Fallback: text prefix (backward compat)
                const string forwardPrefix = "[Forwarded from ";
                if (!string.IsNullOrEmpty(text) && text.StartsWith(forwardPrefix))
                {
                    int endBracket = text.IndexOf(']');
                    if (endBracket > forwardPrefix.Length)
                    {
                        forwardSender = text.Substring(forwardPrefix.Length, endBracket - forwardPrefix.Length);
                        actualText = text.Substring(endBracket + 2).TrimStart();
                    }
                }
            }

            // --- XỬ LÝ TEXT CHO REPLY ---
            string replySender = null;
            string replyText = null;

            const string replyPrefix = "reply::";
            if (!string.IsNullOrEmpty(actualText) && actualText.StartsWith(replyPrefix))
            {
                string payload = actualText.Substring(replyPrefix.Length);
                int firstSep = payload.IndexOf("::");
                if (firstSep > 0)
                {
                    replySender = payload[..firstSep];
                    // Resolve display name; if it's current user → "You"
                    if (replySender != "You")
                    {
                        if (_senderDisplayNameMap.TryGetValue(replySender, out var dn) && !string.IsNullOrEmpty(dn))
                            replySender = dn;
                        else if (replySender == _currentUsername)
                            replySender = "You";
                    }

                    int lastSep = payload.LastIndexOf("::");
                    if (lastSep > firstSep)
                    {
                        replyText = ExtractActualText(payload.Substring(firstSep + 2, lastSep - firstSep - 2));
                        actualText = payload.Substring(lastSep + 2);
                    }
                }
            }

            // Chỉ đo kích thước của phần chữ mới
            Size sz;
            using (var g = _pnlMessages.CreateGraphics())
            {
                sz = TextRenderer.MeasureText(g, actualText, TG.FontRegular(9.5f),
                    new Size(maxW - pad * 2, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            }

            int statusHeight = 16;
            int senderHeight = (!isOut && isGroup && !string.IsNullOrEmpty(sender)) ? 19 : 0;
            int senderMinWidth = 0;
            if (!isOut && isGroup && !string.IsNullOrEmpty(sender))
            {
                using var gSender = _pnlMessages.CreateGraphics();
                senderMinWidth = (int)gSender.MeasureString(sender, TG.FontSemiBold(8f)).Width + pad * 2 + 10;
            }
            int replyBlockHeight = (replySender != null) ? 38 : 0;

            // --- FORWARD HEADER MEASUREMENT ---
            string fwdDisplayName = forwardSender;
            int forwardHeaderHeight = 0;
            float fwdPrefixWidth = 0;
            float fwdPrefixLineH = 0;
            float fwdNameMeasuredH = 0;
            float fwdNameMeasuredW = 0;
            bool fwdNameWraps = false;

            if (forwardSender != null)
            {
                // "Forwarded from You" if original sender is current user
                bool isSelf = false;
                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    if (!string.IsNullOrEmpty(messageId) && _forwardOriginalSenderId.TryGetValue(messageId, out var origId))
                        isSelf = origId == _currentUserId;
                    else if (_usernameToUserId.TryGetValue(forwardSender, out var fwdUid))
                        isSelf = fwdUid == _currentUserId;
                }
                if (isSelf)
                    fwdDisplayName = "You";

                using (var g = _pnlMessages.CreateGraphics())
                {
                    string fwdPrefix = "Forwarded from ";
                    fwdPrefixWidth = g.MeasureString(fwdPrefix, TG.FontRegular(8.5f)).Width;

                    float avail = Math.Max(100, maxW - pad * 2 - 10);
                    using var sf = new StringFormat(StringFormatFlags.LineLimit);
                    var nameMeasured = g.MeasureString(fwdDisplayName, TG.FontSemiBold(8.5f), new SizeF(avail, 999), sf);
                    fwdNameMeasuredW = nameMeasured.Width;
                    fwdNameMeasuredH = nameMeasured.Height;

                    fwdNameWraps = fwdNameMeasuredW > (avail - fwdPrefixWidth);

                    if (fwdNameWraps)
                    {
                        fwdPrefixLineH = g.MeasureString(fwdPrefix.TrimEnd(), TG.FontRegular(8.5f), new SizeF(avail, 999), sf).Height;
                        forwardHeaderHeight = (int)Math.Ceiling(fwdPrefixLineH + 3 + fwdNameMeasuredH + 4);
                    }
                    else
                    {
                        fwdPrefixLineH = g.MeasureString(fwdPrefix.TrimEnd(), TG.FontRegular(8.5f)).Height;
                        forwardHeaderHeight = 24;
                    }
                }
            }

            int minBw = (replySender != null) ? 160 : 100;
            minBw = Math.Max(minBw, senderMinWidth);
            if (forwardSender != null)
            {
                int fwdMinBw = (int)(fwdPrefixWidth + Math.Min(fwdNameMeasuredW, maxW - pad * 2 - 10 - fwdPrefixWidth) + pad * 2 + 10);
                minBw = Math.Max(minBw, Math.Min(maxW, fwdMinBw));
            }
            int bw = Math.Min(maxW, Math.Max((int)sz.Width + pad * 2 + 10, minBw));

            int bh = (int)sz.Height + pad * 2 + statusHeight + senderHeight + replyBlockHeight + forwardHeaderHeight;
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

                    currentY += 40;
                }

                // 2.5 FORWARD HEADER
                if (forwardSender != null)
                {
                    float fwdAccentX = x + pad;
                    float fwdTextX = fwdAccentX + 10;
                    float fwdAvailW = bw - pad * 2 - 10;

                    using var fwdBgBrush = new SolidBrush(Color.FromArgb(18, 0, 0, 0));
                    using var accentPen = new SolidBrush(Color.FromArgb(120, 120, 120));

                    if (!fwdNameWraps)
                    {
                        var fwdRect = new Rectangle(x + pad, (int)currentY, bw - pad * 2, 20);
                        using var fwdPath = RoundedPanel.GetRoundedPath(fwdRect, 4);
                        e.Graphics.FillPath(fwdBgBrush, fwdPath);

                        e.Graphics.FillRectangle(accentPen, fwdAccentX, currentY + 2, 3, 16);

                        e.Graphics.DrawString("Forwarded from ", TG.FontRegular(8.5f), new SolidBrush(TG.TextSecondary), fwdTextX, currentY + 1);

                        using var fwdNameBrush = new SolidBrush(TG.Blue);
                        e.Graphics.DrawString(fwdDisplayName, TG.FontSemiBold(8.5f), fwdNameBrush, fwdTextX + fwdPrefixWidth, currentY + 1);

                        currentY += 24;
                    }
                    else
                    {
                        float totalH = forwardHeaderHeight;

                        var fwdRect = new Rectangle(x + pad, (int)currentY, bw - pad * 2, (int)totalH);
                        using var fwdPath = RoundedPanel.GetRoundedPath(fwdRect, 4);
                        e.Graphics.FillPath(fwdBgBrush, fwdPath);

                        e.Graphics.FillRectangle(accentPen, fwdAccentX, currentY + 2, 3, (int)totalH - 4);

                        e.Graphics.DrawString("Forwarded from", TG.FontRegular(8.5f), new SolidBrush(TG.TextSecondary), fwdTextX, currentY + 2);

                        float nameStartY = currentY + 2 + fwdPrefixLineH + 3;
                        var nameRect = new RectangleF(fwdTextX, nameStartY, fwdAvailW, fwdNameMeasuredH);
                        using var sf = new StringFormat(StringFormatFlags.LineLimit);
                        using var fwdNameBrush = new SolidBrush(TG.Blue);
                        e.Graphics.DrawString(fwdDisplayName, TG.FontSemiBold(8.5f), fwdNameBrush, nameRect, sf);

                        currentY += totalH + 4;
                    }
                }

                // 3. Text tin nhắn chính - dùng TextRenderer để render emoji đúng
                var textRect = new Rectangle(x + pad, (int)currentY, bw - pad * 2, sz.Height);
                TextRenderer.DrawText(e.Graphics, actualText, TG.FontRegular(9.5f), textRect,
                    TG.TextPrimary, TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                // 4. Thời gian và dấu tick ✓✓
                var timeSz = TextRenderer.MeasureText(time, TG.FontRegular(7.5f));
                float tx = x + bw - timeSz.Width - pad - (isOut ? 26 : 0);
                float ty = y + bh - timeSz.Height - 6;
                e.Graphics.DrawString(time, TG.FontRegular(7.5f), new SolidBrush(TG.TextTime), tx, ty);

                if (isOut)
                {
                    float tickX = x + bw - pad - 22;
                    using var tickFont = new Font("Segoe UI Symbol", 8f, FontStyle.Bold);

                    // Lấy delivery status — default Sent nếu chưa có
                    var delivery = (!string.IsNullOrEmpty(messageId) && _msgDelivery.TryGetValue(messageId, out var ds))
                        ? ds : SecureChat.DTOs.DeliveryStatus.Sent;

                    string tickText;
                    Color tickColor;
                    switch (delivery)
                    {
                        case SecureChat.DTOs.DeliveryStatus.Read:
                            tickText  = "✓✓";
                            tickColor = TG.Blue;       // 2 tick xanh = đã đọc
                            break;
                        case SecureChat.DTOs.DeliveryStatus.Delivered:
                            tickText  = "✓✓";
                            tickColor = Color.Gray;    // 2 tick xám = đã nhận
                            break;
                        default:
                            tickText  = "✓";
                            tickColor = Color.Gray;    // 1 tick xám = đã gửi
                            break;
                    }
                    e.Graphics.DrawString(tickText, tickFont, new SolidBrush(tickColor), tickX, ty - 1);
                }

                // 5. Self-destruct timer indicator (nếu message có expiration)
                if (!string.IsNullOrWhiteSpace(messageId) && _expirationService.IsTracking(messageId))
                {
                    int? remainingSeconds = _expirationService.GetRemainingSeconds(messageId);
                    if (remainingSeconds.HasValue && remainingSeconds.Value > 0)
                    {
                        string timerText = FormatRemainingTime(remainingSeconds.Value);
                        using var timerFont = TG.FontSemiBold(7.5f);
                        var timerSz = e.Graphics.MeasureString(timerText, timerFont);
                        float timerX = x + pad;
                        float timerY = y + bh - timerSz.Height - 6;

                        // Draw timer icon and text
                        e.Graphics.DrawString("⏱", new Font("Segoe UI Emoji", 8f), new SolidBrush(Color.FromArgb(255, 87, 34)), timerX, timerY - 1);
                        e.Graphics.DrawString(timerText, timerFont, new SolidBrush(Color.FromArgb(255, 87, 34)), timerX + 14, timerY);
                    }
                }
            };

            // Tạo Context Menu
            var actions = new SecureChat.Client.Forms.Chat.MessageActions
            {
                Reply = id => OnReplyMessage(id),
                Forward = id => OnForwardMessage(id),
                Copy = id => OnCopyMessage(id),
                Edit = isOut ? (id => OnEditMessage(id)) : null,
                Pin = id =>
                {
                    if (_pinnedMessageIds.Contains(id)) OnUnpinMessage(id);
                    else OnPinMessage(id);
                },
                Delete = id => OnDeleteMessage(id, isOut)
            };
            pnl.ContextMenuStrip = SecureChat.Client.Forms.Chat.frmRightClickMessageMenu.Create(messageId, actions, null, _pinnedMessageIds.Contains(messageId));

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



        private async void OnForwardMessage(string messageId)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            var msg = _currentMsgs[msgIndex];

            using var dlg = new SecureChat.Client.Forms.Chat.frmForwardMessage(_convs, _activeConvId);
            if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedConversationId))
                return;

            string targetConversationId = dlg.SelectedConversationId;

            try
            {
                // Lấy display name của người gửi gốc.
                // Nếu message được forward (có _forwardMetadata), giữ nguyên chuỗi forward gốc.
                string senderName;
                if (!string.IsNullOrEmpty(messageId) && _forwardMetadata.TryGetValue(messageId, out var origFwdName))
                    senderName = origFwdName;
                else
                    senderName = string.IsNullOrEmpty(msg.Sender) ? "You"
                        : _senderDisplayNameMap.TryGetValue(msg.Sender, out var dn) && !string.IsNullOrEmpty(dn) ? dn : msg.Sender;

                // Xây nội dung forward: dùng rawContent (luôn là nội dung thật, không có prefix forward)
                string rawContent = StripForwardPrefix(msg.Text ?? string.Empty);
                const string voicePrefix = "voice::";
                const string filePrefix = "file::";

                string forwardText;
                if (rawContent.StartsWith(voicePrefix, StringComparison.Ordinal))
                {
                    forwardText = rawContent; // giữ nguyên protocol text
                }
                else if (rawContent.StartsWith(filePrefix, StringComparison.Ordinal))
                {
                    forwardText = rawContent; // giữ nguyên protocol text
                }
                else
                {
                    forwardText = ExtractActualText(rawContent); // text thuần
                }

                // Mã hóa nội dung với key của cuộc trò chuyện đích
                byte[]? conversationKey = await _decryptor.EnsureConversationKeyAsync(targetConversationId);
                if (conversationKey is null || conversationKey.Length != SecureChat.Shared.Security.AesEncryption.KeySize)
                {
                    MessageBox.Show(this, "Không thể lấy khóa mã hóa cho cuộc trò chuyện đích.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var encryptionService = new MessageEncryptionService();
                if (!encryptionService.ValidateConversationKey(conversationKey))
                {
                    MessageBox.Show(this, "Khóa mã hóa không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var (encryptedContent, contentIV) = encryptionService.EncryptMessage(forwardText, conversationKey);

                // Nếu message nguồn đã là forward, giữ nguyên chuỗi forward gốc (OriginalSenderID)
                string? originalSenderId = null;
                if (!string.IsNullOrEmpty(messageId) && _forwardOriginalSenderId.TryGetValue(messageId, out var chainOrigId))
                    originalSenderId = chainOrigId;
                else if (!string.IsNullOrEmpty(msg.Sender) && _usernameToUserId.TryGetValue(msg.Sender, out var uid))
                    originalSenderId = uid;
                else if (string.IsNullOrEmpty(msg.Sender))
                    originalSenderId = _currentUserId;

                var req = new SendMessageRequest(
                    Type: MessageType.Text,
                    Content: encryptedContent,
                    ContentIV: contentIV,
                    ReplyToID: null,
                    OriginalSenderID: originalSenderId,
                    Attachments: null,
                    MentionedMemberIDs: null,
                    ExpiresAfterSeconds: null
                );

                var (ok, messageResponse, err) = await ApiClient.Instance.PostAsync<SendMessageRequest, MessageResponse>(
                    $"api/conversations/{targetConversationId}/messages", req
                );

                if (!ok || messageResponse is null)
                {
                    MessageBox.Show(this, $"Lỗi forward: {err}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Đánh dấu message ID đã xử lý để SignalR handler không tạo duplicate
                // Chỉ cần khi forward vào chính conversation đang mở (đã thêm manual vào _currentMsgs ở dưới).
                // Khi forward sang conversation khác, để SignalR hoặc sync xử lý thêm vào _allMsgs.
                if (targetConversationId == _activeConvId)
                {
                    lock (_processedMessageIdsLock)
                    {
                        _processedMessageIds.Add(messageResponse.MessageID);
                    }
                }

                // Broadcast qua SignalR để các member khác nhận realtime
                if (_signalRClient is not null && _signalRClient.IsConnected)
                {
                    try
                    {
                        await _signalRClient.SendMessageAsync(targetConversationId, messageResponse);
                    }
                    catch { /* Real-time không phải lỗi nghiêm trọng */ }
                }

                string timeStr = messageResponse.SentAt.ToLocalTime().ToString("h:mm tt");

                // Lưu forward metadata để render header
                _forwardMetadata[messageResponse.MessageID] = senderName;
                if (!string.IsNullOrEmpty(originalSenderId))
                    _forwardOriginalSenderId[messageResponse.MessageID] = originalSenderId;

                // Nếu đang ở cuộc trò chuyện đích, thêm vào UI ngay
                if (targetConversationId == _activeConvId)
                {
                    _messageDates[messageResponse.MessageID] = messageResponse.SentAt.ToLocalTime();
                    _currentMsgs.Add((messageResponse.MessageID, forwardText, true, timeStr, ""));
                    BuildMessages();
                }

                // Cập nhật sidebar cho cuộc trò chuyện đích (preview + reorder)
                RefreshConversationItem(targetConversationId, forwardText, true, "", timeStr, messageId: messageResponse.MessageID);

                MessageBox.Show(this, "Đã chuyển tiếp tin nhắn thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Lỗi forward: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnReplyMessage(string messageId)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            var msg = _currentMsgs[msgIndex];
            _replyingToMessageId = msg.Id;

            string replyDisplayName;
            if (string.IsNullOrEmpty(msg.Sender) || msg.Sender == _currentUsername)
                replyDisplayName = "You";
            else if (_senderDisplayNameMap.TryGetValue(msg.Sender, out var dn) && !string.IsNullOrEmpty(dn))
                replyDisplayName = dn;
            else
                replyDisplayName = msg.Sender;
            _lblReplySender.Text = replyDisplayName;

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
                UpdatePinnedBar();
                RefreshSidebarPreview();
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
            else
            {
                // Track local delete để filter khi re-sync
                _hiddenMessageIds.Add(messageId);
                lock (_processedMessageIdsLock)
                    _processedMessageIds.Add(messageId);
            }

    _currentMsgs.RemoveAt(msgIndex);
    _forwardMetadata.TryRemove(messageId, out _);
    _forwardOriginalSenderId.TryRemove(messageId, out _);
    if (_pinnedMessageIds.Remove(messageId))
        UpdatePinnedBar();
    BuildMessages();
            RefreshSidebarPreview();
        }

        private async Task LoadPinsAsync(string convId)
        {
            try
            {
                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.GetAsync($"api/conversations/{convId}/messages/pins");
                if (!res.IsSuccessStatusCode) return;

                var json = await res.Content.ReadAsStringAsync();
                var pins = System.Text.Json.JsonSerializer.Deserialize<List<PinResponse>>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (pins is null) return;

                _pinnedMessageIds.Clear();
                foreach (var p in pins)
                    _pinnedMessageIds.Add(p.MessageID);

                if (_activeConvId == convId)
                    UpdatePinnedBar();
            }
            catch { /* silently ignore */ }
        }

        private async void OnPinMessage(string messageId)
        {
            if (_pinnedMessageIds.Count >= 3)
            {
                BeginInvoke(new Action(() =>
                    MessageBox.Show(this, "Chỉ được ghim tối đa 3 tin nhắn.", "Giới hạn", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
                return;
            }

            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            var res = await http.PostAsync(
                $"api/conversations/{_activeConvId}/messages/{messageId}/pin",
                null);

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                BeginInvoke(new Action(() =>
                    MessageBox.Show(this, $"Lỗi ghim: {err}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                return;
            }

            _pinnedMessageIds.Add(messageId);
            UpdatePinnedBar();
            BuildMessages();
        }

        private async void OnUnpinMessage(string? messageId = null)
        {
            if (string.IsNullOrWhiteSpace(messageId))
            {
                if (_pinnedMessageIds.Count == 0) return;
                messageId = _pinnedMessageIds.First();
            }

            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            var res = await http.DeleteAsync(
                $"api/conversations/{_activeConvId}/messages/{messageId}/pin");

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                BeginInvoke(new Action(() =>
                    MessageBox.Show(this, $"Lỗi bỏ ghim: {err}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                return;
            }

            _pinnedMessageIds.Remove(messageId);
            UpdatePinnedBar();
            BuildMessages();
        }

        private void UpdatePinnedBar()
        {
            if (_pinnedMessageIds.Count == 0)
            {
                _pnlPinnedBar.Visible = false;
                _pnlPinnedBottomBar.Visible = false;
                _pnlPinnedPopup.Visible = false;
                _isPinnedPopupOpen = false;
                return;
            }

            // Remove stale pin IDs whose messages are no longer loaded
            _pinnedMessageIds.RemoveWhere(pid => !_currentMsgs.Exists(m => m.Id == pid));
            if (_pinnedMessageIds.Count == 0)
            {
                _pnlPinnedBar.Visible = false;
                _pnlPinnedBottomBar.Visible = false;
                _pnlPinnedPopup.Visible = false;
                _isPinnedPopupOpen = false;
                return;
            }

            var firstId = _pinnedMessageIds.First();
            var firstMsg = _currentMsgs.Find(m => m.Id == firstId);
            string preview = GetPinnedDisplayText(firstMsg.Text);

            var count = _pinnedMessageIds.Count;
            string truncatedPreview = TruncateText(preview, 60);
            _lblPinnedText.Text = count == 1
                ? $"Pin: {truncatedPreview}"
                : $"{count} pins: {truncatedPreview}";

            _lblPinnedBottomText.Text = count == 1
                ? "Pinned message"
                : $"{count} pinned messages";

            _pnlPinnedBar.Visible = true;
            _pnlPinnedBottomBar.Visible = true;

            // Rebuild popup if open
            if (_isPinnedPopupOpen)
                RebuildPinnedPopup();
        }

        private string GetPinnedDisplayText(string? text)
        {
            if (text is null) return "(media)";
            string cleaned = ExtractActualText(text);
            if (cleaned.StartsWith("voice::")) return "📞 Voice message";
            if (cleaned.StartsWith("file::")) return "📎 File";
            return cleaned;
        }

        private string TruncateText(string text, int maxLen = 80)
            => text.Length > maxLen ? text[..maxLen] + "…" : text;

        private void RebuildPinnedPopup()
        {
            _pnlPinnedPopup.Controls.Clear();
            if (_pinnedMessageIds.Count == 0)
            {
                _pnlPinnedPopup.Height = 0;
                return;
            }

            int itemH = 40;

            foreach (var pid in _pinnedMessageIds)
            {
                var msg = _currentMsgs.Find(m => m.Id == pid);
                string displayText;
                string senderName = "Unknown";

                if (msg.Id is not null)
                {
                    displayText = GetPinnedDisplayText(msg.Text);
                    if (msg.Out)
                        senderName = "You";
                    else if (!string.IsNullOrEmpty(msg.Sender))
                    {
                        if (_senderDisplayNameMap.TryGetValue(msg.Sender, out var dn) && !string.IsNullOrEmpty(dn))
                            senderName = dn;
                        else senderName = msg.Sender;
                    }
                }
                else
                {
                    displayText = "Pinned message";
                }

                // Item panel
                var item = new Panel
                {
                    Height = itemH,
                    Dock = DockStyle.Top,
                    BackColor = Color.White,
                    Cursor = Cursors.Hand,
                    Tag = pid,
                };

                // Hover highlight
                item.MouseEnter += (_, __) => item.BackColor = Color.FromArgb(0xF0, 0xF0, 0xF0);
                item.MouseLeave += (_, __) => item.BackColor = Color.White;

                // Sender name
                int itemTextW = Math.Max(50, _pnlPinnedBar.Width - 56);
                var lblSender = new Label
                {
                    Text = TruncateText(senderName, 30),
                    Font = TG.FontSemiBold(9f),
                    ForeColor = TG.Blue,
                    AutoSize = false,
                    Height = 16,
                    Width = itemTextW,
                    Location = new Point(8, 4),
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft,
                };

                // Message text (2-line truncation)
                var lblText = new Label
                {
                    Text = TruncateText(displayText, 80),
                    Font = TG.FontRegular(9f),
                    ForeColor = TG.TextPrimary,
                    AutoSize = false,
                    Height = 18,
                    Width = itemTextW,
                    Location = new Point(8, 20),
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft,
                };

                // Unpin button per item
                var btnItemUnpin = new Button
                {
                    Text = "✕",
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(18, 18),
                    Location = new Point(_pnlPinnedBar.Width - 28, 11),
                    Cursor = Cursors.Hand,
                    ForeColor = TG.TextSecondary,
                    Tag = pid,
                };
                btnItemUnpin.FlatAppearance.BorderSize = 0;
                btnItemUnpin.Click += (s, _) =>
                {
                    var btn = (Button)s!;
                    var id = (string)btn.Tag!;
                    _isPinnedPopupOpen = false;
                    _pnlPinnedPopup.Visible = false;
                    OnUnpinMessage(id);
                };

                // Click item → scroll to message
                void OnItemClick(object? s, EventArgs e)
                {
                    _isPinnedPopupOpen = false;
                    _pnlPinnedPopup.Visible = false;
                    ScrollToMessage(pid);
                }
                item.Click += OnItemClick;
                lblSender.Click += OnItemClick;
                lblText.Click += OnItemClick;

                item.Controls.Add(btnItemUnpin);
                item.Controls.Add(lblText);
                item.Controls.Add(lblSender);
                _pnlPinnedPopup.Controls.Add(item);
            }

            // Bottom separator line
            var sep = new Panel
            {
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = TG.Divider,
            };
            _pnlPinnedPopup.Controls.Add(sep);

            _pnlPinnedPopup.Height = _pinnedMessageIds.Count * itemH + 1;
        }

        private void ScrollToMessage(string messageId)
        {
            // Close popup first
            _isPinnedPopupOpen = false;
            _pnlPinnedPopup.Visible = false;

            foreach (Control c in _pnlMessages.Controls)
            {
                if (c.Tag is string tag && tag == messageId && c is Panel pnl)
                {
                    _pnlMessages.ScrollControlIntoView(pnl);

                    // Flash highlight: add a colored border overlay
                    var flash = new Panel
                    {
                        BackColor = Color.FromArgb(60, 0x52, 0x9C, 0xFF),
                        Dock = DockStyle.Fill,
                        Enabled = false,
                    };
                    pnl.Controls.Add(flash);
                    flash.BringToFront();

                    var timer = new System.Windows.Forms.Timer { Interval = 1200 };
                    timer.Tick += (_, __) =>
                    {
                        pnl.Controls.Remove(flash);
                        flash.Dispose();
                        timer.Stop();
                        timer.Dispose();
                    };
                    timer.Start();
                    return;
                }
            }
        }

        private async Task InitializeSignalRAsync()
        {
            if (_signalRClient is not null)
                return;

            _signalRClient = new SecureChat.Client.Services.RealTime.SignalRClient(
                () => Task.FromResult(SecureChat.Client.Services.ApiClient.Instance.CurrentAccessToken));

            _signalRClient.MessageReceived += HandleSignalRMessageAsync;
            _signalRClient.CallSignalReceived += HandleSignalRCallSignalAsync;
            _signalRClient.CallIncoming += HandleCallIncomingAsync;
            _signalRClient.UserTyping += HandleUserTypingAsync;
            _signalRClient.UserStoppedTyping += HandleUserStoppedTypingAsync;
            _signalRClient.MessageStatusUpdated += HandleMessageStatusUpdatedAsync;
            _signalRClient.Reconnected += async _ =>
            {
                await ReRegisterPublicKeyAsync();
                if (!string.IsNullOrWhiteSpace(_activeConvId))
                    await _signalRClient.JoinConversationAsync(_activeConvId);
            };

            try
            {
                await _signalRClient.StartAsync();
                if (!string.IsNullOrWhiteSpace(_activeConvId))
                    await _signalRClient.JoinConversationAsync(_activeConvId);
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "SignalR", MessageBoxButtons.OK, MessageBoxIcon.Error)));
            }
        }

        private async Task JoinConversationSignalRAsync(string conversationId)
        {
            if (_signalRClient is null)
                return;
            try
            {
                await _signalRClient.JoinConversationAsync(conversationId);
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "SignalR", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
        }

        private async Task HandleSignalRMessageAsync(MessageResponse message)
        {
            try
            {
                if (!TryTrackMessageId(message.MessageID))
                    return;

                // Báo delivered cho server ngay khi nhận tin (nếu không phải tin của mình)
                if (message.SenderID != _currentUserId)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                            await http.PostAsync($"api/conversations/{message.ConversationID}/messages/{message.MessageID}/delivered", null);
                        }
                        catch { /* không ảnh hưởng UX */ }
                    });

                    // Khi nhận tin từ người khác → gọi API MarkDelivered để server
                    // lưu DB và push SignalR "Delivered" về cho người gửi
                }

                // Resolve memberId của user hiện tại trong conv để xác định "isOut".
                // Nếu chưa biết (do chưa từng mở conv này) thì fetch /members/me.
                if (!_myMemberIdByConv.TryGetValue(message.ConversationID, out var myMemberId))
                {
                    var (ok, me, _) = await _messageService.GetMyMembershipAsync(message.ConversationID);
                    if (ok && me is not null)
                    {
                        myMemberId = me.MemberID;
                        _myMemberIdByConv[message.ConversationID] = myMemberId;
                    }
                }

                var dm = await _decryptor.ProcessAsync(message, myMemberId);

                // Track message expiration nếu có ExpiresAt
                if (message.ExpiresAt.HasValue)
                {
                    _expirationService.TrackMessage(message.MessageID, message.ExpiresAt.Value);
                }

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (!_allMsgs.TryGetValue(message.ConversationID, out var list))
                        {
                            list = new List<(string Id, string Text, bool Out, string Time, string Sender)>();
                            _allMsgs[message.ConversationID] = list;
                        }
                        if (!dm.Out && !string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(dm.SenderDisplayName))
                            _senderDisplayNameMap[dm.Sender] = dm.SenderDisplayName;
                        if (!string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(message.SenderUserID))
                            _usernameToUserId[dm.Sender] = message.SenderUserID;
                        if (!string.IsNullOrEmpty(message.OriginalSenderID) && !string.IsNullOrEmpty(message.OriginalSenderName))
                        {
                            _forwardMetadata[dm.Id] = message.OriginalSenderName;
                            _forwardOriginalSenderId[dm.Id] = message.OriginalSenderID!;
                        }
                        _messageDates[dm.Id] = message.SentAt.ToLocalTime();
                        list.Add((dm.Id, dm.Text, dm.Out, dm.Time, dm.Sender));

                        // Cập nhật preview ở sidebar (best-effort) và đưa lên đầu.
                        int idx = _convs.FindIndex(c => c.Id == message.ConversationID);
                        if (idx >= 0)
                        {
                            var c = _convs[idx];
                            int unread = (message.ConversationID == _activeConvId || dm.Out) ? c.Unread : c.Unread + 1;
                            string senderForPreview = dm.Out ? "" : dm.Sender;
                            if (!dm.Out && !string.IsNullOrEmpty(dm.Sender) && _senderDisplayNameMap.TryGetValue(dm.Sender, out var dn) && !string.IsNullOrEmpty(dn))
                                senderForPreview = dn;
                            RefreshConversationItem(message.ConversationID, dm.Text, dm.Out, senderForPreview, dm.Time, unread, dm.Id);
                        }

                        if (message.ConversationID == _activeConvId)
                            BuildMessages();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SignalR] UI update failed: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] HandleSignalRMessageAsync failed: {ex.Message}");
            }
        }

        private Task HandleSignalRCallSignalAsync(string callId, string signal)
        {
            return Task.CompletedTask;
        }

        private async Task HandleCallIncomingAsync(string callId, string callerName, int callType, string conversationId)
        {
            if (IsDisposed) return;

            bool accepted = false;
            if (InvokeRequired)
                accepted = (bool)Invoke(new Func<bool>(() =>
                {
                    var form = new Forms.Call.frmIncomingCall(callerName, callType);
                    form.ShowDialog(this);
                    return form.Accepted;
                }));
            else
            {
                var form = new Forms.Call.frmIncomingCall(callerName, callType);
                form.ShowDialog(this);
                accepted = form.Accepted;
            }

            if (!accepted)
            {
                try
                {
                    if (_signalRClient != null)
                        await _signalRClient.SendCallSignalAsync(callId, "CALL_REJECTED");
                }
                catch { }
                return;
            }

            try
            {
                var http = ApiClient.Instance.GetHttpClient();
                var joinResponse = await http.PostAsync($"api/conversations/{conversationId}/calls/{callId}/join", null);
                if (!joinResponse.IsSuccessStatusCode) return;
            }
            catch { return; }

            try
            {
                if (_signalRClient != null)
                    await _signalRClient.SendCallSignalAsync(callId, "CALL_JOINED");
            }
            catch { }

            if (IsDisposed) return;

            BeginInvoke(new Action(() =>
            {
                var callForm = new Forms.Call.frmVideoCall(callerName, callId, conversationId, _signalRClient!);
                callForm.Show();
            }));
        }

        private async Task ReRegisterPublicKeyAsync()
        {
            var (pub, priv) = SecureChat.Shared.Security.KeyManager.GetKeyPair();
            if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(priv))
            {
                var (publicKey, privateKey) = SecureChat.Shared.Security.RSAEncryption.GenerateKeyPair();
                SecureChat.Shared.Security.KeyManager.SetKeyPair(publicKey, privateKey);
                pub = publicKey;
            }

            try
            {
                await SecureChat.Client.Services.ApiClient.Instance.RegisterPublicKeyAsync(pub);
            }
            catch (InvalidOperationException ex)
            {
                BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "SignalR", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            }
        }

        private Task HandleUserTypingAsync(string conversationId, string username)
        {
            if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(username))
                return Task.CompletedTask;

            lock (_typingLock)
            {
                if (!_typingUsernames.TryGetValue(conversationId, out var set))
                {
                    set = new HashSet<string>();
                    _typingUsernames[conversationId] = set;
                }
                set.Add(username);
            }

            if (conversationId == _activeConvId)
            {
                string displayName = _senderDisplayNameMap.TryGetValue(username, out var dn) && !string.IsNullOrEmpty(dn) ? dn : username;
                BeginInvoke(new Action(() =>
                {
                    if (_lblChatStatus != null)
                        _lblChatStatus.Text = $"{displayName} đang gõ...";
                }));
            }

            return Task.CompletedTask;
        }

        private Task HandleUserStoppedTypingAsync(string conversationId, string username)
        {
            if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(username))
                return Task.CompletedTask;

            bool shouldUpdate = false;
            lock (_typingLock)
            {
                if (_typingUsernames.TryGetValue(conversationId, out var set))
                {
                    if (set.Remove(username) && set.Count == 0)
                    {
                        _typingUsernames.Remove(conversationId);
                        shouldUpdate = true;
                    }
                }
            }

            if (shouldUpdate && conversationId == _activeConvId)
            {
                BeginInvoke(new Action(() => RestoreChatStatus()));
            }

            return Task.CompletedTask;
        }

        private Task HandleMessageStatusUpdatedAsync(string messageId, string status)
        {
            if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(status))
                return Task.CompletedTask;

            // Cập nhật _msgDelivery dictionary
            var newDelivery = status switch
            {
                "Read"      => SecureChat.DTOs.DeliveryStatus.Read,
                "Delivered" => SecureChat.DTOs.DeliveryStatus.Delivered,
                _           => SecureChat.DTOs.DeliveryStatus.Sent,
            };

            // Chỉ upgrade (Sent→Delivered→Read), không downgrade
            var upgraded = false;
            _msgDelivery.AddOrUpdate(messageId,
                addValueFactory:    _ => { upgraded = true; return newDelivery; },
                updateValueFactory: (_, cur) =>
                {
                    if (cur < newDelivery) { upgraded = true; return newDelivery; }
                    return cur;
                });
            if (!upgraded) return Task.CompletedTask;

            // Repaint bubble tương ứng trên UI thread
            BeginInvoke(new Action(() =>
            {
                foreach (Control c in _pnlMessages.Controls)
                {
                    if (c.Tag is string tag && tag == messageId)
                    {
                        c.Invalidate(true); // trigger OnPaint lại — tick sẽ đọc _msgDelivery mới
                        break;
                    }
                }
            }));

            return Task.CompletedTask;
        }

        private void RestoreChatStatus()
        {
            if (_lblChatStatus == null || string.IsNullOrWhiteSpace(_activeConvId))
                return;

            lock (_typingLock)
            {
                if (_typingUsernames.TryGetValue(_activeConvId, out var set) && set.Count > 0)
                {
                    var first = set.First();
                    _lblChatStatus.Text = $"{first} đang gõ...";
                    return;
                }
            }

            var conv = _convs.Find(c => c.Id == _activeConvId);
            _lblChatStatus.Text = conv == default ? "" : conv.IsGroup ? $"{conv.Name}" : "last seen recently";
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
            string conversationId,
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
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new InvalidOperationException("Conversation id is required.");
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

            // ─────────────────────────────────────────────────────────────────
            // Lấy danh sách members của conversation
            // ─────────────────────────────────────────────────────────────────
            var convService = new SecureChat.Client.Services.ConversationService();
            var (ok, members, err) = await convService.GetConversationMembersAsync(conversationId);

            if (!ok || members is null || members.Count == 0)
                throw new InvalidOperationException($"Failed to get conversation members: {err}");

            // ─────────────────────────────────────────────────────────────────
            // Encrypt AES key cho mỗi member
            // ─────────────────────────────────────────────────────────────────
            var recipientEncryptions = new List<SecureChat.DTOs.RecipientEncryption>();

            foreach (var member in members)
            {
                if (member.User is null)
                    continue;

                try
                {
                    byte[] encryptedAesKey = SecureChat.Shared.Security.RSAEncryption.Encrypt(aesKey, member.User.PublicKey);
                    byte[] encryptedAesIv = SecureChat.Shared.Security.RSAEncryption.Encrypt(aesIv, member.User.PublicKey);

                    recipientEncryptions.Add(new SecureChat.DTOs.RecipientEncryption(
                        member.UserID,
                        Convert.ToBase64String(encryptedAesKey),
                        Convert.ToBase64String(encryptedAesIv)
                    ));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to encrypt AES key for recipient {member.UserID}: {ex.Message}", ex);
                }
            }

            if (recipientEncryptions.Count == 0)
                throw new InvalidOperationException("No recipients with valid public keys found.");

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
                null,
                null,
                null,
                recipientEncryptions);
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
        private async void SendMessage()
        {
            // ─────────────────────────────────────────────────────────────────
            // VALIDATION: Kiểm tra input và trạng thái
            // ─────────────────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(_tbMessage.Text))
            {
                return;
            }

            string text = _tbMessage.Text.Trim();

            // Validate message length (tránh spam hoặc message quá dài)
            if (text.Length > 4096)
            {
                MessageBox.Show(this,
                    "Tin nhắn quá dài. Vui lòng giới hạn trong 4096 ký tự.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Kiểm tra có conversation đang active không
            if (string.IsNullOrWhiteSpace(_activeConvId))
            {
                MessageBox.Show(this,
                    "Vui lòng chọn một cuộc trò chuyện trước khi gửi tin nhắn.",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Clear input ngay lập tức để UX mượt mà
            _tbMessage.Text = "";

            // ─────────────────────────────────────────────────────────────────
            // PREPARE: Xử lý reply context nếu có
            // ─────────────────────────────────────────────────────────────────
            string? replyToId = null;
            string finalMessageText = text;

            if (!string.IsNullOrEmpty(_replyingToMessageId))
            {
                var origMsg = _currentMsgs.Find(m => m.Id == _replyingToMessageId);
                if (origMsg.Id != null)
                {
                    replyToId = _replyingToMessageId;
                    // Store USERNAME in reply payload (never "You" - it's sender-relative)
                    string origSender = string.IsNullOrEmpty(origMsg.Sender) ? _currentUsername : origMsg.Sender;
                    // Format: reply::SenderUsername::OriginalText::NewText
                    finalMessageText = $"reply::{origSender}::{ExtractActualText(origMsg.Text)}::{text}";
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // UI CLEANUP: Dọn dẹp giao diện reply context
            // ─────────────────────────────────────────────────────────────────
            _pnlReplyContext.Visible = false;
            _pnlInputBar.Height = 56;
            _replyingToMessageId = null;

            // ─────────────────────────────────────────────────────────────────
            // OPTIMISTIC UI: Thêm message vào UI ngay lập tức (pending state)
            // ─────────────────────────────────────────────────────────────────
            string tempMessageId = Guid.NewGuid().ToString();
            _messageDates[tempMessageId] = DateTime.Now;
            _msgDelivery[tempMessageId] = SecureChat.DTOs.DeliveryStatus.Sent;
            _currentMsgs.Add((tempMessageId, finalMessageText, true, DateTime.Now.ToString("h:mm tt"), ""));
            BuildMessages();

            try
            {
                // ─────────────────────────────────────────────────────────────────
                // ENCRYPTION: Lấy conversation key và encrypt message
                // ─────────────────────────────────────────────────────────────────
                byte[]? conversationKey = await _decryptor.EnsureConversationKeyAsync(_activeConvId);

                if (conversationKey is null || conversationKey.Length != SecureChat.Shared.Security.AesEncryption.KeySize)
                {
                    throw new InvalidOperationException(
                        "Không thể lấy khóa mã hóa của cuộc trò chuyện. " +
                        "Vui lòng thử tải lại cuộc trò chuyện.");
                }

                // Validate conversation key
                var encryptionService = new MessageEncryptionService();
                if (!encryptionService.ValidateConversationKey(conversationKey))
                {
                    throw new InvalidOperationException(
                        "Khóa mã hóa không hợp lệ. Vui lòng thử tải lại cuộc trò chuyện.");
                }

                // Encrypt message content
                string encryptedContent;
                string contentIV;

                try
                {
                    (encryptedContent, contentIV) = encryptionService.EncryptMessage(finalMessageText, conversationKey);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Không thể mã hóa tin nhắn: {ex.Message}", ex);
                }

                // Validate encryption output
                if (string.IsNullOrWhiteSpace(encryptedContent) || string.IsNullOrWhiteSpace(contentIV))
                {
                    throw new InvalidOperationException(
                        "Mã hóa tin nhắn thất bại: kết quả rỗng.");
                }

                // ─────────────────────────────────────────────────────────────────
                // API CALL: Gửi encrypted message lên server
                // ─────────────────────────────────────────────────────────────────
                var sendRequest = new SendMessageRequest(
                    Type: MessageType.Text,
                    Content: encryptedContent,
                    ContentIV: contentIV,
                    ReplyToID: replyToId,
                    OriginalSenderID: null,
                    Attachments: null,
                    MentionedMemberIDs: null,
                    ExpiresAfterSeconds: _selfDestructSeconds
                );

                var (success, messageResponse, errorMessage) = await ApiClient.Instance.PostAsync<SendMessageRequest, MessageResponse>(
                    $"api/conversations/{_activeConvId}/messages",
                    sendRequest
                );

                if (!success || messageResponse is null)
                {
                    throw new InvalidOperationException(
                        $"Không thể gửi tin nhắn lên server: {errorMessage}");
                }

                // ─────────────────────────────────────────────────────────────────
                // TRACKING: Đánh dấu message đã xử lý để tránh duplicate
                // ─────────────────────────────────────────────────────────────────
                lock (_processedMessageIdsLock)
                {
                    _processedMessageIds.Add(messageResponse.MessageID);
                }

                // ─────────────────────────────────────────────────────────────────
                // SIGNALR BROADCAST: Phát tin nhắn qua SignalR để realtime
                // ─────────────────────────────────────────────────────────────────
                if (_signalRClient is not null && _signalRClient.IsConnected)
                {
                    try
                    {
                        await _signalRClient.SendMessageAsync(_activeConvId, messageResponse);
                    }
                    catch (Exception ex)
                    {
                        // SignalR broadcast thất bại không phải lỗi nghiêm trọng
                        // Message đã được lưu vào DB, người khác sẽ nhận khi sync
                        System.Diagnostics.Debug.WriteLine($"SignalR broadcast failed: {ex.Message}");
                    }
                }

                // ─────────────────────────────────────────────────────────────────
                // UI UPDATE: Cập nhật message với ID thật từ server
                // ─────────────────────────────────────────────────────────────────
                var index = _currentMsgs.FindIndex(m => m.Id == tempMessageId);
                if (index >= 0)
                {
                    if (_messageDates.Remove(tempMessageId))
                        _messageDates[messageResponse.MessageID] = messageResponse.SentAt.ToLocalTime();
                    // Chuyển delivery status từ tempId sang realId
                    _msgDelivery.TryRemove(tempMessageId, out _);
                    _msgDelivery[messageResponse.MessageID] = SecureChat.DTOs.DeliveryStatus.Sent;
                    string timeStr = messageResponse.SentAt.ToLocalTime().ToString("h:mm tt");
                    _currentMsgs[index] = (
                        messageResponse.MessageID,
                        finalMessageText,
                        true,
                        timeStr,
                        ""
                    );
                    BuildMessages();
                    RefreshConversationItem(_activeConvId, finalMessageText, true, "", timeStr);
                }

                // ─────────────────────────────────────────────────────────────────
                // EXPIRATION TRACKING: Track message nếu có ExpiresAt
                // ─────────────────────────────────────────────────────────────────
                if (messageResponse.ExpiresAt.HasValue)
                {
                    _expirationService.TrackMessage(messageResponse.MessageID, messageResponse.ExpiresAt.Value);
                }
            }
            catch (InvalidOperationException ex)
            {
                // ─────────────────────────────────────────────────────────────────
                // ERROR HANDLING: Xử lý lỗi và thông báo cho user
                // ─────────────────────────────────────────────────────────────────
                BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(this,
                        $"Không thể gửi tin nhắn:\n{ex.Message}",
                        "Lỗi gửi tin nhắn",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    // Xóa optimistic message khỏi UI
                    var index = _currentMsgs.FindIndex(m => m.Id == tempMessageId);
                    if (index >= 0)
                    {
                        _messageDates.Remove(tempMessageId);
                        _currentMsgs.RemoveAt(index);
                        BuildMessages();
                    }
                    RefreshSidebarPreview();

                    // Khôi phục text vào input box để user có thể gửi lại
                    _tbMessage.Text = text;
                }));
            }
            catch (Exception ex)
            {
                // ─────────────────────────────────────────────────────────────────
                // UNEXPECTED ERROR: Xử lý lỗi không mong đợi
                // ─────────────────────────────────────────────────────────────────
                BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(this,
                        $"Lỗi không mong đợi khi gửi tin nhắn:\n{ex.Message}\n\nVui lòng thử lại.",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);

                    // Xóa optimistic message khỏi UI
                    var index = _currentMsgs.FindIndex(m => m.Id == tempMessageId);
                    if (index >= 0)
                    {
                        _messageDates.Remove(tempMessageId);
                        _currentMsgs.RemoveAt(index);
                        BuildMessages();
                    }
                    RefreshSidebarPreview();

                    // Khôi phục text vào input box
                    _tbMessage.Text = text;
                }));

                // Log lỗi để debug
                System.Diagnostics.Debug.WriteLine($"SendMessage failed: {ex}");
            }
        }

        /// <summary>
        /// Event handler khi message hết hạn (self-destruct).
        /// Xóa message khỏi UI và untrack khỏi expiration service.
        /// </summary>
        private void OnMessageExpired(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                return;

            // Xóa message khỏi UI (thread-safe)
            BeginInvoke(new Action(() =>
            {
                _hiddenMessageIds.Add(messageId);
                _messageDates.Remove(messageId);
                _forwardMetadata.TryRemove(messageId, out _);
                _forwardOriginalSenderId.TryRemove(messageId, out _);
                if (_pinnedMessageIds.Remove(messageId))
                    UpdatePinnedBar();
                var index = _currentMsgs.FindIndex(m => m.Id == messageId);
                if (index >= 0)
                {
                    _currentMsgs.RemoveAt(index);
                    BuildMessages();
                    RefreshSidebarPreview();
                    System.Diagnostics.Debug.WriteLine($"Message {messageId} expired and removed from UI.");
                }
            }));

            // Untrack message (đã được xóa tự động trong service, nhưng gọi để chắc chắn)
            _expirationService.UntrackMessage(messageId);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _slideTimer?.Stop();
            _slideTimer?.Dispose();
            _wallpaperWatcher?.Dispose();
            _wallpaper?.Dispose();
            _chatMoreMenu?.Dispose();

            // Stop and dispose countdown refresh timer
            _countdownRefreshTimer?.Stop();
            _countdownRefreshTimer?.Dispose();

            // Stop and dispose expiration service
            _expirationService?.Stop();
            _expirationService?.Dispose();


            // Stop and dispose SignalR client
            if (_signalRClient is not null)
            {
                try
                {
                    _ = _signalRClient.StopAsync();
                    _ = _signalRClient.DisposeAsync();
                }
                catch { }
                _signalRClient = null;
            }

            SecureChat.Shared.Security.KeyManager.Clear();
            _decryptor.ForgetAll();
            _forwardMetadata.Clear();
            _forwardOriginalSenderId.Clear();
            _usernameToUserId.Clear();
            _senderDisplayNameMap.Clear();
            _allMsgs.Clear();
            _myMemberIdByConv.Clear();
            _syncedConversations.Clear();
            _hiddenMessageIds.Clear();
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