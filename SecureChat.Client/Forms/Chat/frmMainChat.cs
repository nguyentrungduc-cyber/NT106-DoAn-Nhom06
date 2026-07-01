using SecureChat.Client.Components.Chat;
using SecureChat.Client.Forms.Profile;
using SecureChat.Client.Forms.Settings;
using SecureChat.Client.Services;
using SecureChat.Client.Settings;
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
        private Panel _pnlSettingsMenuList; // Panel chứa các menu rows (để refresh theme)
        private bool _settingsVisible = false; // Trạng thái menu đang hiện hay ẩn, mặc định là ẩn
        private System.Windows.Forms.Timer _slideTimer; // Timer tạo hiệu ứng trượt (animation)
        private int _settingsTargetX;  // Tọa độ X đích khi animate menu

        // ── Sidebar controls ───────────────────────
        private Button _btnHamburger; // nút ☰ mở menu
        private Panel _pnlSearch = null!;
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
        private Panel _pnlSidebarHeader; // header trên cùng của sidebar trái (hamburger + "SecureChat")
        private Panel _sbHeader; // header của right sidebar (group/user info panel)
        private TelegramTextBox _tbMessage; // TextBox gõ tin nhắn
        private Button _btnAttach, _btnTimer, _btnEmoji, _btnMic;
        private TelegramButton _btnSend;
        private Label _lblChatName, _lblChatStatus; // tên và trạng thái người nhận
        private AvatarControl _chatAvatar; //  avatar tròn người nhận
        private Button _btnVideoCall; // video call button in header
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
        private readonly ConcurrentDictionary<string, string> _pinnedByMap = new(); // messageId → pinnedByName
        private readonly HashSet<string> _hiddenMessageIds = new();
        private bool _lastMessageSyncStarted = false;
        private readonly object _lastMessageSyncLock = new();

        // Typing indicator state
        private System.Windows.Forms.Timer? _typingDebounceTimer;
        private string? _typingDebounceConvId;
        private DateTime _lastTypingSent = DateTime.MinValue;
        private readonly Dictionary<string, HashSet<string>> _typingUsernames = new();
        private readonly object _typingLock = new();

        // Cached fonts to avoid GDI leaks from repeated new Font(...)
        private Font? _fontTimerActive;
        private Font? _fontTimerInactive;

        private bool _isClosing;

        // Sidebar header controls (updated after user data loads)
        private AvatarControl _settingsAvatar;
        private Label _lblSettingsUserName;

        // Sync tin nhắn từ MariaDB (server) -> Client
        private readonly SecureChat.Client.Services.Api.MessageService _messageService = new();
        private readonly SecureChat.Client.Services.MessageDecryptor _decryptor = new();
        private readonly ConcurrentDictionary<string, string> _myMemberIdByConv = new();
        private readonly HashSet<string> _syncedConversations = new();
        private const int MessageSyncPageSize = 50;


        // ── Settings menu controls ─────────────────
        private Panel _pnlSettingsHeader;

        // ── Conversation data ──────────────────────────────
        private string _activeConvId = string.Empty;
        private string _savedMessagesConvId = string.Empty;
        private string _currentUserId = string.Empty;
        private string _currentDisplayName = string.Empty;
        private string _currentUsername = string.Empty;
        private string _currentEmail = string.Empty;
        private bool _isLoggingOut;
        private bool _isSending;
        private string _currentAvatarUrl = string.Empty;

        private readonly List<(string Id, string Name, string Preview, string Time, int Unread, bool IsGroup)> _convs = new();

        private readonly Dictionary<string, Panel> _convRowCache = new();

        private readonly Dictionary<string, Image> _convAvatarCache = new();

        private readonly ConcurrentDictionary<string, string> _convOtherUserId = new();

        private readonly Dictionary<string, int> _convVersions = new();

        private readonly ConcurrentDictionary<string, (bool IsOnline, DateTime? LastSeenUtc)> _userPresence = new();

        // Shared presence cache accessible from other forms (contacts, group info, etc.)
        public static readonly ConcurrentDictionary<string, (bool IsOnline, DateTime? LastSeenUtc)> GlobalPresence = new();
        public static event Action<string, string, DateTime?>? GlobalUserStatusChanged;

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

        private readonly ConcurrentDictionary<string, List<(string Id, string Text, bool Out, string Time, string Sender)>> _allMsgs = new();
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
        private Panel? _nightModeToggle;
        private readonly ConcurrentDictionary<string, string> _senderDisplayNameMap = new();
        private readonly ConcurrentDictionary<string, string> _senderAvatarMap = new();
        private readonly ConcurrentDictionary<string, string> _usernameToUserId = new();
        private readonly ConcurrentDictionary<string, string> _forwardMetadata = new();
        private readonly ConcurrentDictionary<string, string> _forwardOriginalSenderId = new();
        private readonly HashSet<string> _pinnedMessageIds = new();
        private readonly Dictionary<string, DateTime?> _convMuteUntil = new(); // conversationId → muted until (null = not muted)
        private Icon? _originalIcon;
        private Icon? _monochromeIcon;
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
            this.Activated += (_, __) =>
            {
                NotificationManager.StopFlash(this.Handle);
                ApplyAdvancedSettings();
            };
            NightModeService.ThemeChanged += OnNightModeChanged;
            FormClosed += (_, __) => NightModeService.ThemeChanged -= OnNightModeChanged;
            SecureChat.Client.Services.AvatarService.CurrentUserChanged += OnExternalAvatarChanged;
            FormClosed += (_, __) => SecureChat.Client.Services.AvatarService.CurrentUserChanged -= OnExternalAvatarChanged;
        }

        private async void FrmMainChat_Load(object? sender, EventArgs e)
        {
            NightModeService.Initialize();
            _settingsToggles["Night Mode"] = NightModeService.IsEnabled;
            if (NightModeService.IsEnabled)
                OnNightModeChanged();
            UiLocalization.ApplyToForm(this);

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
                        _currentAvatarUrl = me.AvatarURL ?? string.Empty;
                        _decryptor.CurrentUserId = me.UserID;
                        _decryptor.CurrentUsername = me.Username;
                        SecureChat.Client.Services.AvatarService.SetCurrentUser(
                            me.UserID, me.DisplayName, me.Username, me.Email, me.AvatarURL);
                    }
                }
            }
            catch (Exception ex)
            {
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
            }
            // Ensure Saved Messages conversation exists
            try
            {
                var (savedOk, savedConv, _) = await _messageService.GetOrCreateSavedConversationAsync();
                if (savedOk && savedConv is not null)
                    _savedMessagesConvId = savedConv.ConversationID;
            }
            catch { /* best-effort */ }

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
                    SafeBeginInvoke(() => _pnlMessages?.Invalidate());
                }
            };
            _countdownRefreshTimer.Start();

            // Load danh sách hội thoại từ API thật
            await LoadConversationsAsync();

            // Trigger background sync for sidebar previews
            _ = SyncLastMessagePreviewsAsync();

            ApplyAdvancedSettings();
        }

        private async Task LoadConversationsAsync()
        {
            try
            {
                var (ok, list, _) = await _messageService.GetMyConversationsAsync();
                if (!ok || list is null) return;

                SafeBeginInvoke(() =>
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
                    _convOtherUserId.Clear();
                    foreach (var c in list)
                    {
                        bool isGroup = c.Type == SecureChat.Models.ConversationType.Group;
                        string time = c.LastActivityAt.HasValue
                            ? c.LastActivityAt.Value.ToLocalTime().ToString("HH:mm")
                            : c.CreatedAt.ToLocalTime().ToString("HH:mm");

                        // Giữ preview cũ nếu có; nếu không thì dùng preview từ server
                        string preview = existingPreviews.TryGetValue(c.ConversationID, out var oldPreview)
                            ? oldPreview
                            : string.Empty;

                        // Giữ thời gian cũ nếu không có LastActivityAt mới
                        if (c.LastActivityAt == null && existingTimes.TryGetValue(c.ConversationID, out var oldTime))
                            time = oldTime;

                        string convName = !string.IsNullOrWhiteSpace(c.Name) ? c.Name! : (isGroup ? "Group" : "Conversation");
                        _convs.Add((c.ConversationID, convName, preview, time, 0, isGroup));

                        if (!isGroup && !string.IsNullOrWhiteSpace(c.OtherUserId))
                            _convOtherUserId[c.ConversationID] = c.OtherUserId;
                    }

                    // Pin saved messages to the top
                    if (!string.IsNullOrWhiteSpace(_savedMessagesConvId))
                    {
                        int savedIdx = _convs.FindIndex(c => c.Id == _savedMessagesConvId);
                        if (savedIdx > 0)
                        {
                            var saved = _convs[savedIdx];
                            _convs.RemoveAt(savedIdx);
                            _convs.Insert(0, saved);
                        }
                    }

                    BuildConvList();
                    RefreshSidebarPreview();
                    RefreshAllSidebarPreviews();
                });
            }
            catch (Exception ex)
            {
                SafeBeginInvoke(() => MessageBox.Show(this, $"Cannot load conversation list: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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

                    // If the message was recalled, show recall placeholder
                    bool isRecalled = m.RecalledAt is not null;
                    string text = isRecalled ? "recalled::" : (m.Content ?? "");

                    // Skip decryption for recalled messages
                    if (!isRecalled && !string.IsNullOrEmpty(m.ContentIV) && m.ContentIV != "TBD")
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

                    string time = m.SentAt.ToLocalTime().ToString("HH:mm");
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
                    if (m.ExpiresAt.HasValue)
                    {
                        _expirationService.TrackMessage(m.MessageID, m.ExpiresAt.Value);
                    }
                    _messageDates[m.MessageID] = m.SentAt.ToLocalTime();
                    _allMsgs[convId].Add((m.MessageID, text, isOut, time, sender));
                    if (isOut)
                        _msgDelivery[m.MessageID] = m.Delivery;
                }

                SafeBeginInvoke(() => BuildMessages());
            }
            catch (Exception ex)
            {
                SafeBeginInvoke(() => MessageBox.Show(this, $"Cannot load messages: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
            var adv = SecureChat.Client.Settings.AdvancedSettings.Default;

            Text = "SecureChat";
            Size = new Size(1000, 660);
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterScreen;
            // this.MaximizeBox = false; // Vô hiệu hóa nút phóng to
            FormBorderStyle = adv.UseSystemWindowFrame ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle;
            ShowInTaskbar = adv.ShowTaskbarIcon;
            ApplyMonochromeIcon();

            BackColor = TG.SidebarBg;
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

        private void UpdateTitleBar()
        {
            var adv = SecureChat.Client.Settings.AdvancedSettings.Default;

            string title = "SecureChat";

            if (adv.ShowChatName && !string.IsNullOrWhiteSpace(_activeConvId))
            {
                var conv = _convs.Find(c => c.Id == _activeConvId);
                if (conv != default)
                {
                    bool isSaved = !string.IsNullOrWhiteSpace(_savedMessagesConvId) && _activeConvId == _savedMessagesConvId;
                    title = isSaved ? "SecureChat - Saved Messages" : $"SecureChat - {conv.Name}";
                }
            }

            if (adv.TotalUnreadCount)
            {
                int total = 0;
                foreach (var c in _convs)
                    total += c.Unread;
                if (total > 0)
                    title = $"{title} ({total})";
            }

            if (Text != title)
                Text = title;
        }

        private void ApplyMonochromeIcon()
        {
            var adv = SecureChat.Client.Settings.AdvancedSettings.Default;
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "app.ico");

            _originalIcon ??= this.Icon;

            if (_monochromeIcon == null && File.Exists(path))
            {
                try { _monochromeIcon = new Icon(path); }
                catch { }
            }

            bool useMono = adv.UseMonochromeIcon && _monochromeIcon != null;
            Icon target = useMono ? _monochromeIcon : _originalIcon;

            if (this.Icon != target)
                this.Icon = target;
        }

        private void ApplyAdvancedSettings()
        {
            var adv = SecureChat.Client.Settings.AdvancedSettings.Default;

            if (FormBorderStyle != (adv.UseSystemWindowFrame ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle))
            {
                FormBorderStyle = adv.UseSystemWindowFrame ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle;
            }

            if (ShowInTaskbar != adv.ShowTaskbarIcon)
                ShowInTaskbar = adv.ShowTaskbarIcon;

            ApplyMonochromeIcon();
            UpdateTitleBar();
        }

        private void AdjustLayout()
        {
            int sw = 300;                        // Sidebar Width
            int smw = 260;                       // Settings Menu Width
            int rsw = _isSidebarOpen ? 340 : 0;  // Right Sidebar Width

            _pnlSidebar.SetBounds(0, 0, sw, ClientSize.Height);
            _pnlChat.SetBounds(sw, 0, ClientSize.Width - sw - rsw, ClientSize.Height);

            if (_isSidebarOpen)
            {
                _pnlRightSidebar.SetBounds(
                    ClientSize.Width - 340, 0, 340, ClientSize.Height);
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
            _pnlSidebar = new Panel { BackColor = TG.SidebarBg };

            // ── Header xanh ──────────────────────────
            _pnlSidebarHeader = new Panel { Height = 52, BackColor = TG.TitleBarBg, Dock = DockStyle.Top };
            // Gắn chặt panel này vào mép trên cùng của sidebar.

            _btnHamburger = new Button
            {
                Text = "☰",
                FlatStyle = FlatStyle.Flat, // Đặt kiểu hiển thị phẳng, không có hiệu ứng nổi 3D của Windows cổ điển.
                Font = TG.FontRegular(14f),
                ForeColor = TG.TitleBarFg,
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
                ForeColor = TG.TitleBarFg,
                Size = new Size(40, 52),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            btnEdit.FlatAppearance.BorderSize = 0;

            // _pnlSidebarHeader.Controls.AddRange(new Control[] { _btnHamburger, lblTitle, btnEdit });
            _pnlSidebarHeader.Controls.AddRange(new Control[] { _btnHamburger, lblTitle });

            _pnlSidebarHeader.Resize += (s, e) =>
            {
                lblTitle.Width = _pnlSidebarHeader.Width - 96;
                btnEdit.Location = new Point(_pnlSidebarHeader.Width - 42, 0);
            };

            // ── Search ────────────────────────────────
            _pnlSearch = new Panel { Height = 44, Dock = DockStyle.Top, BackColor = TG.SidebarBg, Padding = new Padding(8, 6, 8, 4) };
            _tbSearch = new TelegramTextBox { Height = 32, Dock = DockStyle.Fill };
            _tbSearch.SetPlaceholder("🔍  Search");
            _tbSearch.TextChanged += (_, __) => BuildConvList();
            _pnlSearch.Controls.Add(_tbSearch);

            // ── Conversation list ─────────────────────
            _pnlConvList = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = TG.SidebarBg };

            _pnlEmptyState = new Panel { Dock = DockStyle.Fill, BackColor = TG.SidebarBg, Visible = false };
            _lblEmptyState = new Label
            {
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
                    MessageBox.Show(this, ex.Message, LocalizationService.Translate("New message"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            _pnlEmptyState.Controls.Add(_lblEmptyState);
            _pnlEmptyState.Controls.Add(_btnNewMessage);
            _pnlEmptyState.Resize += (_, __) => LayoutEmptyState();

            BuildConvList();

            _pnlSidebar.Controls.Add(_pnlEmptyState);
            _pnlSidebar.Controls.Add(_pnlConvList);
            _pnlSidebar.Controls.Add(_pnlSearch);
            _pnlSidebar.Controls.Add(_pnlSidebarHeader);

            UpdateEmptyStateUI();
        }

        private void BuildConvList()
        {
            _convRowCache.Clear();
            foreach (Control ctrl in _pnlConvList.Controls)
                ctrl.Dispose();
            _pnlConvList.Controls.Clear();

            string search = _tbSearch?.Text?.Trim() ?? string.Empty;
            bool hasSearch = !string.IsNullOrWhiteSpace(search);

            int y = 0;

            foreach (var c in _convs)
            {
                if (hasSearch && c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var row = BuildConvRow(c.Id, c.Name, c.Preview, c.Time, c.Unread, c.IsGroup);
                EnableDoubleBuffering(row);
                row.Location = new Point(0, y);
                row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                row.Width = _pnlConvList.ClientSize.Width;
                _pnlConvList.Controls.Add(row);
                y += 68;
            }

            UpdateEmptyStateUI();
        }

        private void UpdateConvRowHighlight(string convId, string? prevActiveConvId = null)
        {
            // Find the new active row and update its appearance; reset the previously active row
            Control? newActive = null, oldActive = null;
            string prevId = prevActiveConvId ?? _activeConvId;
            foreach (Control c in _pnlConvList.Controls)
            {
                if (c.Tag is string tag)
                {
                    if (tag == convId)
                        newActive = c;
                    else if (tag == prevId)
                        oldActive = c;
                }
                if (newActive != null && oldActive != null) break;
            }
            if (newActive != null)
                ApplyConvRowActiveStyle(newActive);
            if (oldActive != null && oldActive != newActive)
                ApplyConvRowInactiveStyle(oldActive);
        }

        private static void ApplyConvRowActiveStyle(Control pnl)
        {
            pnl.BackColor = TG.SidebarActive;
            foreach (Control c in pnl.Controls)
            {
                if (c is Label lbl)
                {
                    if (lbl.Name == "lblPreview")
                        lbl.ForeColor = Color.FromArgb(220, TG.TitleBarFg.R, TG.TitleBarFg.G, TG.TitleBarFg.B);
                    else if (lbl.Name == "lblTime")
                        lbl.ForeColor = Color.FromArgb(200, TG.TitleBarFg.R, TG.TitleBarFg.G, TG.TitleBarFg.B);
                    else
                        lbl.ForeColor = TG.TitleBarFg;
                }
            }
        }

        private static void ApplyConvRowInactiveStyle(Control pnl)
        {
            pnl.BackColor = TG.SidebarBg;
            foreach (Control c in pnl.Controls)
            {
                if (c is Label lbl)
                {
                    if (lbl.Name == "lblPreview")
                        lbl.ForeColor = TG.TextSecondary;
                    else if (lbl.Name == "lblTime")
                        lbl.ForeColor = TG.TextTime;
                    else
                        lbl.ForeColor = TG.TextName;
                }
            }
        }

        private void InsertConvRowAtTop(string id)
        {
            var idx = _convs.FindIndex(c => c.Id == id);
            if (idx < 0) return;
            var c = _convs[idx];
            var row = BuildConvRow(c.Id, c.Name, c.Preview, c.Time, c.Unread, c.IsGroup);
            EnableDoubleBuffering(row);
            row.Location = new Point(0, 0);
            row.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            row.Width = _pnlConvList.ClientSize.Width;
            _pnlConvList.Controls.Add(row);
            _pnlConvList.Controls.SetChildIndex(row, 0);
            // Shift existing rows down
            foreach (Control existing in _pnlConvList.Controls)
            {
                if (existing != row && existing.Top >= 0)
                    existing.Top += 68;
            }
            UpdateEmptyStateUI();
        }

        private void RemoveConvRow(string convId)
        {
            Control? target = null;
            foreach (Control c in _pnlConvList.Controls)
            {
                if (c.Tag is string tag && tag == convId)
                {
                    target = c;
                    break;
                }
            }
            if (target == null) return;
            int removedTop = target.Top;
            _pnlConvList.Controls.Remove(target);
            target.Dispose();
            // Shift remaining rows up
            foreach (Control c in _pnlConvList.Controls)
            {
                if (c.Top > removedTop)
                    c.Top -= 68;
            }
            UpdateEmptyStateUI();
        }

        private void UpdateConvRowName(string convId, string newName)
        {
            foreach (Control c in _pnlConvList.Controls)
            {
                if (c.Tag is string tag && tag == convId)
                {
                    foreach (Control child in c.Controls)
                    {
                        if (child is Label lbl && child.Name != "lblPreview" && child.Name != "lblTime")
                        {
                            lbl.Text = newName;
                            return;
                        }
                    }
                }
            }
        }

        private void UpdateEmptyStateUI()
        {
            string search = _tbSearch?.Text?.Trim() ?? string.Empty;
            bool hasSearch = !string.IsNullOrWhiteSpace(search);

            int visibleCount = 0;
            if (hasSearch)
            {
                foreach (var c in _convs)
                {
                    if (c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                        visibleCount++;
                }
            }

            bool hasConversations = _convs.Count > 0;
            bool hasVisible = hasConversations && (!hasSearch || visibleCount > 0);

            _pnlConvList.Visible = hasVisible;
            _pnlEmptyState.Visible = !hasVisible;

            if (!hasVisible)
            {
                if (hasSearch && hasConversations)
                    _lblEmptyState.Text = LocalizationService.Translate("No conversations match your search.");
                else
                    _lblEmptyState.Text = LocalizationService.Translate("You have no conversations yet.");
                LayoutEmptyState();
            }
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

            SafeBeginInvoke(() =>
            {
                // Thêm vào sidebar nếu chưa có
                if (!_convs.Any(c => c.Id == targetConvId))
                {
                    bool isGroup = conv.Type == ConversationType.Group;
                    string display = !string.IsNullOrWhiteSpace(conv.Name)
                        ? conv.Name!
                        : "Direct chat";
                    string time = conv.LastActivityAt?.ToLocalTime().ToString("HH:mm")
                        ?? string.Empty;
                    _convs.Insert(0, (conv.ConversationID, display, string.Empty, time, 0, isGroup));

                    if (!isGroup && !string.IsNullOrWhiteSpace(conv.OtherUserId))
                        _convOtherUserId[conv.ConversationID] = conv.OtherUserId;
                }

                _activeConvId = targetConvId;
                InsertConvRowAtTop(targetConvId);
                LoadConversation(targetConvId);
            });
        }

        private Panel BuildConvRow(string id, string name, string preview, string time, int unread, bool isGroup)
        {
            // Tạo một khung chứa có chiều cao cố định là 68px, màu nền trắng và đổi con trỏ chuột thành hình bàn tay (Cursors.Hand) khi rê vào.
            // Thuộc tính Tag được gán bằng id của cuộc trò chuyện để dễ dàng nhận diện.
            bool isSavedRow = !string.IsNullOrWhiteSpace(_savedMessagesConvId) && id == _savedMessagesConvId;
            var pnl = new Panel { Height = 68, BackColor = isSavedRow ? TG.WindowBg : TG.SidebarBg, Tag = id, Cursor = Cursors.Hand };

            // Kiểm tra xem dòng chat này có đang được người dùng chọn (active) hay không bằng cách so sánh Tag với biến toàn cục _activeConvId.
            bool isActive() => (string)pnl.Tag == _activeConvId;

            // Avatar
            var avatar = new AvatarControl { Size = new Size(48, 48), Location = new Point(10, 10) };
            EnableDoubleBuffering(avatar); // <--- QUAN TRỌNG: Control tự vẽ rất cần cái này
            if (isSavedRow && !string.IsNullOrWhiteSpace(_currentDisplayName))
                avatar.SetName(_currentDisplayName);
            else
                avatar.SetName(name);
            if (_convAvatarCache.TryGetValue(id, out var cachedImg) && cachedImg != null)
                avatar.Photo = new Bitmap(cachedImg);
            bool isUserOnline = !isGroup && !isSavedRow
                && _convOtherUserId.TryGetValue(id, out var otherId)
                && _userPresence.TryGetValue(otherId, out var pp)
                && pp.IsOnline;
            avatar.ShowOnline = isUserOnline;

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
            // Add controls
            pnl.Controls.AddRange(new Control[] { avatar, lblName, lblPreview, lblTime });

            // Click behavior
            // Định nghĩa hành động khi người dùng bấm vào dòng chat: Cập nhật ID đang kích hoạt, vẽ lại danh sách và tải nội dung tin nhắn của cuộc trò chuyện đó.
            Action doClick = () => { string prevId = _activeConvId; _activeConvId = id; UpdateConvRowHighlight(id, prevId); LoadConversation(id); };
            pnl.Click += (s, e) => doClick();
            avatar.Click += (s, e) => doClick();

            // Khi rê chuột vào/ra khỏi tấm nền, nếu dòng chat này không ở trạng thái được chọn (!isActive()), nền sẽ đổi sang màu hover (TG.SidebarHover) và ngược lại.
            pnl.MouseEnter += (s, e) => { if (!isActive()) pnl.BackColor = TG.SidebarHover; };
            pnl.MouseLeave += (s, e) => { if (!isActive()) pnl.BackColor = TG.SidebarBg; };

            // Resize child layout when row width changes
            pnl.Resize += (s, e) =>
            {
                lblName.Width = Math.Max(0, pnl.Width - 66 - 80);
                lblPreview.Width = Math.Max(0, pnl.Width - 66 - 12);
                lblTime.Location = new Point(pnl.Width - lblTime.Width - 12, 12);

                // Update colors for active state
                if (isActive())
                {
                    pnl.BackColor = TG.SidebarActive;
                    lblName.ForeColor = TG.TitleBarFg;
                    lblPreview.ForeColor = Color.FromArgb(220, TG.TitleBarFg.R, TG.TitleBarFg.G, TG.TitleBarFg.B);
                    lblTime.ForeColor = Color.FromArgb(200, TG.TitleBarFg.R, TG.TitleBarFg.G, TG.TitleBarFg.B);
                }
                else
                {
                    pnl.BackColor = TG.SidebarBg;
                    lblName.ForeColor = TG.TextName;
                    lblPreview.ForeColor = TG.TextSecondary;
                    lblTime.ForeColor = TG.TextTime;
                }
            };

            // Set width ngay lần đầu render
            int initWidth = _pnlConvList.ClientSize.Width > 0 ? _pnlConvList.ClientSize.Width : 280;
            lblTime.Location = new Point(Math.Max(0, initWidth - lblTime.Width - 12), 12);
            lblName.Width = Math.Max(0, initWidth - 66 - 80);
            lblPreview.Width = Math.Max(0, initWidth - 66 - 12);

            // Truyền sự kiện (Event Propagation)/hover for child controls
            // Trong WinForms, khi bạn click vào một Label nằm bên trong Panel, sự kiện Click của Panel sẽ không tự kích hoạt.
            // Vòng lặp này duyệt qua tất cả các control con để gán đè sự kiện Click và Hover. Mục đích là giúp người dùng click hay rê chuột vào bất cứ điểm nào trên dòng chat(dù là vào chữ hay vào khoảng trống) thì cả dòng chat vẫn phản hồi đồng bộ.
            foreach (Control c in pnl.Controls)
            {
                if (c == avatar) continue; // avatar wired already
                c.Click += (s, e) => doClick();
                c.MouseEnter += (s, e) => { if (!isActive()) pnl.BackColor = TG.SidebarHover; };
                c.MouseLeave += (s, e) => { if (!isActive()) pnl.BackColor = TG.SidebarBg; };
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
            _pnlChat = new Panel { BackColor = TG.SidebarBg };

            // ── Chat Header ───────────────────────────
            _pnlChatHeader = new Panel { Height = 52, BackColor = TG.SidebarBg, Dock = DockStyle.Top };
            // vẽ một đường kẻ ngang màu xám/nhạt (TG.Divider) ở dưới cùng để ngăn cách header với vùng tin nhắn.
            _pnlChatHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(TG.Divider);
                e.Graphics.DrawLine(pen, 0, 51, _pnlChatHeader.Width, 51);
            };

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
            _btnVideoCall = btnVideo;
            btnVideo.Click += async (s, e) =>
            {
                if (_isCallInitiating)
                    return;
                _isCallInitiating = true;
                try
                {
                    if (string.IsNullOrWhiteSpace(_activeConvId))
                    {
                        MessageBox.Show(LocalizationService.Translate("Please select a conversation first."), LocalizationService.Translate("Video Call"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (_signalRClient == null || !_signalRClient.IsConnected)
                    {
                            MessageBox.Show(LocalizationService.Translate("SignalR is not connected. Please wait..."), LocalizationService.Translate("Video Call"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var createPayload = new { type = 1 };
                    var json = System.Text.Json.JsonSerializer.Serialize(createPayload);
                    var http = ApiClient.Instance.GetHttpClient();
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var response = await http.PostAsync($"api/conversations/{_activeConvId}/calls", content);
                    var responseStr = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Cannot start call: {responseStr}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var callData = System.Text.Json.JsonSerializer.Deserialize<CallResponse>(responseStr, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
                    if (callData == null)
                    {
                        MessageBox.Show(LocalizationService.Translate("Invalid server response."), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    await _signalRClient.NotifyCallIncomingAsync(_activeConvId, callData.CallID, _currentDisplayName, SecureChat.Models.CallType.Video);

                    bool isGroupCall = _convs.Find(c => c.Id == _activeConvId).IsGroup;
                    var callForm = new Forms.Call.frmVideoCall(_lblChatName.Text, callData.CallID, _activeConvId, _signalRClient, isGroupCall);
                    callForm.FormClosed += (_, __) => this.Activate();
                    callForm.FormClosed += (_, __) => _isCallInitiating = false;
                    callForm.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot start call: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    _isCallInitiating = false;
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
                const int buttonArea = 168;
                const int safetyPad = 30;
                int labelWidth = _pnlChatHeader.Width - 56 - buttonArea - safetyPad;
                _lblChatName.Width = Math.Max(0, labelWidth);
                _lblChatStatus.Width = Math.Max(0, labelWidth);

                int w = _pnlChatHeader.Width;
                btnMore.Location = new Point(w - 42, 8);
                _btnToggleSidebar.Location = new Point(w - 84, 8);
                btnVideo.Location = new Point(w - 126, 8);
                btnSearch.Location = new Point(w - 168, 8);
            };


            // ── Messages area  - Vùng hiển thị tin nhắn ─────────────────────────
            _pnlMessages = new ChatPanel
            {
                Dock = DockStyle.Fill, // chiếm trọn phần diện tích còn lại
                AutoScroll = true,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = TG.ChatBg,
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
                BackColor = Color.FromArgb(220, TG.WindowBg.R, TG.WindowBg.G, TG.WindowBg.B),
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
                Size = new Size(28, 28),   // khớp chiều cao panel, không bị clip
                Cursor = Cursors.Hand,
                ForeColor = TG.TextSecondary,
                Font = new Font("Segoe UI Symbol", 9f), // font nhỏ để ký tự vừa trong 28px
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _btnUnpin.FlatAppearance.BorderSize = 0;
            _btnUnpin.Click += (s, e) => { _isPinnedPopupOpen = false; _pnlPinnedPopup.Visible = false; OnUnpinMessage(); };
            _pnlPinnedBar = new Panel
            {
                Height = 28,
                Dock = DockStyle.Top,
                BackColor = TG.Divider,
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
                // Layout từ phải sang trái: [✕ 28px] [▾ 24px] [text phần còn lại]
                const int btnW   = 28;
                const int arrowW = 24;
                const int pad    = 4;

                _btnUnpin.Size     = new Size(btnW, _pnlPinnedBar.Height); // full height = không bị clip
                _btnUnpin.Location = new Point(_pnlPinnedBar.Width - pad - btnW, 0);

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
                BackColor = TG.SidebarBg,
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
                BackColor = TG.Divider,
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
            _pnlRightSidebar = new Panel { Width = 300, BackColor = TG.SidebarBg, Visible = false };

            _sbHeader = new Panel { Height = 52, Dock = DockStyle.Top, BackColor = TG.SidebarBg };
            _sbHeader.Paint += (_, e) =>
            {
                using var pen = new Pen(TG.Divider);
                e.Graphics.DrawLine(pen, 0, 51, _sbHeader.Width, 51);
            };

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
                Location = new Point(292, 8)
            };
            sbClose.FlatAppearance.BorderSize = 0;
            sbClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(15, 0, 0, 0);
            sbClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(30, 0, 0, 0);
            sbClose.Click += (_, _) => { _isSidebarOpen = false; _btnToggleSidebar.Text = "⏪"; AdjustLayout(); };

            _sbHeader.Controls.Add(new Label
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
            _sbHeader.Controls.Add(sbClose);

            _sbBody = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = TG.SidebarBg };

            _pnlRightSidebar.Controls.Add(_sbBody);
            _pnlRightSidebar.Controls.Add(_sbHeader);

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
                    BackColor = TG.SidebarBg,
                    ForeColor = TG.TextPrimary,
                    Font = new Font("Segoe UI", 10f),
                    Renderer = new ToolStripProfessionalRenderer(new ChatMenuColorTable())
                };

                _mnuMuteNotifications = CreateChatMenuItem($"🔕  {LocalizationService.Translate("Mute Notifications")}", (_, __) => ToggleMuteNotificationsQuick());

                _mnuUnmuteNow = CreateChatMenuItem($"🔊  {LocalizationService.Translate("Unmute Now")}", (_, __) => UnmuteNow());
                _mnuDisableSound = CreateChatMenuItem($"🔇  {LocalizationService.Translate("Disable Sound")}", (_, __) => ToggleDisableSound());
                _mnuMuteForever = CreateChatMenuItem($"⛔  {LocalizationService.Translate("Mute Forever")}", (_, __) => SetMuteForever());
                _mnuMuteFor = CreateChatMenuItem($"⏳  {LocalizationService.Translate("Mute for...")}", null);

                _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem(LocalizationService.Translate("30 minutes"), (_, __) => SetMuteFor(TimeSpan.FromMinutes(30))));
                _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem(LocalizationService.Translate("1 hour"), (_, __) => SetMuteFor(TimeSpan.FromHours(1))));
                _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem(LocalizationService.Translate("8 hours"), (_, __) => SetMuteFor(TimeSpan.FromHours(8))));
                _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem(LocalizationService.Translate("1 day"), (_, __) => SetMuteFor(TimeSpan.FromDays(1))));
                _mnuMuteFor.DropDownItems.Add(CreateChatSubMenuItem(LocalizationService.Translate("1 week"), (_, __) => SetMuteFor(TimeSpan.FromDays(7))));

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
            bool isSavedConv = !string.IsNullOrWhiteSpace(_savedMessagesConvId) && _activeConvId == _savedMessagesConvId;

            _chatMoreMenu.Items.Add(_mnuMuteNotifications);

            if (isGroup)
            {
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"ℹ️  {LocalizationService.Translate("View group info")}", (_, __) => OpenGroupInfo()));
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"🎛️  {LocalizationService.Translate("Manage group")}", (_, __) => OpenEditGroupFromChat()));
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"🧹  {LocalizationService.Translate("Clear History")}", (_, __) => ClearHistory()));
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"🚪  {LocalizationService.Translate("Delete and leave")}", (_, __) => DeleteAndLeave(), Color.FromArgb(0xE2, 0x4B, 0x4A)));
            }
            else if (isSavedConv)
            {
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"🧹  {LocalizationService.Translate("Clear Saved Messages History")}", (_, __) => ClearSavedMessagesHistory()));
            }
            else
            {
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"👤  {LocalizationService.Translate("View Profile")}", (_, __) => ViewProfile()));
                _chatMoreMenu.Items.Add(new ToolStripSeparator());
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"🗑  {LocalizationService.Translate("Clear History")}", (_, __) => ClearHistoryPrivate()));
                _chatMoreMenu.Items.Add(CreateChatMenuItem($"🗑  {LocalizationService.Translate("Delete Chat")}", (_, __) => DeleteChat(), Color.FromArgb(0xE2, 0x4B, 0x4A)));
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
                _notificationsSoundEnabled ? LocalizationService.Translate("Notification sound enabled for this chat.") : LocalizationService.Translate("Notification sound disabled for this chat."),
                LocalizationService.Translate("Notifications"),
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
                LocalizationService.Translate("Notifications are muted forever for this chat."),
                LocalizationService.Translate("Notifications"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void SetMuteFor(TimeSpan duration)
        {
            _notificationsMuted = true;
            _muteUntilUtc = DateTime.UtcNow.Add(duration);
            RefreshMuteMenuState();
            MessageBox.Show(this,
                string.Format(LocalizationService.Translate("Notifications are muted for {0}."), FormatDuration(duration)),
                LocalizationService.Translate("Notifications"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 60) return string.Format(LocalizationService.Translate("{0} minutes"), (int)duration.TotalMinutes);
            if (duration.TotalHours < 24) return string.Format(LocalizationService.Translate("{0} hours"), (int)duration.TotalHours);
            return string.Format(LocalizationService.Translate("{0} days"), (int)duration.TotalDays);
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

                _mnuMuteNotifications.Text = $"🔊  {LocalizationService.Translate("Unmute Now")}";
            }
            else
            {
                if (_mnuMuteNotifications.DropDownItems.Count == 0)
                {
                    foreach (var item in _muteOptionItems)
                        _mnuMuteNotifications.DropDownItems.Add(item);
                }

                _mnuMuteNotifications.Text = _notificationsMuted
                    ? $"🔕  {string.Format(LocalizationService.Translate("Muted until {0}"), _muteUntilUtc.Value.ToLocalTime().ToString("HH:mm"))}"
                    : $"🔔  {LocalizationService.Translate("Mute Notifications")}";
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
            Image? avatarImage = null;
            string? convDescription = null;
            try
            {
                var (ok, view, _) = await SecureChat.Client.Services.ApiClient.Instance
                    .GetAsync<SecureChat.DTOs.ConversationViewResponse>($"api/conversations/{_activeConvId}/view");
                if (!ok || view?.Metadata == null) return;

                var meta = view.Metadata;
                convDescription = meta.Description;

                if (!string.IsNullOrWhiteSpace(meta.AvatarURL))
                {
                    try
                    {
                        var imgRes = await http.GetAsync(meta.AvatarURL);
                        if (imgRes.IsSuccessStatusCode)
                        {
                            using var imgStream = await imgRes.Content.ReadAsStreamAsync();
                            avatarImage = new Bitmap(imgStream);
                        }
                    }
                    catch { }
                }

                members = view.Members.Select(m => new SecureChat.Client.Forms.Chat.MemberModel(
                    m.User?.DisplayName ?? m.Nickname ?? "Unknown",
                    m.User != null && m.User.ShowOnlineStatus
                        ? SecureChat.Client.Helpers.PresenceFormatter.GetPresenceText(m.IsOnline, m.User.LastSeenUtc)
                        : "offline",
                    m.Role switch
                    {
                        SecureChat.Models.MemberRole.Owner => "Admin",
                        SecureChat.Models.MemberRole.Moderator => "Moderator",
                        _ => "Member"
                    },
                    null,
                    TG.GetAvatarColor(m.User?.DisplayName ?? "?"),
                    m.MemberID
                )).ToList();
            }
            catch { }

            using var dlg = new SecureChat.Client.Forms.Chat.frmGroupInfo();
            dlg.LoadGroup(_lblChatName.Text, convDescription, avatarImage, members);
            dlg.SetContext(_activeConvId, _currentDisplayName);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.AddMemberRequested += () =>
            {
                using var settingsDlg = new SecureChat.Client.Forms.Chat.frmMembersSettings(_activeConvId);
                settingsDlg.StartPosition = FormStartPosition.CenterParent;
                settingsDlg.ShowDialog(dlg);
            };
            dlg.ShowDialog(this);
            avatarImage?.Dispose();
        }


        private async void OpenEditGroupFromChat()
        {
            using var dlg = new SecureChat.Client.Forms.Chat.frmEditGroup(_activeConvId, _lblChatName.Text);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();

            // Upload avatar mới nếu có
            string? avatarUrl = null;
            if (dlg.NewAvatarPath != null)
            {
                try
                {
                    using var fs = new FileStream(dlg.NewAvatarPath, FileMode.Open, FileAccess.Read);
                    using var ms = new MemoryStream();
                    await fs.CopyToAsync(ms);
                    ms.Position = 0;
                    using var formContent = new MultipartFormDataContent();
                    formContent.Add(new ByteArrayContent(ms.ToArray()), "File", "avatar.jpg");
                    var uploadRes = await http.PostAsync("api/files/upload", formContent);
                    if (uploadRes.IsSuccessStatusCode)
                    {
                        var uploadJson = await uploadRes.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(uploadJson);
                        avatarUrl = doc.RootElement.GetProperty("url").GetString();
                    }
                }
                catch { }
            }

            var updatePayload = new
            {
                Name = dlg.GroupName,
                AvatarUrl = avatarUrl,
                Description = dlg.DescriptionText,
                ChatHistoryMode = dlg.ChatHistoryMode == "Visible" ? (int)SecureChat.Models.HistoryMode.Visible : (int)SecureChat.Models.HistoryMode.Hidden
            };
            var updateJson = System.Text.Json.JsonSerializer.Serialize(updatePayload);
            var updateRes = await http.PatchAsync(
                $"api/conversations/{_activeConvId}",
                new StringContent(updateJson, System.Text.Encoding.UTF8, "application/json"));

            if (!updateRes.IsSuccessStatusCode)
            {
                var errBody = await updateRes.Content.ReadAsStringAsync();
                MessageBox.Show(this, string.Format(LocalizationService.Translate("Update failed: {0}"), errBody), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _lblChatName.Text = dlg.GroupName;
            var idx = _convs.FindIndex(c => c.Id == _activeConvId);
            if (idx >= 0)
            {
                var old = _convs[idx];
                _convs[idx] = (old.Id, dlg.GroupName, old.Preview, old.Time, old.Unread, old.IsGroup);
            }
            UpdateConvRowName(_activeConvId, dlg.GroupName);

            // Cập nhật avatar trong header và sidebar
            if (avatarUrl != null)
            {
                try
                {
                    var imgRes = await http.GetAsync(avatarUrl);
                    if (imgRes.IsSuccessStatusCode)
                    {
                    using var imgStream = await imgRes.Content.ReadAsStreamAsync();
                    var img = new Bitmap(imgStream);
                    var oldPhoto = _chatAvatar.Photo;
                    if (oldPhoto != null)
                    {
                        _chatAvatar.Photo = null;
                        oldPhoto.Dispose();
                    }
                    _chatAvatar.Photo = img;
                    _chatAvatar.Invalidate();

                    // Lưu vào cache để sidebar hiển thị
                    if (_convAvatarCache.TryGetValue(_activeConvId, out var oldImg))
                        oldImg?.Dispose();
                    _convAvatarCache[_activeConvId] = new Bitmap(img);
                    }
                }
                catch { }
            }
            await SyncConversationsAsync();
        }

        private async void ClearHistory()
        {
            var targetConvId = _activeConvId;
            using var dlg = new SecureChat.Client.Forms.Chat.frmClearHistory(_lblChatName.Text);
            if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.DeleteConfirmed)
                return;

            if (!await TryRemoveConversationOnServerAsync(targetConvId))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                MessageBox.Show(this,
                    resources.GetString("ChatDeleteFailed") ?? "Unable to delete the conversation right now.",
                    resources.GetString("ChatDeleteTitle") ?? "Clear history",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RemoveConversationLocal(targetConvId);
        }

        private async void DeleteAndLeave()
        {
            var currentConv = _convs.Find(c => c.Id == _activeConvId);
            bool isGroup = currentConv.IsGroup;
            string targetConvId = _activeConvId;

            string? newOwnerMemberId = null;

            if (isGroup)
            {
                // Kiểm tra role của mình trong group
                var (memOk, myMembership, _) = await _messageService.GetMyMembershipAsync(_activeConvId);

                if (memOk && myMembership != null && myMembership.Role == SecureChat.Models.MemberRole.Owner)
                {
                    // Là Owner → phải appoint admin mới
                    var memberNames = new List<string>();
                    var memberIds = new List<string>();
                    var (memListOk, memList, memListErr) = await _messageService.GetMembersAsync(_activeConvId);
                    if (memListOk && memList != null)
                    {
                        var others = memList.Where(m => m.UserID != _currentUserId).ToList();
                        memberNames = others.Select(m => m.User?.DisplayName ?? m.Nickname ?? "Unknown").ToList();
                        memberIds = others.Select(m => m.MemberID).ToList();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[DeleteAndLeave] Failed to fetch members: {memListErr}");
                    }

                    if (memberNames.Count == 0)
                    {
                        var result = MessageBox.Show(this,
                            "You are the only member in this group. Leaving will permanently delete the group for everyone.\n\nContinue?",
                            "Leave group",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (result != DialogResult.Yes)
                            return;

                        // No admin needed — call delete (server will auto-delete when last member leaves)
                        if (!await TryRemoveConversationOnServerAsync(targetConvId, null))
                        {
                            MessageBox.Show(this, "Unable to leave the group right now.", "Leave group",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        RemoveConversationLocal(targetConvId);
                        return;
                    }

                    var defaultNextOwner = memberNames[0];
                    using var dlg = new frmLeaveGroup(_lblChatName.Text, defaultNextOwner, memberNames.ToArray(), memberIds.ToArray());
                    if (dlg.ShowDialog(this) != DialogResult.OK || !dlg.LeaveConfirmed)
                        return;
                    newOwnerMemberId = dlg.AppointedAdminMemberId;
                }
                else
                {
                    // Not Owner → just confirm leaving group
                    string convName = string.IsNullOrWhiteSpace(currentConv.Name) ? LocalizationService.Translate("this") : currentConv.Name;
                    var result = MessageBox.Show(this,
                        string.Format(LocalizationService.Translate("Are you sure you want to leave the group {0}?"), convName),
                        LocalizationService.Translate("Leave Group"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                        return;
                }
            }
            else
            {
                string convName = string.IsNullOrWhiteSpace(currentConv.Name) ? LocalizationService.Translate("this user") : currentConv.Name;
                var result = MessageBox.Show(this,
                    string.Format(LocalizationService.Translate("Are you sure you want to delete the conversation with {0}?\n\nAll messages will be permanently deleted."), convName),
                    LocalizationService.Translate("Delete Conversation"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;
            }

            if (!await TryRemoveConversationOnServerAsync(targetConvId, newOwnerMemberId))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                string errorTitle = isGroup
                    ? (resources.GetString("ChatLeaveTitle") ?? LocalizationService.Translate("Leave group"))
                    : (resources.GetString("ChatDeleteTitle") ?? LocalizationService.Translate("Delete conversation"));
                string errorMessage = isGroup
                    ? (resources.GetString("ChatLeaveFailed") ?? LocalizationService.Translate("Unable to leave the group right now."))
                    : LocalizationService.Translate("Cannot delete conversation. Please try again.");

                MessageBox.Show(this,
                    errorMessage,
                    errorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            RemoveConversationLocal(targetConvId);
        }

        private async Task<bool> TryRemoveConversationOnServerAsync(string conversationId, string? newOwnerMemberId = null)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return false;

            // Nếu đang chuyển quyền Owner (rời nhóm), bỏ qua DELETE → chỉ LEAVE
            if (!string.IsNullOrWhiteSpace(newOwnerMemberId))
            {
                var leavePayload = new SecureChat.DTOs.LeaveConversationRequest { NewOwnerMemberId = newOwnerMemberId };
                var (leftOk, _, leftErr) = await ApiClient.Instance.PostAsync<SecureChat.DTOs.LeaveConversationRequest, object>(
                    $"api/conversations/{conversationId}/leave", leavePayload);
                return leftOk;
            }

            var (deletedOk, deletedErr) = await ApiClient.Instance.DeleteAsync($"api/conversations/{conversationId}");
            if (deletedOk)
                return true;

            var leavePayload2 = new SecureChat.DTOs.LeaveConversationRequest { NewOwnerMemberId = newOwnerMemberId };
            var (leftOk2, _, leftErr2) = await ApiClient.Instance.PostAsync<SecureChat.DTOs.LeaveConversationRequest, object>(
                $"api/conversations/{conversationId}/leave", leavePayload2);

            if (leftOk2)
                return true;

            if (!string.IsNullOrWhiteSpace(leftErr2))
            {
                var resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMainChat));
                MessageBox.Show(this,
                    leftErr2,
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

        private async Task RefreshAvatarForConversationAsync(string convId, string? avatarUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(avatarUrl))
                {
                    // Avatar removed — clear cache and revert to initials
                    SafeBeginInvoke(() =>
                    {
                        if (_convAvatarCache.TryGetValue(convId, out var oldCached))
                        {
                            oldCached?.Dispose();
                            _convAvatarCache.Remove(convId);
                        }
                        if (_activeConvId == convId)
                        {
                            var old = _chatAvatar.Photo;
                            if (old != null)
                            {
                                _chatAvatar.Photo = null;
                                old.Dispose();
                            }
                            var conv = _convs.Find(c => c.Id == convId);
                            if (conv != default)
                                _chatAvatar.SetName(conv.Name);
                            _chatAvatar.Invalidate();
                        }
                        if (_convRowCache.TryGetValue(convId, out var row))
                        {
                            foreach (Control c in row.Controls)
                            {
                                if (c is AvatarControl av)
                                {
                                    var oldAv = av.Photo;
                                    if (oldAv != null)
                                    {
                                        av.Photo = null;
                                        oldAv.Dispose();
                                    }
                                    var conv = _convs.Find(c => c.Id == convId);
                                    if (conv != default)
                                        av.SetName(conv.Name);
                                    av.Invalidate();
                                    break;
                                }
                            }
                        }
                    });
                    return;
                }

                var http = ApiClient.Instance.GetHttpClient();
                var imgRes = await http.GetAsync(avatarUrl);
                if (imgRes.IsSuccessStatusCode)
                {
                    using var imgStream = await imgRes.Content.ReadAsStreamAsync();
                    var img = new Bitmap(imgStream);

                    // Update cache for sidebar (thread-safe ConcurrentDictionary)
                    if (_convAvatarCache.TryGetValue(convId, out var oldCached))
                        oldCached?.Dispose();
                    _convAvatarCache[convId] = new Bitmap(img);

                    // Update UI elements on UI thread
                    if (_activeConvId == convId || _convRowCache.ContainsKey(convId))
                    {
                        SafeBeginInvoke(() =>
                        {
                            if (IsDisposed) return;
                            if (_activeConvId == convId)
                            {
                                var old = _chatAvatar.Photo;
                                if (old != null)
                                {
                                    _chatAvatar.Photo = null;
                                    old.Dispose();
                                }
                                _chatAvatar.Photo = new Bitmap(img);
                                _chatAvatar.Invalidate();
                            }
                            if (_convRowCache.TryGetValue(convId, out var row))
                            {
                                foreach (Control c in row.Controls)
                                {
                                    if (c is AvatarControl av)
                                    {
                                        var oldAv = av.Photo;
                                        if (oldAv != null)
                                        {
                                            av.Photo = null;
                                            oldAv.Dispose();
                                        }
                                        av.Photo = new Bitmap(img);
                                        av.Invalidate();
                                        break;
                                    }
                                }
                            }
                        });
                    }
                }
            }
            catch { }
        }

        private void RemoveConversationLocal(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return;

            // Xóa conversation khỏi danh sách
            _convs.RemoveAll(c => c.Id == conversationId);

            // Xóa tất cả tin nhắn của conversation
            _allMsgs.TryRemove(conversationId, out _);

            // Xóa khỏi cache sync
            _syncedConversations.Remove(conversationId);
            _myMemberIdByConv.TryRemove(conversationId, out _);

            // Xóa avatar cache
            if (_convAvatarCache.TryGetValue(conversationId, out var oldAvatar))
            {
                oldAvatar?.Dispose();
                _convAvatarCache.Remove(conversationId);
            }

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
                _pinnedByMap.Clear();
                UpdatePinnedBar();
                
                // Chọn conversation đầu tiên không phải Saved Messages
                if (_convs.Count > 0)
                {
                    var first = _convs.FirstOrDefault(c => !string.IsNullOrWhiteSpace(_savedMessagesConvId) && c.Id != _savedMessagesConvId);
                    _activeConvId = first.Id ?? _convs[0].Id;
                }
            }

            // Rebuild UI
            RemoveConvRow(conversationId);
            if (!string.IsNullOrWhiteSpace(_activeConvId))
                UpdateConvRowHighlight(_activeConvId);

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
                    MessageBox.Show(this, LocalizationService.Translate("Unable to load profile information."), LocalizationService.Translate("View Profile"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    MessageBox.Show(this, LocalizationService.Translate("Unable to find the other user in this conversation."), LocalizationService.Translate("View Profile"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var other = members.FirstOrDefault(m => m.UserID != _currentUserId) ?? members[0];
                if (other.User == null)
                {
                    MessageBox.Show(this, LocalizationService.Translate("User information is not available."), LocalizationService.Translate("View Profile"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string otherUserId = other.UserID ?? other.User.UserID;
                bool isOnline = other.IsOnline;
                DateTime? lastSeen = other.User.LastSeenUtc;
                if (_userPresence.TryGetValue(otherUserId, out var pData))
                {
                    isOnline = isOnline || pData.IsOnline;
                    lastSeen = lastSeen ?? pData.LastSeenUtc;
                }

                using var dlg = new frmUserProfile(
                    other.User.DisplayName ?? "Unknown",
                    other.User.Username ?? "unknown",
                    other.User.Email,
                    other.User.BioText,
                    isOnline,
                    lastSeen,
                    other.User.ShowOnlineStatus
                );
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading profile: {ex.Message}", LocalizationService.Translate("View Profile"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ClearHistoryPrivate()
        {
            var conv = _convs.Find(c => c.Id == _activeConvId);
            string otherName = conv.Name ?? LocalizationService.Translate("the other user");

            var result = MessageBox.Show(this,
                LocalizationService.Translate("Clear chat history?"),
                LocalizationService.Translate("Clear History"),
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            bool alsoForOther = MessageBox.Show(this,
                string.Format(LocalizationService.Translate("Also clear for {0}?"), otherName),
                LocalizationService.Translate("Clear History"),
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
                            string.Format(LocalizationService.Translate("Failed to clear chat on server: {0}"), clearErr),
                            LocalizationService.Translate("Clear History"),
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
                _pinnedByMap.Clear();
                UpdatePinnedBar();

                // Refresh sidebar: clear preview text and timestamp.
                // RefreshConversationItem updates both the _convs tuple
                // and the visible Label controls in the cached row.
                RefreshConversationItem(_activeConvId, string.Empty, true, string.Empty, string.Empty);

                BuildMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error clearing history: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ClearSavedMessagesHistory()
        {
            var result = MessageBox.Show(this,
                LocalizationService.Translate("This will permanently delete all notes, messages, links, files and media stored in Saved Messages."),
                LocalizationService.Translate("Clear Saved Messages History"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.OK)
                return;

            try
            {
                var (clearOk, _, clearErr) = await ApiClient.Instance.PostAsync<object, object>(
                    $"api/conversations/{_activeConvId}/clear", new { });
                if (!clearOk)
                {
                    MessageBox.Show(this,
                        string.Format(LocalizationService.Translate("Failed to clear Saved Messages: {0}"), clearErr),
                        LocalizationService.Translate("Clear Saved Messages History"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                foreach (var msg in _currentMsgs)
                {
                    _messageDates.Remove(msg.Id);
                    _forwardMetadata.TryRemove(msg.Id, out _);
                    _forwardOriginalSenderId.TryRemove(msg.Id, out _);
                }
                _currentMsgs.Clear();

                _pinnedMessageIds.Clear();
                _pinnedByMap.Clear();
                UpdatePinnedBar();

                RefreshConversationItem(_activeConvId, string.Empty, true, string.Empty, string.Empty);

                BuildMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error clearing Saved Messages: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void DeleteChat()
        {
            var targetConvId = _activeConvId;
            var conv = _convs.Find(c => c.Id == targetConvId);
            string otherName = conv.Name ?? LocalizationService.Translate("the other user");

            var result = MessageBox.Show(this,
                LocalizationService.Translate("Delete this chat?"),
                LocalizationService.Translate("Delete Chat"),
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            bool alsoForOther = MessageBox.Show(this,
                string.Format(LocalizationService.Translate("Also delete for {0}?"), otherName),
                LocalizationService.Translate("Delete Chat"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

            bool serverOk;
            if (alsoForOther)
            {
                var (deletedOk, deletedErr) = await ApiClient.Instance.DeleteAsync(
                    $"api/conversations/{targetConvId}");
                serverOk = deletedOk;
                if (!serverOk)
                {
                    MessageBox.Show(this,
                        string.Format(LocalizationService.Translate("Unable to delete the conversation: {0}"), deletedErr),
                        LocalizationService.Translate("Delete Chat"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                var (leftOk, _, leftErr) = await ApiClient.Instance.PostAsync<object, object>(
                    $"api/conversations/{targetConvId}/leave", new { });
                serverOk = leftOk;
                if (!serverOk)
                {
                    MessageBox.Show(this,
                        string.Format(LocalizationService.Translate("Unable to leave the conversation: {0}"), leftErr),
                        LocalizationService.Translate("Delete Chat"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            RemoveConversationLocal(targetConvId);
        }

        // Cache ảnh nền để không load lại mỗi lần paint
        private Image _wallpaper = null;
        private bool _wallpaperLoaded = false;


        private Image LoadWallpaper()
        {
            if (_wallpaperLoaded) return _wallpaper;
            _wallpaperLoaded = true;

            string imagesDir = Path.Combine(Application.StartupPath, "Resources", "Images");
            var candidates = new List<string>();
            if (NightModeService.IsEnabled)
                candidates.Add(Path.Combine(imagesDir, "chat_bg_nm.jpg"));
            candidates.AddRange(new[] {
            Path.Combine(imagesDir, "chat_bg.jpg"),
            Path.Combine(imagesDir, "chat_bg.png"),
            Path.Combine(imagesDir, "wallpaper.jpg"),
            Path.Combine(imagesDir, "wallpaper.png"),
            Path.Combine(imagesDir, "background.jpg"),
            Path.Combine(imagesDir, "background.png"),
    });

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
                SafeBeginInvoke(ReloadWallpaper);
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
            g.Clear(TG.ChatBg);
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
            _pnlInputBar = new Panel { Height = 56, Dock = DockStyle.Bottom, BackColor = TG.SidebarBg };
            _pnlInputBar.Paint += (s, e) =>
            {
                using var pen = new Pen(TG.Divider);
                e.Graphics.DrawLine(pen, 0, 0, _pnlInputBar.Width, 0);
            };

            // =========================================================
            // 1. TẠO PANEL REPLY (Bao gồm Icon, Đường viền, Tên, Text)
            // =========================================================
            _pnlReplyContext = new Panel { Dock = DockStyle.Top, Height = 44, Visible = false, BackColor = TG.SidebarBg };

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
            var btnAttach = MakeInputBtn("📎"); _btnAttach = btnAttach;
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
                        SafeBeginInvoke(() => MessageBox.Show(this, string.Format(LocalizationService.Translate("Upload error: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                        SafeBeginInvoke(() => MessageBox.Show(this, $"Cannot compute file hash: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
                        return;
                    }

                    // Upload file via multipart/form-data to API
                    try
                    {
                        using var fs = new FileStream(encryptedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var content = new MultipartFormDataContent();
                        var streamContent = new StreamContent(fs);
                        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                        content.Add(streamContent, "file", Path.GetFileName(encryptedPath));

                        var resp = await ApiClient.Instance.GetHttpClient().PostAsync("api/files/upload", content).ConfigureAwait(false);
                        var respStr = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode)
                        {
                            string errorMessage = LocalizationService.Translate("Cannot upload file to server.");
                            if (!string.IsNullOrWhiteSpace(respStr))
                            {
                                try
                                {
                                    var errorDoc = System.Text.Json.JsonDocument.Parse(respStr);
                                    if (errorDoc.RootElement.TryGetProperty("message", out var msgProp))
                                    {
                                        errorMessage = $"Upload failed: {msgProp.GetString()}";
                                    }
                                    else if (errorDoc.RootElement.TryGetProperty("error", out var errProp))
                                    {
                                        errorMessage = $"Upload failed: {errProp.GetString()}";
                                    }
                                    else
                                    {
                                        errorMessage = $"Upload failed (HTTP {(int)resp.StatusCode}): {respStr}";
                                    }
                                }
                                catch
                                {
                                    errorMessage = $"Upload failed (HTTP {(int)resp.StatusCode}): {respStr}";
                                }
                            }
                            else
                            {
                                errorMessage = $"Upload failed with HTTP error code {(int)resp.StatusCode}.";
                            }

                            SafeBeginInvoke(() => MessageBox.Show(this, errorMessage, LocalizationService.Translate("File Upload Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                            SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                            return;
                        }
                        catch (ArgumentException ex)
                        {
                            SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                            var errMsg = string.IsNullOrWhiteSpace(msgErr) ? LocalizationService.Translate("Failed to create message on server.") : msgErr;
                            SafeBeginInvoke(() => MessageBox.Show(this, errMsg, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                                    if (msgRes.ExpiresAt.HasValue)
                                        _expirationService.TrackMessage(msgRes.MessageID, msgRes.ExpiresAt.Value);
                                    SafeBeginInvoke(() =>
                                    {
                                        _messageDates[msgRes.MessageID] = msgRes.SentAt.ToLocalTime();
                                        _currentMsgs.Add((msgRes.MessageID, payload, true, msgRes.SentAt.ToLocalTime().ToString("HH:mm"), msgRes.SenderUsername ?? ""));
                                        BuildMessages();
                                        var time = msgRes.SentAt.ToLocalTime().ToString("HH:mm");
                                        RefreshConversationItem(_activeConvId, payload, true, "", time, messageId: msgRes.MessageID);
                                    });
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
                                    SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("SignalR"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeBeginInvoke(() => MessageBox.Show(this, $"Upload error: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
            var btnTimer = MakeInputBtn("⏱"); _btnTimer = btnTimer;
            btnTimer.Click += (s, e) =>
            {
                // Show context menu to select self-destruct time
                var menu = new ContextMenuStrip();
                menu.Items.Add(LocalizationService.Translate("No self-destruct"), null, (_, __) => { _selfDestructSeconds = null; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(LocalizationService.Translate("5 seconds"), null, (_, __) => { _selfDestructSeconds = 5; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(LocalizationService.Translate("10 seconds"), null, (_, __) => { _selfDestructSeconds = 10; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(LocalizationService.Translate("30 seconds"), null, (_, __) => { _selfDestructSeconds = 30; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(LocalizationService.Translate("1 minute"), null, (_, __) => { _selfDestructSeconds = 60; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(LocalizationService.Translate("5 minutes"), null, (_, __) => { _selfDestructSeconds = 300; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(LocalizationService.Translate("1 hour"), null, (_, __) => { _selfDestructSeconds = 3600; UpdateTimerButtonText(btnTimer); });
                menu.Items.Add(LocalizationService.Translate("1 day"), null, (_, __) => { _selfDestructSeconds = 86400; UpdateTimerButtonText(btnTimer); });
                menu.Show(btnTimer, new Point(0, btnTimer.Height));
            };

            _tbMessage = new TelegramTextBox { Height = 36 };
            _tbMessage.SetPlaceholder("Write a message...");
            _tbMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Return && !e.Shift) { e.SuppressKeyPress = true; SendMessage(); } };

            var btnEmoji = MakeInputBtn("😊"); _btnEmoji = btnEmoji;
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
            var btnMic = MakeInputBtn("🎤"); _btnMic = btnMic;
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
                        SafeBeginInvoke(() => MessageBox.Show(this, $"Cannot compute file hash: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                                    string errorMessage = LocalizationService.Translate("Cannot upload file to server.");
                                    if (!string.IsNullOrWhiteSpace(uploadErr))
                                    {
                                        errorMessage = $"Upload failed: {uploadErr}";
                                    }
                                    else if (!string.IsNullOrWhiteSpace(respStr))
                                    {
                                        try
                                        {
                                            var errorDoc = System.Text.Json.JsonDocument.Parse(respStr);
                                            if (errorDoc.RootElement.TryGetProperty("message", out var msgProp))
                                            {
                                                errorMessage = $"Upload failed: {msgProp.GetString()}";
                                            }
                                            else if (errorDoc.RootElement.TryGetProperty("error", out var errProp))
                                            {
                                                errorMessage = $"Upload failed: {errProp.GetString()}";
                                            }
                                            else
                                            {
                                                errorMessage = $"Upload failed: {respStr}";
                                            }
                                        }
                                        catch
                                        {
                                            errorMessage = $"Upload failed: {respStr}";
                                        }
                                    }

                                    SafeBeginInvoke(() => MessageBox.Show(this, errorMessage, LocalizationService.Translate("Voice Upload Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                                    SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                                    return;
                                }
                                catch (ArgumentException ex)
                                {
                                    SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                                    var errMsg = string.IsNullOrWhiteSpace(msgErr) ? LocalizationService.Translate("Failed to create message on server.") : msgErr;
                                    SafeBeginInvoke(() => MessageBox.Show(this, errMsg, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                                            if (msgRes.ExpiresAt.HasValue)
                                                _expirationService.TrackMessage(msgRes.MessageID, msgRes.ExpiresAt.Value);
                                            SafeBeginInvoke(() =>
                                            {
                                                _messageDates[msgRes.MessageID] = msgRes.SentAt.ToLocalTime();
                                                _currentMsgs.Add((msgRes.MessageID, payload, true, msgRes.SentAt.ToLocalTime().ToString("HH:mm"), msgRes.SenderUsername ?? ""));
                                                BuildMessages();
                                                var time = msgRes.SentAt.ToLocalTime().ToString("HH:mm");
                                                RefreshConversationItem(_activeConvId, payload, true, "", time, messageId: msgRes.MessageID);
                                            });
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
                                    SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("SignalR"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                        SafeBeginInvoke(() => MessageBox.Show(this, $"Upload error: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                            SafeBeginInvoke(() => MessageBox.Show(this, $"Voice encryption error: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
                        }
                    }
                }
                catch (Exception ex)
                {
                    SafeBeginInvoke(() => MessageBox.Show(this, $"Recording error: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
            };
            var btnSend = new TelegramButton { Text = "↑", Height = 36, Width = 36, Font = TG.FontSemiBold(14f), Radius = 18, Visible = false };
            _btnSend = btnSend;
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
                _fontTimerActive ??= new Font("Segoe UI Emoji", 10f);
                btnTimer.Font = _fontTimerActive;
            }
            else
            {
                btnTimer.Text = "⏱";
                btnTimer.ForeColor = TG.Blue;
                _fontTimerInactive ??= new Font("Segoe UI Emoji", 13f);
                btnTimer.Font = _fontTimerInactive;
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
                BackColor = TG.SidebarBg,
                Visible = true,   // luôn visible, chỉ di chuyển X
                Width = smw,
                Left = -smw,      // bắt đầu ẩn ngoài màn hình bên trái
            };
            // Border trái để phân cách với chat area
            _pnlSettingsMenu.Paint += (s, e) =>
            {
                using var pen = new Pen(TG.Divider);
                e.Graphics.DrawLine(pen, 0, 0, 0, _pnlSettingsMenu.Height);
            };

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
                ForeColor = TG.TitleBarFg,
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
                ForeColor = TG.TitleBarFg,
                AutoSize = false,
                Height = 28,           // tăng từ 22 → 28 để chứa dấu tiếng Việt (ễ, ắ...)
                Width = smw - 80 - 14, // smw - leftPos(80) - rightMargin(14)
                Location = new Point(80, 58),
                BackColor = Color.Transparent,
                AutoEllipsis = true,   // hiện "..." nếu tên quá dài
            };

            // Resize handler: cập nhật width khi menu thay đổi kích thước
            _pnlSettingsHeader.Resize += (s, e) =>
            {
                _lblSettingsUserName.Width = _pnlSettingsHeader.Width - 80 - 14;
            };

            _pnlSettingsHeader.Controls.AddRange(new Control[] { btnClose, _settingsAvatar, _lblSettingsUserName });

            // ── Menu items ────────────────────────────
            var menuItems = new (string Emoji, string Label, bool HasToggle)[]
{
    ("👤", "My Profile",      false),
    ("👥", "New Group",       false),
    ("🪪", "Contacts",        false),
    ("🔖", "Saved Messages",  false),
    ("⚙️", "Settings",        false),
    ("🌙", "Night Mode",      true),
};

            var pnlMenuList = new Panel { Dock = DockStyle.Fill, BackColor = TG.SidebarBg };
            _pnlSettingsMenuList = pnlMenuList;
            pnlMenuList.Resize += (s, e) =>
            {
                foreach (Control c in pnlMenuList.Controls)
                    c.Width = pnlMenuList.ClientSize.Width;
            };
            int my = 8;
            foreach (var item in menuItems)
            {
                string key = item.Label;

                var row = BuildSettingsRow(item.Emoji, key, item.HasToggle, newState => _settingsToggles[key] = newState);
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
                Text = "SecureChat v1.0 · NT106 Group 6",
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
            _pnlSettingsMenu.Controls.Add(_pnlSettingsHeader);
        }

        private Panel BuildSettingsRow(string emoji, string label, bool hasToggle, Action<bool>? onToggle = null)
        {
            var pnl = new Panel { Height = 48, BackColor = TG.SidebarBg, Cursor = Cursors.Hand };

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
                using var pen = new Pen(TG.DividerLight);
                e.Graphics.DrawLine(pen, 56, 47, pnl.Width, 47);
            };
            pnl.Resize += (s, e) =>
            {
                // clamp width to avoid negative values when panel is very narrow
                lblLabel.Width = Math.Max(0, pnl.Width - 56 - (hasToggle ? 56 : 12));
            };

            if (hasToggle)
            {
                var toggle = new Panel { Size = new Size(44, 24), BackColor = Color.Transparent, Cursor = Cursors.Hand };
                toggle.Paint += (s, e) =>
                {
                    bool on = _settingsToggles.TryGetValue(label, out bool v) ? v : false;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var r = new Rectangle(0, 2, 40, 20);
                    using var brush = new SolidBrush(on ? TG.Blue : Color.FromArgb(0xCC, 0xCC, 0xCC));
                    e.Graphics.FillPath(brush, RoundedPanel.GetRoundedPath(r, 10));
                    int cx = on ? 22 : 2;
                    using var thumbBrush = new SolidBrush(TG.TitleBarFg);
                    e.Graphics.FillEllipse(thumbBrush, cx, 4, 16, 16);
                };
                toggle.Click += (s, e) =>
                {
                    bool on = _settingsToggles.TryGetValue(label, out bool v) ? v : false;
                    on = !on;
                    _settingsToggles[label] = on;
                    onToggle?.Invoke(on);
                    toggle.Invalidate();
                };
                pnl.Controls.Add(toggle);
                pnl.Resize += (s, e) => toggle.Location = new Point(Math.Max(0, pnl.Width - 52), 12);
                if (label == "Night Mode")
                    _nightModeToggle = toggle;
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
                            var localAvatarPath = DownloadAndCacheAvatar(_currentAvatarUrl);
                            var profile = new SecureChat.Client.Models.ProfileModel
                            {
                                FullName = _currentDisplayName,
                                Email = _currentEmail,
                                Username = _currentUsername,
                                AvatarPath = localAvatarPath ?? string.Empty,
                                StatusText = "online"
                            };
                            using var myprofile = new SecureChat.Client.Forms.Profile.frmMyProfile(profile);
                            myprofile.StartPosition = FormStartPosition.CenterParent;
                            if (myprofile.ShowDialog(this) == DialogResult.OK)
                            {
                                _currentDisplayName = profile.FullName;
                                _currentUsername = profile.Username;
                                _currentEmail = profile.Email;
                                if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
                                    _currentAvatarUrl = profile.AvatarUrl;
                                _decryptor.CurrentUsername = profile.Username;
                                SecureChat.Client.Services.AvatarService.UpdateAvatar(_currentAvatarUrl);
                                SecureChat.Client.Services.AvatarService.UpdateProfile(
                                    _currentDisplayName, _currentUsername, _currentEmail);
                                UpdateSettingsHeaderUI();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                MessageBox.Show(this, LocalizationService.Translate("Group name is required."), LocalizationService.Translate("Create Group"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                break;
                            }
                            if (dlg.ResultMemberIds == null || dlg.ResultMemberIds.Count == 0)
                            {
                                MessageBox.Show(this, LocalizationService.Translate("Please select members for the group."), LocalizationService.Translate("Create Group"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                break;
                            }

                            var http = ApiClient.Instance.GetHttpClient();

                            // Upload avatar nếu có
                            string? avatarUrl = null;
                            if (!string.IsNullOrWhiteSpace(dlg.ResultAvatarPath))
                            {
                                try
                                {
                                    using var fs = new FileStream(dlg.ResultAvatarPath, FileMode.Open, FileAccess.Read);
                                    using var ms = new MemoryStream();
                                    await fs.CopyToAsync(ms);
                                    ms.Position = 0;

                                    using var formContent = new MultipartFormDataContent();
                                    formContent.Add(new ByteArrayContent(ms.ToArray()), "File", "avatar.jpg");
                                    var uploadRes = await http.PostAsync("api/files/upload", formContent);
                                    if (uploadRes.IsSuccessStatusCode)
                                    {
                                        var uploadJson = await uploadRes.Content.ReadAsStringAsync();
                                        using var uploadDoc = System.Text.Json.JsonDocument.Parse(uploadJson);
                                        avatarUrl = uploadDoc.RootElement.GetProperty("url").GetString();
                                    }
                                }
                                catch { }
                            }

                            // E2EE: Generate AES-256 key upfront for the group
                            byte[] groupAesKey = new byte[SecureChat.Shared.Security.AesEncryption.KeySize];
                            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                                rng.GetBytes(groupAesKey);

                            // Build Members payload: list of { UserID, EncryptedKey }
                            var allUserIds = new List<string>(dlg.ResultMemberIds);
                            if (!string.IsNullOrWhiteSpace(_currentUserId) && !allUserIds.Contains(_currentUserId))
                                allUserIds.Insert(0, _currentUserId);

                            var members = new List<object>();
                            foreach (var uid in allUserIds)
                            {
                                string publicKey;
                                if (uid == _currentUserId)
                                {
                                    var (pubKey, _) = SecureChat.Shared.Security.KeyManager.GetKeyPair();
                                    publicKey = pubKey ?? string.Empty;
                                }
                                else
                                {
                                    publicKey = string.Empty;
                                    try
                                    {
                                        var userRes = await http.GetAsync($"api/users/{uid}");
                                        if (userRes.IsSuccessStatusCode)
                                        {
                                            var userJson = await userRes.Content.ReadAsStringAsync();
                                            var userData = System.Text.Json.JsonSerializer.Deserialize<SecureChat.DTOs.UserResponse>(userJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                            if (userData is not null)
                                                publicKey = userData.PublicKey;
                                        }
                                    }
                                    catch { }
                                }

                                string encryptedKeyB64;
                                if (!string.IsNullOrWhiteSpace(publicKey))
                                {
                                    try
                                    {
                                        byte[] enc = SecureChat.Shared.Security.RSAEncryption.Encrypt(groupAesKey, publicKey);
                                        encryptedKeyB64 = Convert.ToBase64String(enc);
                                    }
                                    catch
                                    {
                                        encryptedKeyB64 = "TBD";
                                    }
                                }
                                else
                                {
                                    encryptedKeyB64 = "TBD";
                                }

                                members.Add(new { UserID = uid, EncryptedKey = encryptedKeyB64 });
                            }

                            var payload = new
                            {
                                Type = SecureChat.Models.ConversationType.Group,
                                Name = dlg.ResultGroupName,
                                AvatarUrl = avatarUrl,
                                Members = members
                            };

                            var json = System.Text.Json.JsonSerializer.Serialize(payload);
                            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                            var res = await http.PostAsync("api/conversations", content);

                            if (!res.IsSuccessStatusCode)
                            {
                                string body = string.Empty;
                                try { body = await res.Content.ReadAsStringAsync(); } catch { /* ignore */ }

                                var msg = string.Format(LocalizationService.Translate("Create group failed. HTTP {0} {1}"), (int)res.StatusCode, res.ReasonPhrase);
                                if (!string.IsNullOrWhiteSpace(body))
                                    msg += $"\n\nServer response:\n{body}";

                                MessageBox.Show(this, msg, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                                System.Diagnostics.Debug.WriteLine($"Create group failed: {msg}");
                                break;
                            }

                            // success — lấy ID nhóm mới
                            string? newConvId = null;
                            try
                            {
                                var createdJson = await res.Content.ReadAsStringAsync();
                                using var createdDoc = System.Text.Json.JsonDocument.Parse(createdJson);
                                newConvId = createdDoc.RootElement.GetProperty("conversationID").GetString();
                            }
                            catch { }

                            // Đồng bộ avatar nếu có
                            if (avatarUrl != null && !string.IsNullOrWhiteSpace(newConvId))
                            {
                                try
                                {
                                    var imgRes = await http.GetAsync(avatarUrl);
                                    if (imgRes.IsSuccessStatusCode)
                                    {
                                        using var imgStream = await imgRes.Content.ReadAsStreamAsync();
                                        var img = new Bitmap(imgStream);
                                        if (_convAvatarCache.TryGetValue(newConvId, out var oldImg))
                                            oldImg?.Dispose();
                                        _convAvatarCache[newConvId] = new Bitmap(img);
                                    }
                                }
                                catch { }
                            }

                            // Warm the key cache so the first message can be decrypted immediately
                            if (!string.IsNullOrWhiteSpace(newConvId))
                                _ = _decryptor.EnsureConversationKeyAsync(newConvId);

                            // success — refresh conversations
                            await SyncConversationsAsync();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                UpdateEmptyStateUI();    // show pnlConvList, ẩn pnlEmptyState
                                LoadConversation(convId);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                case "Saved Messages":
                    {
                        if (string.IsNullOrWhiteSpace(_savedMessagesConvId))
                            break;

                        await LoadConversationsAsync();

                        _activeConvId = _savedMessagesConvId;
                        UpdateEmptyStateUI();
                        LoadConversation(_savedMessagesConvId);
                        break;
                    }
                case "Settings":
                    {
                        try
                        {
                            var localAvatarPath = DownloadAndCacheAvatar(_currentAvatarUrl);
                            var profile = new SecureChat.Client.Models.ProfileModel
                            {
                                FullName = _currentDisplayName,
                                Email = _currentEmail,
                                Username = _currentUsername,
                                AvatarPath = localAvatarPath ?? string.Empty,
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
                                if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
                                    _currentAvatarUrl = profile.AvatarUrl;
                                _decryptor.CurrentUsername = profile.Username;
                                UpdateSettingsHeaderUI();
                            }
                            else if (dr == DialogResult.No)
                            {
                                // 1. Call logout API to set Offline immediately
                                try
                                {
                                    var http = ApiClient.Instance.GetHttpClient();
                                    await http.PostAsync("api/auth/logout", null);
                                }
                                catch { }

                                // 2. Stop SignalR so OnDisconnectedAsync does NOT double-broadcast
                                _isLoggingOut = true;
                                if (_signalRClient != null)
                                {
                                    try { await _signalRClient.StopAsync(); }
                                    catch { }
                                }

                                // 3. Xóa Access Token để không gọi API được nữa
                                ApiClient.Instance.SetAccessToken(null);
            // Clear presence caches
            _userPresence.Clear();
            GlobalPresence.Clear();
            _convOtherUserId.Clear();
            _convRowCache.Clear();
            foreach (var kvp in _convAvatarCache)
                kvp.Value?.Dispose();
            _convAvatarCache.Clear();

            SecureChat.Shared.Security.KeyManager.Clear();
                                lock (_processedMessageIdsLock)
                                {
                                    _processedMessageIds.Clear();
                                }

                                // 4. Tìm login form cũ đang ẩn và hiện lại
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

                                // 5. Ẩn Form Main thay vì đóng để không kích hoạt FormClosed cascade
                            this.Hide();
                        }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        break;
                    }
                case "Night Mode":
                    {
                        NightModeService.Toggle();
                        break;
                    }
            }
        }

        public void OnNightModeChanged()
        {
            // ── Sidebar ────────────────────────────────────────────────
            _pnlConvList.BackColor = TG.SidebarBg;
            _settingsToggles["Night Mode"] = NightModeService.IsEnabled;
            _nightModeToggle?.Invalidate();

            // Sidebar panel + controls missing from original toggle
            if (_pnlSidebar != null) _pnlSidebar.BackColor = TG.SidebarBg;
            if (_pnlSearch != null) _pnlSearch.BackColor = TG.SidebarBg;
            if (_btnHamburger != null) _btnHamburger.ForeColor = TG.TitleBarFg;
            if (_tbSearch != null) _tbSearch.RefreshTheme();
            if (_pnlEmptyState != null) _pnlEmptyState.BackColor = TG.SidebarBg;
            if (_lblEmptyState != null) _lblEmptyState.ForeColor = TG.TextSecondary;
            if (_btnNewMessage != null) { _btnNewMessage.BackColor = TG.Blue; _btnNewMessage.ForeColor = Color.White; }

            // Header bar (hamburger + title area)
            if (_pnlSidebarHeader != null)
            {
                _pnlSidebarHeader.BackColor = TG.TitleBarBg;
                foreach (Control c in _pnlSidebarHeader.Controls)
                {
                    if (c is Label lbl) { lbl.ForeColor = TG.TitleBarFg; lbl.BackColor = Color.Transparent; }
                    else if (c is Button btn) btn.ForeColor = TG.TitleBarFg;
                }
            }

            // Settings menu header
            if (_pnlSettingsHeader != null)
            {
                _pnlSettingsHeader.BackColor = TG.TitleBarBg;
                foreach (Control c in _pnlSettingsHeader.Controls)
                {
                    if (c is Label lbl) lbl.ForeColor = TG.TitleBarFg;
                    else if (c is Button btn) btn.ForeColor = TG.TitleBarFg;
                }
            }
            if (_lblSettingsUserName != null) _lblSettingsUserName.ForeColor = TG.TitleBarFg;

            // Settings menu body
            if (_pnlSettingsMenu != null) _pnlSettingsMenu.BackColor = TG.SidebarBg;

            // Refresh toàn bộ settings menu rows (pnlMenuList + từng row + label)
            if (_pnlSettingsMenuList != null)
            {
                _pnlSettingsMenuList.BackColor = TG.SidebarBg;
                foreach (Control row in _pnlSettingsMenuList.Controls)
                {
                    row.BackColor = TG.SidebarBg;
                    foreach (Control c in row.Controls)
                    {
                        if (c is Label lbl && lbl.BackColor != Color.Transparent)
                            lbl.ForeColor = TG.TextPrimary;
                        else if (c is Label lbl2)
                            lbl2.ForeColor = TG.TextPrimary;
                    }
                    row.Invalidate();
                }
            }

            // Context menu (chat header ⋮)
            if (_chatMoreMenu != null)
            {
                _chatMoreMenu.BackColor = TG.SidebarBg;
                _chatMoreMenu.ForeColor = TG.TextPrimary;
                foreach (ToolStripItem item in _chatMoreMenu.Items)
                {
                    item.BackColor = TG.SidebarBg;
                    item.ForeColor = TG.TextPrimary;
                }
            }
                _lblChatEmpty.BackColor = Color.FromArgb(220, TG.WindowBg.R, TG.WindowBg.G, TG.WindowBg.B);

            // Chat header (title bar top) — use semantic colors, not blanket overwrite
            if (_pnlChatHeader != null)
            {
                _pnlChatHeader.BackColor = TG.SidebarBg;
                if (_lblChatName != null) _lblChatName.ForeColor = TG.TextPrimary;
                if (_lblChatStatus != null) _lblChatStatus.ForeColor = TG.TextSecondary;
                foreach (Control c in _pnlChatHeader.Controls)
                    if (c is Button b) b.ForeColor = TG.TextSecondary;
                if (_btnVideoCall != null) _btnVideoCall.ForeColor = TG.TextSecondary;
                if (_btnToggleSidebar != null) _btnToggleSidebar.ForeColor = TG.TextSecondary;
                _pnlChatHeader.Invalidate();
            }

            // Input bar
            if (_pnlInputBar != null) _pnlInputBar.BackColor = TG.SidebarBg;
            if (_tbMessage != null) _tbMessage.RefreshTheme();
            if (_btnSend != null) _btnSend.RefreshTheme();
            if (_btnAttach != null) _btnAttach.ForeColor = TG.Blue;
            if (_btnTimer != null) _btnTimer.ForeColor = TG.Blue;
            if (_btnEmoji != null) _btnEmoji.ForeColor = TG.Blue;
            if (_btnMic != null) _btnMic.ForeColor = TG.Blue;

            // Reply context bar
            if (_pnlReplyContext != null)
            {
                _pnlReplyContext.BackColor = TG.SidebarBg;
                if (_lblReplySender != null) _lblReplySender.ForeColor = TG.Blue;
                if (_lblReplyText != null) _lblReplyText.ForeColor = TG.TextSecondary;
                foreach (Control c in _pnlReplyContext.Controls)
                {
                    if (c is Label lbl && lbl != _lblReplySender && lbl != _lblReplyText)
                        lbl.ForeColor = TG.Blue;
                    else if (c is Panel p) p.BackColor = TG.Blue;
                }
                _pnlReplyContext.Invalidate();
            }

            // Right sidebar
            if (_pnlRightSidebar != null) _pnlRightSidebar.BackColor = TG.SidebarBg;
            if (_sbHeader != null)
            {
                _sbHeader.BackColor = TG.SidebarBg;
                foreach (Control c in _sbHeader.Controls)
                {
                    if (c is Label l) l.ForeColor = TG.TitleBarFg;
                    else if (c is Button b) b.ForeColor = TG.TitleBarFg;
                }
            }

            foreach (var pnl in _convRowCache.Values)
            {
                string id = pnl.Tag as string ?? "";
                bool isSavedRow = !string.IsNullOrWhiteSpace(_savedMessagesConvId) && id == _savedMessagesConvId;
                bool isSavedMessages = isSavedRow;
                pnl.BackColor = isSavedMessages ? TG.WindowBg : TG.SidebarBg;
                foreach (Control c in pnl.Controls)
                {
                    if (c is Label lbl)
                    {
                        string n = lbl.Name;
                        if (n == "lblPreview")
                            lbl.ForeColor = TG.TextSecondary;
                        else if (n == "lblTime")
                            lbl.ForeColor = TG.TextTime;
                        else
                            lbl.ForeColor = TG.TextName;
                    }
                }
            }

            _pnlMessages.BackColor = TG.ChatBg;
            ReloadWallpaper();
            if (_pnlChat != null) _pnlChat.BackColor = TG.SidebarBg;

            foreach (Control c in _pnlMessages.Controls)
            {
                if (c is ucAudioBubble audio)
                    audio.OnNightModeChanged();
                else if (c is ucFileBubble file)
                    file.OnNightModeChanged();
            }

            if (_sbBody != null)
            {
                _sbBody.BackColor = TG.WindowBg;
                foreach (Control c in _sbBody.Controls)
                {
                    if (c is ucGroupMemberItem member)
                        member.OnNightModeChanged();
                    else if (c is Label lbl)
                    {
                        Color fg = lbl.ForeColor;
                        // Giữ màu "Online" xanh
                        if (fg == Color.FromArgb(0x21, 0xA1, 0x66)) continue;
                        // Bảo toàn secondary text nếu đang là màu TextSecondary (ở cả 2 theme)
                        if (fg == Color.FromArgb(0x70, 0x70, 0x70) ||   // TextSecondary_L
                            fg == Color.FromArgb(0xA3, 0xAD, 0xB8))     // TextSecondary_D
                            lbl.ForeColor = TG.TextSecondary;
                        else
                            lbl.ForeColor = TG.TextPrimary;
                        lbl.BackColor = Color.Transparent;
                    }
                    else if (c is Panel p)
                    {
                        p.BackColor = TG.WindowBg;
                        foreach (Control pc in p.Controls)
                        {
                            if (pc is ucGroupMemberItem mi)
                                mi.OnNightModeChanged();
                            else if (pc is Label pl)
                            {
                                Color fg = pl.ForeColor;
                                if (fg == Color.FromArgb(0x70, 0x70, 0x70) ||
                                    fg == Color.FromArgb(0xA3, 0xAD, 0xB8))
                                    pl.ForeColor = TG.TextSecondary;
                                else
                                    pl.ForeColor = TG.TextPrimary;
                            }
                        }
                        p.Invalidate();
                    }
                }
                _sbBody.Invalidate(true);
            }

            // Pinned message bar (bị bỏ sót - không nằm trong OnNightModeChanged ban đầu)
            if (_pnlPinnedBar != null)
            {
                _pnlPinnedBar.BackColor = TG.Divider;
                _lblPinnedText.ForeColor = TG.TextPrimary;
                foreach (Control c in _pnlPinnedBar.Controls)
                    if (c is Label lbl && lbl != _lblPinnedText)
                        lbl.ForeColor = TG.TextSecondary; // downArrow
                _pnlPinnedBar.Invalidate(true);
            }
            if (_pnlPinnedPopup != null)
            {
                _pnlPinnedPopup.BackColor = TG.SidebarBg;
                if (_pnlPinnedPopup.Visible) RebuildPinnedPopup(); // rebuild để item con lấy màu mới
            }
            if (_pnlPinnedBottomBar != null)
            {
                _pnlPinnedBottomBar.BackColor = TG.Divider;
                _lblPinnedBottomText.ForeColor = TG.TextPrimary;
                _pnlPinnedBottomBar.Invalidate(true);
            }

            _pnlMessages.Invalidate(true);

            if (!string.IsNullOrWhiteSpace(_activeConvId))
                BuildMessages();
        }

        private void UpdateSettingsHeaderUI()
        {
            if (_settingsAvatar != null)
            {
                _settingsAvatar.SetName(_currentDisplayName);
                LoadAvatarToControl(_settingsAvatar, _currentAvatarUrl);
            }
            if (_lblSettingsUserName != null)
                _lblSettingsUserName.Text = _currentDisplayName;
        }

        private void OnExternalAvatarChanged()
        {
            if (InvokeRequired)
            {
                SafeBeginInvoke(OnExternalAvatarChanged);
                return;
            }
            _currentAvatarUrl = SecureChat.Client.Services.AvatarService.CurrentAvatarUrl;
            _currentDisplayName = SecureChat.Client.Services.AvatarService.CurrentDisplayName;
            UpdateSettingsHeaderUI();

            // Refresh chat header avatar if viewing Saved Messages or a DM
            if (!string.IsNullOrWhiteSpace(_activeConvId))
            {
                _chatAvatar.SetName(_currentDisplayName);
                _ = RefreshAvatarForConversationAsync(_activeConvId, _currentAvatarUrl);
            }

            // Refresh Saved Messages row in sidebar
            if (!string.IsNullOrWhiteSpace(_savedMessagesConvId))
            {
                _ = RefreshAvatarForConversationAsync(_savedMessagesConvId, _currentAvatarUrl);
            }

            // Refresh all DM rows in sidebar (where current user is the other party)
            for (int i = 0; i < _convs.Count; i++)
            {
                var c = _convs[i];
                if (!c.IsGroup && _convOtherUserId.TryGetValue(c.Id, out var oid) && oid == _currentUserId)
                {
                    _convs[i] = (c.Id, _currentDisplayName, c.Preview, c.Time, c.Unread, c.IsGroup);
                    _ = RefreshAvatarForConversationAsync(c.Id, _currentAvatarUrl);
                    if (_convRowCache.TryGetValue(c.Id, out var row))
                    {
                        foreach (Control ctrl in row.Controls)
                        {
                            if (ctrl is Label lbl && lbl.Location.Y == 10)
                                lbl.Text = _currentDisplayName;
                            else if (ctrl is AvatarControl av)
                                av.SetName(_currentDisplayName);
                        }
                        _convRowCache[c.Id] = row;
                    }
                }
            }
        }

        private string? DownloadAndCacheAvatar(string url)
        {
            return SecureChat.Client.Services.AvatarCacheService.DownloadAsync(url)
                .GetAwaiter().GetResult();
        }

        private void LoadAvatarToControl(AvatarControl ctrl, string url)
        {
            try
            {
                var img = SecureChat.Client.Services.AvatarCacheService.LoadImage(url);
                if (img != null)
                {
                    var old = ctrl.Photo;
                    if (old != null)
                    {
                        ctrl.Photo = null;
                        old.Dispose();
                    }
                    ctrl.Photo = new Bitmap(img);
                    ctrl.Invalidate();
                }
            }
            catch
            {
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

            bool isSavedConv = !string.IsNullOrWhiteSpace(_savedMessagesConvId) && convId == _savedMessagesConvId;

            // Hide video call button for saved messages
            if (_btnVideoCall != null)
                _btnVideoCall.Visible = !isSavedConv;

            // Luôn clear ảnh header cũ trước khi chuyển conversation
            var oldHeaderPhoto = _chatAvatar.Photo;
            if (oldHeaderPhoto != null)
            {
                _chatAvatar.Photo = null;
                oldHeaderPhoto.Dispose();
            }

            string headerName = isSavedConv && !string.IsNullOrWhiteSpace(_currentDisplayName) ? _currentDisplayName : conv.Name;
            _chatAvatar.SetName(headerName);
            _lblChatName.Text = isSavedConv ? "Saved Messages" : conv.Name;
            RestoreChatStatus();

            if (!conv.IsGroup && _signalRClient != null && _signalRClient.IsConnected)
            {
                if (_convOtherUserId.TryGetValue(convId, out var otherId))
                    _ = _signalRClient.QueryUserPresenceAsync(otherId);
            }

            // Set ảnh header từ cache nếu có (tạo bản sao riêng để tránh shared reference)
            if (_convAvatarCache.TryGetValue(convId, out var cachedImg) && cachedImg != null)
                _chatAvatar.Photo = new Bitmap(cachedImg);

            _chatAvatar.Invalidate();

            // Đảm bảo có list (rỗng nếu chưa sync) trước khi vẽ.
            BuildMessages();

            UpdateTitleBar();

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

            // Refresh tick Sent/Delivered/Read cho các tin MÌNH đã gửi trong conv này.
            // Cần làm RIÊNG biệt khỏi SyncMessagesForActiveConversationAsync vì hàm đó
            // bị chặn bởi _syncedConversations (chỉ chạy 1 lần/session) — nếu không có
            // bước này, tick sẽ "đứng" ở trạng thái cũ cho tới khi người gửi (mình) đang
            // online đúng lúc đối phương đọc tin (nhận được SignalR push realtime).
            _ = Task.Run(async () =>
            {
                try
                {
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var res = await http.GetAsync($"api/conversations/{convId}/messages/delivery-status");
                    if (!res.IsSuccessStatusCode) return;

                    var json = await res.Content.ReadAsStringAsync();
                    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var entries = System.Text.Json.JsonSerializer.Deserialize<List<DeliveryStatusEntry>>(json, opts);
                    if (entries is null || entries.Count == 0) return;

                    bool anyChanged = false;
                    foreach (var entry in entries)
                    {
                        var newStatus = entry.Delivery switch
                        {
                            "Read"      => SecureChat.DTOs.DeliveryStatus.Read,
                            "Delivered" => SecureChat.DTOs.DeliveryStatus.Delivered,
                            _           => SecureChat.DTOs.DeliveryStatus.Sent,
                        };

                        _msgDelivery.AddOrUpdate(entry.MessageID,
                            addValueFactory:    _ => { anyChanged = true; return newStatus; },
                            updateValueFactory: (_, cur) =>
                            {
                                if (cur != newStatus) { anyChanged = true; return newStatus; }
                                return cur;
                            });
                    }

                    if (anyChanged && convId == _activeConvId)
                        SafeBeginInvoke(() => BuildMessages());
                }
                catch { }
            });
        }

        private sealed class DeliveryStatusEntry
        {
            public string MessageID { get; set; } = "";
            public string Delivery { get; set; } = "";
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
                var (ok, view, _) = await ApiClient.Instance
                    .GetAsync<ConversationViewResponse>($"api/conversations/{_activeConvId}/view");
                if (!ok || view?.Members == null || view.Members.Count == 0)
                    return;

                var isGroup = conv.IsGroup;
                var members = view.Members;

                // Populate _senderAvatarMap for all members
                foreach (var m in members)
                {
                    if (m.User != null && !string.IsNullOrWhiteSpace(m.User.Username) && !string.IsNullOrWhiteSpace(m.User.AvatarURL))
                        _senderAvatarMap[m.User.Username] = m.User.AvatarURL;
                }

                SafeBeginInvoke(() =>
                {
                    _sbBody.SuspendLayout();
                    _sbBody.Controls.Clear();

                    if (isGroup)
                        BuildGroupSidebar(_sbBody, members);
                    else
                        BuildDirectSidebar(_sbBody, members);

                    _sbBody.ResumeLayout();
                });
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

            var otherId = members.FirstOrDefault(m => m.UserID != _currentUserId)?.UserID ?? other.UserID;

            int y = 24;

            // Large avatar (centered)
            var avatar = new AvatarControl
            {
                Size = new Size(100, 100),
                Location = new Point((body.ClientSize.Width - 100) / 2, y)
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
            y += 6;

            // Presence status (centered, fixed width)
            string presenceText;
            bool isOnline = false;
            if (_userPresence.TryGetValue(otherId, out var pp))
            {
                isOnline = pp.IsOnline;
                presenceText = Helpers.PresenceFormatter.GetPresenceText(pp.IsOnline, pp.LastSeenUtc);
            }
            else if (other.ShowOnlineStatus)
                presenceText = Helpers.PresenceFormatter.GetPresenceText(false, other.LastSeenUtc);
            else
                presenceText = LocalizationService.Translate("offline");

            AppendCenteredLabel(body, presenceText,
                TG.FontRegular(10f), isOnline ? Color.FromArgb(0x21, 0xA1, 0x66) : TG.TextSecondary, ref y);
            y += 16;

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
                Location = new Point((body.ClientSize.Width - 100) / 2, y)
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
                AutoSize = false,
                Size = new Size(260, 20), // 14px trước đó quá thấp cho font 9pt, cắt mất đáy chữ "E"
                Location = new Point(20, y)
            });
            y += 26;

            // Lấy role của mình để quyết định có hiện menu admin không
            var myMember = members.FirstOrDefault(m => m.User?.UserID == _currentUserId);
            var myRole   = myMember?.Role ?? SecureChat.Models.MemberRole.Member;
            bool canManage = myRole >= SecureChat.Models.MemberRole.Moderator;

            // Member list
            foreach (var m in members)
            {
                var displayName = m.User?.DisplayName ?? m.Nickname ?? "Unknown";
                var status = (m.User?.ShowOnlineStatus == true)
                    ? (m.IsOnline ? Helpers.PresenceFormatter.GetPresenceText(true, null)
                                  : Helpers.PresenceFormatter.GetPresenceText(false, m.User?.LastSeenUtc))
                    : "offline";
                var role = m.Role switch
                {
                    SecureChat.Models.MemberRole.Owner => LocalizationService.Translate("Admin"),
                    SecureChat.Models.MemberRole.Moderator => LocalizationService.Translate("Moderator"),
                    _ => LocalizationService.Translate("Member")
                };

                // Wrapper panel để chứa item + nút ⋮
                var rowPanel = new Panel
                {
                    Width  = body.ClientSize.Width > 0 ? body.ClientSize.Width : 340,
                    Height = ucGroupMemberItem.ITEM_HEIGHT, // dùng đúng hằng số thật, tránh lệch lại như trước (56 vs 78/100)
                    Location = new Point(0, y),
                    BackColor = Color.Transparent
                };

                var item = new ucGroupMemberItem
                {
                    Dock = DockStyle.None,
                    Width = rowPanel.Width - (canManage ? 32 : 0),
                    Height = ucGroupMemberItem.ITEM_HEIGHT,
                    Location = Point.Empty,
                    BackColor = Color.Transparent
                };
                item.DisplayName = displayName;
                item.Status      = status;
                item.Role        = role;
                item.SetInitial(displayName.Length > 0
                    ? displayName[0].ToString().ToUpperInvariant()
                    : "?");
                rowPanel.Controls.Add(item);

                // Nút ⋮ — chỉ hiện nếu mình có quyền quản lý VÀ target không phải chính mình
                // VÀ role target thấp hơn role mình (Moderator không kick Moderator khác)
                bool isSelf        = m.User?.UserID == _currentUserId;
                bool targetLower   = m.Role < myRole;
                bool showMenu      = canManage && !isSelf && targetLower;

                if (showMenu)
                {
                    var memberId  = m.MemberID; // capture cho closure
                    var memberName = displayName;

                    var btnMore = new Button
                    {
                        Text      = "⋮",
                        FlatStyle = FlatStyle.Flat,
                        Size      = new Size(28, 28),
                        Location  = new Point(rowPanel.Width - 30, (rowPanel.Height - 28) / 2),
                        Cursor    = Cursors.Hand,
                        Font      = TG.FontSemiBold(13f),
                        ForeColor = TG.TextSecondary,
                        BackColor = Color.Transparent,
                    };
                    btnMore.FlatAppearance.BorderSize      = 0;
                    btnMore.FlatAppearance.MouseOverBackColor  = Color.FromArgb(20, 0, 0, 0);
                    btnMore.FlatAppearance.MouseDownBackColor  = Color.FromArgb(40, 0, 0, 0);

                    btnMore.Click += (s, e) =>
                    {
                        var menu = new ContextMenuStrip
                        {
                            Font = TG.FontRegular(9.5f),
                            BackColor = TG.SidebarBg,
                            ForeColor = TG.TextPrimary,
                            ShowImageMargin = false,
                            Renderer = new ToolStripProfessionalRenderer(new ChatMenuColorTable())
                        };

                        // ── Mute submenu ──
                        var itemMute = new ToolStripMenuItem($"  🔇  {LocalizationService.Translate("Mute")}");
                        itemMute.ForeColor = TG.TextPrimary;

                        var muteOptions = new[]
                        {
                            (LocalizationService.Translate("5 min"),      5),
                            (LocalizationService.Translate("30 min"),    30),
                            (LocalizationService.Translate("1 hour"),    60),
                            (LocalizationService.Translate("1 day"),     60 * 24),
                            (LocalizationService.Translate("Permanent"), 60 * 24 * 365 * 10),
                        };

                        foreach (var (label, minutes) in muteOptions)
                        {
                            var min = minutes;
                            var sub = new ToolStripMenuItem($"  {label}");
                            sub.ForeColor = TG.TextPrimary;
                            sub.Click += async (_, __) => await MuteMemberAsync(memberId, memberName, min);
                            itemMute.DropDownItems.Add(sub);
                        }

                        // ── Unmute ──
                        var itemUnmute = new ToolStripMenuItem($"  🔔  {LocalizationService.Translate("Unmute")}");
                        itemUnmute.ForeColor = TG.TextPrimary;
                        itemUnmute.Click += async (_, __) => await UnmuteMemberAsync(memberId, memberName);

                        // ── Kick ──
                        var itemKick = new ToolStripMenuItem($"  👢  {LocalizationService.Translate("Remove from group")}");
                        itemKick.ForeColor = Color.FromArgb(0xE2, 0x4B, 0x4A);
                        itemKick.Click += async (_, __) => await KickMemberAsync(memberId, memberName);

                        menu.Items.Add(itemMute);
                        menu.Items.Add(itemUnmute);
                        menu.Items.Add(new ToolStripSeparator());
                        menu.Items.Add(itemKick);
                        menu.Show(btnMore, new Point(0, btnMore.Height));
                    };

                    rowPanel.Controls.Add(btnMore);

                    // Cập nhật Left khi panel resize
                    rowPanel.Resize += (s, e) =>
                    {
                        item.Width    = rowPanel.Width - 32;
                        btnMore.Left  = rowPanel.Width - 30;
                    };
                }

                body.Controls.Add(rowPanel);
                y += rowPanel.Height;
            }

            body.AutoScrollMinSize = new Size(0, y + 20);
        }

        private async Task MuteMemberAsync(string memberId, string memberName, int minutes)
        {
            try
            {
                var bannedUntil = DateTime.UtcNow.AddMinutes(minutes);
                var http = ApiClient.Instance.GetHttpClient();
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Role              = (object?)null,
                    Nickname          = (string?)null,
                    ShowNotifications = (bool?)null,
                    BannedUntil       = bannedUntil,
                    EncryptedKey      = (string?)null
                });
                var res = await http.PatchAsync(
                    $"api/conversations/{_activeConvId}/members/{memberId}",
                    new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

                if (res.IsSuccessStatusCode)
                {
                    string duration = minutes >= 60 * 24 * 365
                        ? LocalizationService.Translate("permanently")
                        : minutes >= 60 * 24 ? string.Format(LocalizationService.Translate("{0} days"), minutes / (60 * 24))
                        : minutes >= 60      ? string.Format(LocalizationService.Translate("{0} hours"), minutes / 60)
                        : string.Format(LocalizationService.Translate("{0} minutes"), minutes);
                    MessageBox.Show(string.Format(LocalizationService.Translate("Muted {0} for {1}."), memberName, duration), LocalizationService.Translate("Success"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadRightSidebarContentAsync();
                }
                else
                    MessageBox.Show(string.Format(LocalizationService.Translate("Mute failed ({0})."), (int)res.StatusCode), LocalizationService.Translate("Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationService.Translate("Error: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UnmuteMemberAsync(string memberId, string memberName)
        {
            try
            {
                var http = ApiClient.Instance.GetHttpClient();
                // Set BannedUntil = past to unmute
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Role              = (object?)null,
                    Nickname          = (string?)null,
                    ShowNotifications = (bool?)null,
                    BannedUntil       = DateTime.UtcNow.AddSeconds(-1),
                    EncryptedKey      = (string?)null
                });
                var res = await http.PatchAsync(
                    $"api/conversations/{_activeConvId}/members/{memberId}",
                    new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show(string.Format(LocalizationService.Translate("Unmuted {0}."), memberName), LocalizationService.Translate("Success"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadRightSidebarContentAsync();
                }
                else
                    MessageBox.Show(string.Format(LocalizationService.Translate("Unmute failed ({0})."), (int)res.StatusCode), LocalizationService.Translate("Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationService.Translate("Error: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task KickMemberAsync(string memberId, string memberName)
        {
            var confirm = MessageBox.Show(
                string.Format(LocalizationService.Translate("Kick {0} from group?"), memberName),
                LocalizationService.Translate("Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                var http = ApiClient.Instance.GetHttpClient();
                var res = await http.DeleteAsync(
                    $"api/conversations/{_activeConvId}/members/{memberId}");

                if (res.IsSuccessStatusCode)
                {
                    MessageBox.Show(string.Format(LocalizationService.Translate("Kicked {0} from group."), memberName), LocalizationService.Translate("Success"),
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await LoadRightSidebarContentAsync();
                }
                else
                    MessageBox.Show(string.Format(LocalizationService.Translate("Kick failed ({0})."), (int)res.StatusCode), LocalizationService.Translate("Error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(LocalizationService.Translate("Error: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void AppendCenteredLabel(Panel body, string text, Font font, Color color, ref int y)
        {
            int maxW = Math.Max(200, body.ClientSize.Width - 40);
            var lbl = new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(maxW, 0),
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(maxW, 0)
            };
            if (lbl.Height < 24) lbl.Height = 24;
            lbl.Location = new Point((body.ClientSize.Width - lbl.Width) / 2, y);
            body.Controls.Add(lbl);
            y += lbl.Height;
        }

        private static void AppendDivider(Panel body, ref int y)
        {
            body.Controls.Add(new Panel
            {
                Height = 1,
                Width = Math.Max(200, body.ClientSize.Width - 40),
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
                MaximumSize = new Size(Math.Max(200, body.ClientSize.Width - 48), 0),
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
                SafeBeginInvoke(UpdateChatEmptyStateUI);
                return;
            }

            // Fetch unread counts for ALL conversations in parallel
            var unreadDict = new Dictionary<string, int>();
            var unreadTasks = list
                .Where(c => !string.IsNullOrWhiteSpace(c.ConversationID))
                .Select(async c =>
                {
                    var (uOk, uData, _) = await _messageService.GetUnreadCountAsync(c.ConversationID);
                    if (uOk && uData is not null)
                    {
                        lock (unreadDict) { unreadDict[c.ConversationID] = uData.Count; }
                    }
                }).ToArray();
            if (unreadTasks.Length > 0)
                await Task.WhenAll(unreadTasks);

            SafeBeginInvoke(() =>
            {
                // Lưu preview/thời gian hiện tại trước khi rebuild
                var existingPreviews = new Dictionary<string, string>();
                var existingTimes = new Dictionary<string, string>();
                foreach (var ec in _convs)
                {
                    existingPreviews[ec.Id] = ec.Preview;
                    existingTimes[ec.Id] = ec.Time;
                }

                // Chỉ rebuild _convs, KHÔNG clear _allMsgs/_syncedConversations
                _convs.Clear();

                // Xoá cache của các conversation không còn trong danh sách
                var newIds = new HashSet<string>(list.Select(c => c.ConversationID));
                foreach (var key in _allMsgs.Keys.Where(k => !newIds.Contains(k)).ToList())
                    _allMsgs.TryRemove(key, out _);
                foreach (var key in _syncedConversations.Where(k => !newIds.Contains(k)).ToList())
                    _syncedConversations.Remove(key);
                foreach (var key in _myMemberIdByConv.Keys.Where(k => !newIds.Contains(k)).ToList())
                    _myMemberIdByConv.TryRemove(key, out _);

                foreach (var c in list)
                {
                    string display = !string.IsNullOrWhiteSpace(c.Name)
                        ? c.Name!
                        : (c.Type == ConversationType.Group ? "Group" : "Direct chat");
                    string time = c.LastActivityAt?.ToLocalTime().ToString("HH:mm") ?? string.Empty;
                    bool isGroup = c.Type == ConversationType.Group;

                    // Giữ preview cũ nếu có (sẽ được background sync cập nhật sau)
                    string convPreview = existingPreviews.TryGetValue(c.ConversationID, out var oldPreview)
                        ? oldPreview
                        : string.Empty;

                    if (c.LastActivityAt == null && existingTimes.TryGetValue(c.ConversationID, out var oldTime))
                        time = oldTime;

                    int unread = unreadDict.TryGetValue(c.ConversationID, out var u) ? u : 0;
                    _convs.Add((c.ConversationID, display, convPreview, time, unread, isGroup));
                }

                // Pin saved messages to the top
                if (!string.IsNullOrWhiteSpace(_savedMessagesConvId))
                {
                    int savedIdx = _convs.FindIndex(c => c.Id == _savedMessagesConvId);
                    if (savedIdx > 0)
                    {
                        var saved = _convs[savedIdx];
                        _convs.RemoveAt(savedIdx);
                        _convs.Insert(0, saved);
                    }
                }

                BuildConvList();

                // Trigger background avatar loading for conversations with avatar URLs
                foreach (var c in list)
                {
                    if (!string.IsNullOrWhiteSpace(c.AvatarURL))
                        _ = RefreshAvatarForConversationAsync(c.ConversationID, c.AvatarURL);
                }

                if (_convs.Count > 0)
                {
                    // Preserve existing active conversation; do NOT auto-open Saved Messages.
                    if (string.IsNullOrWhiteSpace(_activeConvId) || !_convs.Any(c => c.Id == _activeConvId))
                    {
                        var firstNonSaved = _convs.FirstOrDefault(c => !string.IsNullOrWhiteSpace(_savedMessagesConvId) && c.Id != _savedMessagesConvId);
                        _activeConvId = firstNonSaved.Id ?? string.Empty;
                    }

                    if (!string.IsNullOrWhiteSpace(_activeConvId))
                    {
                        UpdateConvRowHighlight(_activeConvId);
                        LoadConversation(_activeConvId);
                        RefreshSidebarPreview();
                        RefreshAllSidebarPreviews();
                    }
                    else
                    {
                        _activeConvId = string.Empty;
                        _convs.Clear();
                        _pnlConvList.Controls.Clear();
                        _convRowCache.Clear();
                        UpdateEmptyStateUI();
                        UpdateChatEmptyStateUI();
                    }
                }
                else
                {
                    _activeConvId = string.Empty;
                    _pnlConvList.Controls.Clear();
                    _convRowCache.Clear();
                    UpdateEmptyStateUI();
                    UpdateChatEmptyStateUI();
                }
            });
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
                SafeBeginInvoke(() =>
                    MessageBox.Show(this, LocalizationService.Translate("Cannot determine current member in the conversation."),
                        LocalizationService.Translate("Sync messages"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                return;
            }

            var (ok, list, err) = await FetchAllMessagesAsync(convId);
            if (!ok || list is null)
            {
                SafeBeginInvoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(err))
                        MessageBox.Show(this, err, LocalizationService.Translate("Sync messages"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
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

            SafeBeginInvoke(() =>
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
                        _messageDates[dm.Id] = dm.Raw.SentAt.ToLocalTime();
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
                        _messageDates[dm.Id] = dm.Raw.SentAt.ToLocalTime();
                        merged.Add((dm.Id, dm.Text, dm.Out, dm.Time, dm.Sender));
                    }
                    foreach (var m in existing)
                        if (!decrypted.Exists(d => d.Id == m.Id) && !_hiddenMessageIds.Contains(m.Id))
                            merged.Add(m);
                    _allMsgs[convId] = merged;
                }

                _allMsgs[convId] = _allMsgs[convId]
                    .OrderBy(m => _messageDates.TryGetValue(m.Id, out var d) ? d : DateTime.MinValue)
                    .ToList();

                if (decrypted.Count > 0)
                    UpdateConversationPreview(convId, decrypted[^1]);

                if (convId == _activeConvId)
                    BuildMessages();
            });
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
            // Recalled message
            if (!string.IsNullOrEmpty(rawText) && rawText.StartsWith("recalled::"))
                return LocalizationService.Translate("Recalled message");

            // Forward metadata check
            if (!string.IsNullOrEmpty(messageId) && _forwardMetadata.ContainsKey(messageId))
                return LocalizationService.Translate("Forwarded message");

            // Fallback: text prefix (backward compat)
            const string forwardPrefix = "[Forwarded from ";
            if (!string.IsNullOrEmpty(rawText) && rawText.StartsWith(forwardPrefix))
                return LocalizationService.Translate("Forwarded message");

            string cleaned = ExtractActualText(rawText);
            if (cleaned.StartsWith("voice::")) return LocalizationService.Translate("Voice message");
            if (cleaned.StartsWith("file::")) return LocalizationService.Translate("File");
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

            // Re-pin Saved Messages to the top if it was displaced
            if (!string.IsNullOrWhiteSpace(_savedMessagesConvId) && convId != _savedMessagesConvId)
            {
                int savedIdx = _convs.FindIndex(x => x.Id == _savedMessagesConvId);
                if (savedIdx > 0)
                {
                    var saved = _convs[savedIdx];
                    _convs.RemoveAt(savedIdx);
                    _convs.Insert(0, saved);
                }
            }

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
                        else if (lbl.Name == "lblTime")
                        {
                            lbl.Text = timeStr;
                            // AutoSize thay đổi Width → cần reposition theo right-edge
                            lbl.Location = new Point(row.Width - lbl.Width - 12, 12);
                        }
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

            // Ensure Saved Messages row stays visually on top (z-order)
            if (!string.IsNullOrWhiteSpace(_savedMessagesConvId) && _savedMessagesConvId != convId)
            {
                if (_convRowCache.TryGetValue(_savedMessagesConvId, out var savedRow) && _pnlConvList.Controls.Contains(savedRow))
                    _pnlConvList.Controls.SetChildIndex(savedRow, 0);
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
                latest.Raw.SentAt.ToLocalTime().ToString("HH:mm"), messageId: latest.Id);
        }

        private void RefreshSidebarPreview()
        {
            if (string.IsNullOrWhiteSpace(_activeConvId)) return;
            var msgs = _currentMsgs;
            if (msgs.Count == 0) return;

            var latest = msgs[^1];
            string senderForPreview = latest.Out ? "" :
                (_senderDisplayNameMap.TryGetValue(latest.Sender, out var dn) && !string.IsNullOrEmpty(dn) ? dn : latest.Sender);
            RefreshConversationItem(_activeConvId, latest.Text, latest.Out, senderForPreview, latest.Time, messageId: latest.Id);
        }

        private void RefreshAllSidebarPreviews()
        {
            for (int i = 0; i < _convs.Count; i++)
            {
                var c = _convs[i];
                if (!_allMsgs.TryGetValue(c.Id, out var msgs) || msgs.Count == 0)
                    continue;

                var latest = msgs[^1];
                string senderForPreview = latest.Out ? "" :
                    (_senderDisplayNameMap.TryGetValue(latest.Sender, out var dn) && !string.IsNullOrEmpty(dn) ? dn : latest.Sender);
                RefreshConversationItem(c.Id, latest.Text, latest.Out, senderForPreview, latest.Time, messageId: latest.Id);
            }
        }

        /// <summary>
        /// Background sync: fetch and decrypt the last message for every conversation
        /// that currently shows no preview (e.g. "..." or empty). Called after login
        /// and after any conversation is opened.
        /// </summary>
        private async Task SyncLastMessagePreviewsAsync()
        {
            // Ensure only one background sync runs at a time
            lock (_lastMessageSyncLock)
            {
                if (_lastMessageSyncStarted) return;
                _lastMessageSyncStarted = true;
            }
            try
            {
                // Collect conversations that need preview sync
                var needSync = new List<(string Id, bool IsGroup)>();
            for (int i = 0; i < _convs.Count; i++)
            {
                var c = _convs[i];
                if (string.IsNullOrWhiteSpace(c.Preview))
                {
                    needSync.Add((c.Id, c.IsGroup));
                }
            }

            foreach (var (convId, isGroup) in needSync)
            {
                try
                {
                    var (ok, page, _) = await _messageService.GetMessagesAsync(convId, 1, null);
                    if (!ok || page is null || page.Count == 0) continue;

                    var latest = page[^1];

                    // Need myMemberId to determine isOut
                    if (!_myMemberIdByConv.TryGetValue(convId, out var myMemberId))
                    {
                        var (memOk, me, _) = await _messageService.GetMyMembershipAsync(convId);
                        if (memOk && me is not null)
                        {
                            myMemberId = me.MemberID;
                            _myMemberIdByConv[convId] = myMemberId;
                        }
                    }

                    var dm = await _decryptor.ProcessAsync(latest, myMemberId);

                    // Cache AES key and forward metadata
                    if (latest.Attachments != null)
                    {
                        foreach (var att in latest.Attachments)
                            HandleHybridEncryptedAttachment(latest.MessageID, att);
                    }
                    if (!dm.Out && !string.IsNullOrEmpty(dm.Sender) && !string.IsNullOrEmpty(dm.SenderDisplayName))
                        _senderDisplayNameMap[dm.Sender] = dm.SenderDisplayName;
                    if (!string.IsNullOrEmpty(latest.OriginalSenderID) && !string.IsNullOrEmpty(latest.OriginalSenderName))
                    {
                        _forwardMetadata[dm.Id] = latest.OriginalSenderName;
                        _forwardOriginalSenderId[dm.Id] = latest.OriginalSenderID!;
                    }

                    string senderForPreview = dm.Out ? "" : (!string.IsNullOrEmpty(dm.SenderDisplayName) ? dm.SenderDisplayName : dm.Sender);

                    SafeBeginInvoke(() =>
                        RefreshConversationItem(convId, dm.Text, dm.Out, senderForPreview, dm.Time, messageId: dm.Id));
                }
                catch
                {
                    // Silently skip failed conversations
                }
            }
            }
            finally
            {
                lock (_lastMessageSyncLock)
                {
                    _lastMessageSyncStarted = false;
                }
            }
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
                    var sep = new Panel
                    {
                        Height = 28,
                        Width = Math.Max(0, _pnlMessages.ClientSize.Width - _pnlMessages.Padding.Horizontal),
                        BackColor = Color.Transparent
                    };
                    sep.Paint += (s, e) =>
                    {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using var font = new Font("Segoe UI", 9f, FontStyle.Regular);
                        var textSize = e.Graphics.MeasureString(displayDate, font);
                        int pillW = (int)textSize.Width + 24;
                        int pillH = 22;
                        int pillX = (sep.ClientSize.Width - pillW) / 2;
                        int pillY = (sep.ClientSize.Height - pillH) / 2;
                        using var bgBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
                        using var path = new System.Drawing.Drawing2D.GraphicsPath();
                        path.AddArc(pillX, pillY, pillH, pillH, 90, 180);
                        path.AddArc(pillX + pillW - pillH, pillY, pillH, pillH, 270, 180);
                        path.CloseFigure();
                        e.Graphics.FillPath(bgBrush, path);
                        TextRenderer.DrawText(e.Graphics, displayDate, font,
                            new Point(pillX + 12, pillY + (pillH - (int)textSize.Height) / 2),
                            Color.FromArgb(220, 220, 220), TextFormatFlags.NoPadding);
                    };
                    sep.Location = new Point(0, y);
                    sep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

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
                return LocalizationService.Translate("Today");
            if (date.Date == today.AddDays(-1))
                return LocalizationService.Translate("Yesterday");
            var monthName = LocalizationService.Translate(date.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture));
            bool isVietnamese = LocalizationService.CurrentLanguage == LanguageType.Vietnamese;
            if (isVietnamese)
            {
                if (date.Year == today.Year)
                    return $"{date.Day} {monthName}";
                return $"{date.Day} {monthName}, {date.Year}";
            }
            else
            {
                if (date.Year == today.Year)
                    return $"{monthName} {date.Day}";
                return $"{monthName} {date.Day}, {date.Year}";
            }
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

            UpdateTitleBar();

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
                    fwdDisplayName = LocalizationService.Translate("You");
            }

            var wrapper = new Panel { BackColor = Color.Transparent };

            string fwdFullText = string.Format(LocalizationService.Translate("Forwarded from {0}"), fwdDisplayName);
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

                const int rightMargin = 30; // tăng để bubble voice lùi sang trái
                int voiceLeftOffset = (!isOut && isGroup) ? 44 : 10;
                const int voiceBubbleW = 300; // cố định, KHÔNG co giãn theo panel.Width

                audioBubble.Width  = voiceBubbleW;
                audioBubble.Top    = 4;
                audioBubble.Anchor = isOut
                    ? AnchorStyles.Top | AnchorStyles.Right
                    : AnchorStyles.Top | AnchorStyles.Left;

                // KHÔNG dùng panel.Width ngay lúc này — panel chưa add vào parent
                // nên Width = 0, khiến Left tính ra âm rất sâu (bubble trôi lệch trái).
                // Đặt Left tạm theo voiceLeftOffset, LayoutVoice() sẽ set lại đúng
                // ngay khi panel có kích thước thật (HandleCreated/ParentChanged/Resize).
                audioBubble.Left = voiceLeftOffset;

                panel.Height = audioBubble.Height + 8;

                void LayoutVoice()
                {
                    if (panel.ClientSize.Width <= 0) return;
                    // audioBubble GIỮ NGUYÊN 300px (giống bubble text co theo nội dung,
                    // không full-width theo panel chứa nó) — chỉ Left thay đổi để căn phải/trái
                    audioBubble.Width = voiceBubbleW;
                    audioBubble.Left  = isOut
                        ? panel.ClientSize.Width - voiceBubbleW - rightMargin
                        : voiceLeftOffset;
                }

                panel.Resize        += (s, e) => LayoutVoice();
                panel.HandleCreated += (s, e) => LayoutVoice();
                panel.ParentChanged += (s, e) => LayoutVoice();

                panel.Controls.Add(audioBubble);

                panel.PerformLayout();
                LayoutVoice(); // thử layout ngay, phòng khi panel đã có size sẵn
                return WrapForwardIfNeeded(panel, messageId, isOut);
            }

            // recalled::
            const string recallPrefix = "recalled::";
            if (!string.IsNullOrEmpty(text) && text.StartsWith(recallPrefix, StringComparison.Ordinal))
            {
                var recallPnl = new Panel { BackColor = Color.Transparent };
                int recallPad = 12;
                int recallLeft = (!isOut && isGroup) ? 44 : 10;
                int recallMaxW = 360;
                if (_pnlMessages != null && _pnlMessages.ClientSize.Width > 0)
                    recallMaxW = Math.Max(220, (int)(_pnlMessages.ClientSize.Width * 0.66f) - _pnlMessages.Padding.Horizontal);

                string recallText = LocalizationService.Translate("Recalled message");
                Size recallTextSz;
                using (var g = _pnlMessages.CreateGraphics())
                {
                    recallTextSz = TextRenderer.MeasureText(g, recallText, TG.FontRegular(9.5f),
                        new Size(recallMaxW - recallPad * 2, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                }
                int recallBw = Math.Min(recallMaxW, (int)recallTextSz.Width + recallPad * 2 + 20);
                int recallBh = (int)recallTextSz.Height + recallPad * 2 + 16;
                recallPnl.Height = recallBh + 16;

                recallPnl.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    int rx = isOut ? recallPnl.ClientSize.Width - recallBw - 10 : recallLeft;
                    int ry = 4;
                    Color rBg = isOut ? TG.MsgOutBg : TG.MsgInBg;

                    using var shadowBrush = new SolidBrush(Color.FromArgb(20, 0, 0, 0));
                    using var shadowPath = RoundedPanel.GetRoundedPath(new Rectangle(rx + 1, ry + 2, recallBw, recallBh), TG.RadiusBubble);
                    e.Graphics.FillPath(shadowBrush, shadowPath);

                    using var bubblePath = RoundedPanel.GetRoundedPath(new Rectangle(rx, ry, recallBw, recallBh), TG.RadiusBubble);
                    e.Graphics.FillPath(new SolidBrush(rBg), bubblePath);

                    var rTail = isOut
                        ? new[] { new Point(rx + recallBw, ry + recallBh - 8), new Point(rx + recallBw + 5, ry + recallBh), new Point(rx + recallBw, ry + recallBh) }
                        : new[] { new Point(rx, ry + recallBh - 8), new Point(rx - 5, ry + recallBh), new Point(rx, ry + recallBh) };
                    e.Graphics.FillPolygon(new SolidBrush(rBg), rTail);

                    using var iconFont = new Font("Segoe UI Symbol", 12f);
                    float iconX = rx + recallPad;
                    float iconY = ry + (recallBh - 24f) / 2;
                    e.Graphics.DrawString("\u21A9", iconFont, new SolidBrush(Color.FromArgb(153, 153, 153)), iconX, iconY);

                    float rTextX = iconX + 28;
                    float rTextY = ry + (recallBh - recallTextSz.Height) / 2;
                    using var paintFont = new Font("Segoe UI", 9.5f, FontStyle.Italic);
                    TextRenderer.DrawText(e.Graphics, recallText, paintFont,
                        new Rectangle((int)rTextX, (int)rTextY, recallMaxW - recallPad * 2 - 30, recallTextSz.Height),
                        Color.FromArgb(153, 153, 153), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                };

                return recallPnl;
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
                    var adv = SecureChat.Client.Settings.AdvancedSettings.Default;
                    string destination;

                    if (adv.AskDownloadPathEachFile)
                    {
                        using var sfd = new SaveFileDialog
                        {
                            FileName = fileName,
                            Filter = "All Files|*.*",
                            Title = "Save File"
                        };

                        if (sfd.ShowDialog(this) != DialogResult.OK)
                            return;

                        destination = sfd.FileName;
                    }
                    else
                    {
                        string dir = adv.ResolveDownloadPath();
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        destination = Path.Combine(dir, fileName);
                        int counter = 1;
                        while (File.Exists(destination))
                        {
                            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                            string ext = Path.GetExtension(fileName);
                            destination = Path.Combine(dir, $"{nameNoExt} ({counter}){ext}");
                            counter++;
                        }
                    }
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
                                        SafeBeginInvoke(() => MessageBox.Show(this, LocalizationService.Translate("Downloaded file hash does not match. File may have been tampered with or transmission error."), LocalizationService.Translate("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    SafeBeginInvoke(() => MessageBox.Show(this, $"Cannot verify hash: {ex.Message}", LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                                SafeBeginInvoke(() => MessageBox.Show(this, LocalizationService.Translate("Download complete."), LocalizationService.Translate("Downloaded"), MessageBoxButtons.OK, MessageBoxIcon.Information));
                            }
                            else
                            {
                                // No AES key found — save encrypted file as-is
                                if (File.Exists(destination))
                                    File.Delete(destination);
                                File.Move(encryptedTemp, destination);
                                SafeBeginInvoke(() => MessageBox.Show(this, LocalizationService.Translate("Download complete (cannot decrypt)."), LocalizationService.Translate("Downloaded"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                            SafeBeginInvoke(() => MessageBox.Show(this, LocalizationService.Translate("Download cancelled."), LocalizationService.Translate("Cancelled"), MessageBoxButtons.OK, MessageBoxIcon.Information));
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeBeginInvoke(() => MessageBox.Show(this, string.Format(LocalizationService.Translate("Failed to download file: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                    // Resolve display name; if it's current user → translated "You"
                    if (replySender != "You")
                    {
                        if (_senderDisplayNameMap.TryGetValue(replySender, out var dn) && !string.IsNullOrEmpty(dn))
                            replySender = dn;
                        else if (replySender == _currentUsername)
                            replySender = LocalizationService.Translate("You");
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
                    string fwdPrefix = LocalizationService.Translate("Forwarded from") + " ";
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
            // Khi có timer tự hủy: đảm bảo bubble đủ rộng cho [⏱ Xs] + [time] + [tick]
            bool hasExpiryTimer = !string.IsNullOrEmpty(messageId) && _expirationService.IsTracking(messageId);
            if (hasExpiryTimer)
            {
                var timeSzMeasure = TextRenderer.MeasureText(time, TG.FontRegular(7.5f));
                // Đo thực tế text dài nhất có thể — "3600s" để tránh undercount do TextRenderer padding
                using var timerFontRef = TG.FontSemiBold(7.5f);
                var maxTimerTextSz = TextRenderer.MeasureText("3600s", timerFontRef);
                const int timerIconW = 14;
                const int timerGap   = 6;
                int tickW     = isOut ? 26 : 0;
                int timerBlockMaxW = timerIconW + maxTimerTextSz.Width + timerGap;
                int neededBw  = pad * 2 + timerBlockMaxW + timeSzMeasure.Width + tickW + 8;
                minBw = Math.Max(minBw, neededBw);
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

                        e.Graphics.DrawString(LocalizationService.Translate("Forwarded from") + " ", TG.FontRegular(8.5f), new SolidBrush(TG.TextSecondary), fwdTextX, currentY + 1);

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

                        e.Graphics.DrawString(LocalizationService.Translate("Forwarded from"), TG.FontRegular(8.5f), new SolidBrush(TG.TextSecondary), fwdTextX, currentY + 2);

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

                // Tính trước timerBlockW để dịch tx sang trái nhường chỗ cho timer
                int timerBlockW = 0;
                string? timerTextCached = null;
                if (!string.IsNullOrWhiteSpace(messageId) && _expirationService.IsTracking(messageId))
                {
                    int? remSec = _expirationService.GetRemainingSeconds(messageId);
                    if (remSec.HasValue && remSec.Value > 0)
                    {
                        timerTextCached = FormatRemainingTime(remSec.Value);
                        using var timerFontMeasure = TG.FontSemiBold(7.5f);
                        var timerSzMeasure = TextRenderer.MeasureText(timerTextCached, timerFontMeasure);
                        timerBlockW = 14 + timerSzMeasure.Width + 6; // iconW + textW + gap
                    }
                }

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
                            tickColor = TG.TextSecondary;
                            break;
                        default:
                            tickText  = "✓";
                            tickColor = TG.TextSecondary;
                            break;
                    }
                    e.Graphics.DrawString(tickText, tickFont, new SolidBrush(tickColor), tickX, ty - 1);
                }

                // 5. Self-destruct timer indicator — vẽ inline bên trái timestamp
                if (timerTextCached != null)
                {
                    using var timerFont = TG.FontSemiBold(7.5f);
                    const int timerIconW = 14;

                    // tx = điểm bắt đầu vẽ timestamp (đã đúng vị trí)
                    // timer nằm NGAY BÊN TRÁI tx — không trừ timerBlockW lần nữa
                    int timerX = (int)tx - timerBlockW;
                    timerX = Math.Max(x + pad, timerX);
                    int timerY = (int)ty;

                    using var iconFont = new Font("Segoe UI Symbol", 7.5f);
                    TextRenderer.DrawText(e.Graphics, "⏱", iconFont,
                        new Point(timerX, timerY),
                        Color.FromArgb(220, 87, 34),
                        TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, timerTextCached, timerFont,
                        new Point(timerX + timerIconW, timerY),
                        Color.FromArgb(220, 87, 34),
                        TextFormatFlags.NoPadding);
                }
            };

            // Tạo Context Menu
            var actions = new SecureChat.Client.Forms.Chat.MessageActions
            {
                Reply = id => OnReplyMessage(id),
                Forward = id => OnForwardMessage(id),
                Copy = id => OnCopyMessage(id),
                Edit = isOut ? (id => OnEditMessage(id)) : null,
                Recall = isOut ? (id => OnRecallMessage(id)) : null,
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
                BackColor = TG.SidebarBg,
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

            // Cancel button
            var btnCancel = new Button
            {
                Text = LocalizationService.Translate("Cancel"),
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

            using var dlg = new SecureChat.Client.Forms.Chat.frmForwardMessage(_convs, _activeConvId, _savedMessagesConvId);
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
                    MessageBox.Show(this, LocalizationService.Translate("Cannot get encryption key for target conversation."), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var encryptionService = new MessageEncryptionService();
                if (!encryptionService.ValidateConversationKey(conversationKey))
                {
                    MessageBox.Show(this, LocalizationService.Translate("Invalid encryption key."), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show(this, string.Format(LocalizationService.Translate("Forward error: {0}"), err), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Mark message ID as processed so SignalR handler does not create duplicate
                // Only needed when forwarding into the currently open conversation (already added manually to _currentMsgs below).
                // When forwarding to another conversation, let SignalR or sync handle adding to _allMsgs.
                if (targetConversationId == _activeConvId)
                {
                    lock (_processedMessageIdsLock)
                    {
                        _processedMessageIds.Add(messageResponse.MessageID);
                    }
                }

                // Broadcast via SignalR so other members receive realtime
                if (_signalRClient is not null && _signalRClient.IsConnected)
                {
                    try
                    {
                        await _signalRClient.SendMessageAsync(targetConversationId, messageResponse);
                    }
                    catch { /* Real-time error is not critical */ }
                }

                string timeStr = messageResponse.SentAt.ToLocalTime().ToString("HH:mm");

                // Save forward metadata for rendering header
                _forwardMetadata[messageResponse.MessageID] = senderName;
                if (!string.IsNullOrEmpty(originalSenderId))
                    _forwardOriginalSenderId[messageResponse.MessageID] = originalSenderId;

                // If currently in the target conversation, add to UI immediately
                if (targetConversationId == _activeConvId)
                {
                    _messageDates[messageResponse.MessageID] = messageResponse.SentAt.ToLocalTime();
                    _currentMsgs.Add((messageResponse.MessageID, forwardText, true, timeStr, ""));
                    BuildMessages();
                }

                // Update sidebar for target conversation (preview + reorder)
                RefreshConversationItem(targetConversationId, forwardText, true, "", timeStr, messageId: messageResponse.MessageID);

                MessageBox.Show(this, LocalizationService.Translate("Message forwarded successfully!"), LocalizationService.Translate("Success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(LocalizationService.Translate("Forward error: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private async void OnEditMessage(string messageId)
        {
            var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
            if (msgIndex < 0) return;

            var msg = _currentMsgs[msgIndex];
            if (!msg.Out) return;

            if (TryShowEditDialog(ExtractActualText(msg.Text), out var newText))
            {
                string finalNewText = newText;
                if (msg.Text.StartsWith("reply::"))
                {
                    var parts = msg.Text.Substring(7).Split(new[] { "::" }, 3, StringSplitOptions.None);
                    if (parts.Length == 3) finalNewText = $"reply::{parts[0]}::{parts[1]}::{newText}";
                }

                try
                {
                    // Encrypt the new content
                    byte[]? conversationKey = await _decryptor.EnsureConversationKeyAsync(_activeConvId);
                    if (conversationKey is null)
                        throw new InvalidOperationException("Cannot get encryption key for this conversation.");

                    var encryptionService = new MessageEncryptionService();
                    var (encryptedContent, contentIV) = encryptionService.EncryptMessage(finalNewText, conversationKey);

                    // Persist edit to server
                    var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                    var editReq = new SecureChat.DTOs.EditMessageRequest(encryptedContent, contentIV);
                    var json = System.Text.Json.JsonSerializer.Serialize(editReq);
                    var reqContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var res = await http.PatchAsync($"api/conversations/{_activeConvId}/messages/{messageId}", reqContent);
                    if (!res.IsSuccessStatusCode)
                    {
                        var err = await res.Content.ReadAsStringAsync();
                        MessageBox.Show(this, $"Edit error: {err}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Update local state on success; server broadcasts to group
                    _currentMsgs[msgIndex] = (msg.Id, finalNewText, msg.Out, msg.Time, msg.Sender);
                    BuildMessages();
                    UpdatePinnedBar();
                    RefreshSidebarPreview();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Edit error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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
                    MessageBox.Show(this, string.Format(LocalizationService.Translate("Recall error: {0}"), err), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    _messageDates.Remove(messageId);
    _forwardMetadata.TryRemove(messageId, out _);
    _forwardOriginalSenderId.TryRemove(messageId, out _);
    _pinnedByMap.TryRemove(messageId, out _);
    if (_pinnedMessageIds.Remove(messageId))
        UpdatePinnedBar();
    BuildMessages();
            RefreshSidebarPreview();
        }

        private async void OnRecallMessage(string messageId)
        {
            try
            {
                var msgIndex = _currentMsgs.FindIndex(m => m.Id == messageId);
                if (msgIndex < 0) return;

                var dialog = ShowTelegramDialog(LocalizationService.Translate("Recall message?"), LocalizationService.Translate("Recall for everyone"), LocalizationService.Translate("Recall"), Color.FromArgb(0xE2, 0x4B, 0x4A));
                if (dialog.Result != DialogResult.Yes) return;

                var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                var res = await http.PostAsync($"api/conversations/{_activeConvId}/messages/{messageId}/recall", null);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    MessageBox.Show(this, string.Format(LocalizationService.Translate("Recall error: {0}"), err), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Update local state
                _currentMsgs[msgIndex] = (messageId, "recalled::", _currentMsgs[msgIndex].Out, _currentMsgs[msgIndex].Time, _currentMsgs[msgIndex].Sender);

                // Unpin if pinned
                if (_pinnedMessageIds.Remove(messageId))
                {
                    _pinnedByMap.TryRemove(messageId, out _);
                    UpdatePinnedBar();
                }

                // Clean up associated metadata (keep _messageDates for date grouping)
                _forwardMetadata.TryRemove(messageId, out _);
                _forwardOriginalSenderId.TryRemove(messageId, out _);

                // Broadcast recall to other members via SignalR
                if (_signalRClient is not null && _signalRClient.IsConnected)
                {
                    try
                    {
                        await _signalRClient.RecallMessageAsync(_activeConvId, messageId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Recall] SignalR broadcast failed: {ex.Message}");
                    }
                }
                else if (_signalRClient is not null && !_signalRClient.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("[Recall] SignalR not connected — broadcast skipped. Other clients will see recall after refresh.");
                }

                BuildMessages();
                RefreshSidebarPreview();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Recall] OnRecallMessage failed: {ex.Message}");
                MessageBox.Show(this, string.Format(LocalizationService.Translate("Recall error: {0}"), ex.Message), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                _pinnedByMap.Clear();
                foreach (var p in pins)
                {
                    _pinnedMessageIds.Add(p.MessageID);
                    if (!string.IsNullOrWhiteSpace(p.PinnedByName))
                        _pinnedByMap[p.MessageID] = p.PinnedByName;
                }

                if (_activeConvId == convId)
                    UpdatePinnedBar();
            }
            catch { /* silently ignore */ }
        }

        private async void OnPinMessage(string messageId)
        {
            if (_pinnedMessageIds.Count >= 3)
            {
                SafeBeginInvoke(() =>
                    MessageBox.Show(this, LocalizationService.Translate("Maximum 3 messages can be pinned."), LocalizationService.Translate("Limit"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
                return;
            }

            var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
            var res = await http.PostAsync(
                $"api/conversations/{_activeConvId}/messages/{messageId}/pin",
                null);

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync();
                SafeBeginInvoke(() =>
                    MessageBox.Show(this, string.Format(LocalizationService.Translate("Pin error: {0}"), err), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
                return;
            }

            _pinnedMessageIds.Add(messageId);
            _pinnedByMap[messageId] = _currentDisplayName;
            UpdatePinnedBar();
            BuildMessages();

            // Broadcast pin to other members via SignalR (identity resolved server-side)
            if (_signalRClient is not null && _signalRClient.IsConnected)
            {
                try
                {
                    await _signalRClient.PinMessageAsync(_activeConvId, messageId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Pin] SignalR broadcast failed: {ex.Message}");
                }
            }
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
                SafeBeginInvoke(() =>
                    MessageBox.Show(this, string.Format(LocalizationService.Translate("Unpin error: {0}"), err), LocalizationService.Translate("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error));
                return;
            }

            _pinnedMessageIds.Remove(messageId);
            _pinnedByMap.TryRemove(messageId, out _);
            UpdatePinnedBar();
            BuildMessages();

            // Broadcast unpin to other members via SignalR
            if (_signalRClient is not null && _signalRClient.IsConnected)
            {
                try
                {
                    await _signalRClient.UnpinMessageAsync(_activeConvId, messageId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Unpin] SignalR broadcast failed: {ex.Message}");
                }
            }
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
            string pinnedByName = LocalizationService.Translate("Unknown");
            if (_pinnedByMap.TryGetValue(firstId, out var pn) && !string.IsNullOrWhiteSpace(pn))
                pinnedByName = pn;
            _lblPinnedText.Text = count == 1
                ? string.Format(LocalizationService.Translate("Pin: {0}"), truncatedPreview)
                : string.Format(LocalizationService.Translate("{0} pins: {1}"), count, truncatedPreview);

            _lblPinnedBottomText.Text = count == 1
                ? string.Format(LocalizationService.Translate("Pinned by {0}"), pinnedByName)
                : string.Format(LocalizationService.Translate("{0} pinned messages"), count);

            _pnlPinnedBar.Visible = true;
            _pnlPinnedBottomBar.Visible = true;

            // Rebuild popup if open
            if (_isPinnedPopupOpen)
                RebuildPinnedPopup();
        }

        private string GetPinnedDisplayText(string? text)
        {
            if (text is null) return LocalizationService.Translate("Media");
            if (text.StartsWith("recalled::")) return LocalizationService.Translate("Recalled message");
            string cleaned = ExtractActualText(text);
            if (cleaned.StartsWith("voice::")) return "📞 " + LocalizationService.Translate("Voice message");
            if (cleaned.StartsWith("file::")) return "📎 " + LocalizationService.Translate("File");
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

            int itemH = 44; // 2px top + 20px (tên) + 22px (nội dung) = 44, trước là 40 → bị clip 4px

            foreach (var pid in _pinnedMessageIds)
            {
                var msg = _currentMsgs.Find(m => m.Id == pid);
                string displayText;
                string pinnedByName = LocalizationService.Translate("Unknown");

                if (msg.Id is not null)
                {
                    displayText = GetPinnedDisplayText(msg.Text);
                    if (_pinnedByMap.TryGetValue(pid, out var pn) && !string.IsNullOrWhiteSpace(pn))
                        pinnedByName = pn;
                    else if (msg.Out)
                        pinnedByName = LocalizationService.Translate("You");
                    else if (!string.IsNullOrEmpty(msg.Sender))
                    {
                        if (_senderDisplayNameMap.TryGetValue(msg.Sender, out var dn) && !string.IsNullOrEmpty(dn))
                            pinnedByName = dn;
                        else pinnedByName = msg.Sender;
                    }
                }
                else
                {
                    displayText = LocalizationService.Translate("Pinned message");
                }

                // Item panel
                var item = new Panel
                {
                    Height = itemH,
                    Dock = DockStyle.Top,
                    BackColor = TG.SidebarBg,
                    Cursor = Cursors.Hand,
                    Tag = pid,
                };

                // Hover highlight
                item.MouseEnter += (_, __) => item.BackColor = TG.SidebarHover;
                item.MouseLeave += (_, __) => item.BackColor = TG.SidebarBg;

                // Sender name
                int itemTextW = Math.Max(50, _pnlPinnedBar.Width - 56);
                var lblSender = new Label
                {
                    Text = TruncateText(string.Format(LocalizationService.Translate("Pinned by {0}"), pinnedByName), 30),
                    Font = TG.FontSemiBold(9f),
                    ForeColor = TG.Blue,
                    AutoSize = false,
                    Height = 20,
                    Width = itemTextW,
                    Location = new Point(8, 2),
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
                    Height = 22,
                    Width = itemTextW,
                    Location = new Point(8, 22),
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleLeft,
                };

                // Unpin button per item
                var btnItemUnpin = new Button
                {
                    Text = "✕",
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(28, 28),
                    Location = new Point(_pnlPinnedBar.Width - 32, (itemH - 28) / 2),
                    Cursor = Cursors.Hand,
                    ForeColor = TG.TextSecondary,
                    Font = new Font("Segoe UI Symbol", 9f),
                    TextAlign = ContentAlignment.MiddleCenter,
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
            _signalRClient.MessageRecalled += HandleSignalRRecalledAsync;
            _signalRClient.MessageEdited += HandleSignalREditedAsync;
            _signalRClient.MessageDeleted += HandleSignalRDeletedAsync;
            _signalRClient.MessageStatusUpdated += HandleMessageStatusUpdatedAsync;
            _signalRClient.CallSignalReceived += HandleSignalRCallSignalAsync;
            _signalRClient.CallIncoming += HandleCallIncomingAsync;
            _signalRClient.CallMissed += HandleCallMissedAsync;
            _signalRClient.UserTyping += HandleUserTypingAsync;
            _signalRClient.UserStoppedTyping += HandleUserStoppedTypingAsync;
            _signalRClient.ConversationCreated += async convId =>
            {
                var (ok, conv, _) = await _messageService.GetConversationAsync(convId);
                if (!ok || conv is null) return;
                // Saved Messages is auto-created; no notification needed
                if (conv.Type == ConversationType.SavedMessages)
                {
                    _savedMessagesConvId = convId;
                    return;
                }
                // Join the SignalR group immediately so realtime messages arrive
                await _signalRClient.JoinConversationAsync(convId);
                SafeBeginInvoke(() =>
                {
                    if (_convs.Any(c => c.Id == convId)) return;
                    bool isGroup = conv.Type == ConversationType.Group;
                    string display = !string.IsNullOrWhiteSpace(conv.Name)
                        ? conv.Name!
                        : (isGroup ? "Group" : "Direct chat");
                    string time = conv.LastActivityAt?.ToLocalTime().ToString("HH:mm") ?? string.Empty;
                    _convs.Insert(0, (convId, display, string.Empty, time, 0, isGroup));

                    if (!isGroup && !string.IsNullOrWhiteSpace(conv.OtherUserId))
                        _convOtherUserId[convId] = conv.OtherUserId;

                    InsertConvRowAtTop(convId);
                    if (!string.IsNullOrWhiteSpace(conv.AvatarURL))
                        _ = RefreshAvatarForConversationAsync(convId, conv.AvatarURL);

                    var s = NotificationSettings.Default;
                    if (isGroup && !s.GroupNotifications) return;
                    if (!isGroup && !s.PrivateChatNotifications) return;
                    if (s.DesktopNotifications)
                        NotificationManager.ShowDesktopNotification(display, "New conversation created");
                    if (s.FlashTaskbar)
                        NotificationManager.FlashWindow(this.Handle);
                    if (s.AllowSound)
                        NotificationManager.PlayNotificationSound(s.Volume);
                });
            };
            _signalRClient.ProfileUpdated += async (userId, displayName, username, avatarUrl) =>
            {
                string capturedUrl = avatarUrl;
                string capturedUsername = username;
                string capturedUserId = userId;
                SafeBeginInvoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(username))
                    {
                        _senderDisplayNameMap[username] = displayName;
                    }
                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        _senderAvatarMap[username] = capturedUrl ?? string.Empty;
                    }
                    if (userId == _currentUserId)
                    {
                        _currentDisplayName = displayName ?? string.Empty;
                        _currentUsername = username ?? string.Empty;
                        _currentAvatarUrl = avatarUrl ?? string.Empty;
                        _decryptor.CurrentUsername = username ?? string.Empty;
                        SecureChat.Client.Services.AvatarService.UpdateAvatar(avatarUrl);
                        SecureChat.Client.Services.AvatarService.UpdateProfile(
                            displayName ?? string.Empty, username ?? string.Empty, string.Empty);
                        UpdateSettingsHeaderUI();

                        // Refresh Saved Messages row in sidebar
                        if (!string.IsNullOrWhiteSpace(_savedMessagesConvId))
                        {
                            for (int i = 0; i < _convs.Count; i++)
                            {
                                var c = _convs[i];
                                if (c.Id == _savedMessagesConvId)
                                {
                                    _convs[i] = (c.Id, "Saved Messages", c.Preview, c.Time, c.Unread, c.IsGroup);
                                    _ = RefreshAvatarForConversationAsync(c.Id, _currentAvatarUrl);
                                    break;
                                }
                            }
                        }
                    }
                    // If this is a profile update for another user in a direct conversation,
                    // update the conversation name and avatar using userId-based lookup
                    if (!string.IsNullOrWhiteSpace(capturedUserId))
                    {
                        string newName = displayName ?? capturedUsername;

                        for (int i = 0; i < _convs.Count; i++)
                        {
                            var c = _convs[i];
                            if (c.IsGroup) continue;

                            if (_convOtherUserId.TryGetValue(c.Id, out var otherId) && otherId == capturedUserId)
                            {
                                _convs[i] = (c.Id, newName, c.Preview, c.Time, c.Unread, c.IsGroup);

                                if (_convRowCache.TryGetValue(c.Id, out var row))
                                {
                                    foreach (Control ctrl in row.Controls)
                                    {
                                        if (ctrl is Label lbl && lbl.Location.Y == 10)
                                            lbl.Text = newName;
                                        else if (ctrl is AvatarControl av)
                                            av.SetName(newName);
                                    }
                                }

                                _ = RefreshAvatarForConversationAsync(c.Id, capturedUrl);
                            }
                        }
                    }
                    // Update active conversation header if it's a DM with the changed user
                    if (!string.IsNullOrWhiteSpace(_activeConvId) && !string.IsNullOrWhiteSpace(capturedUserId))
                    {
                        var activeConv = _convs.Find(c => c.Id == _activeConvId);
                        if (activeConv != default && !activeConv.IsGroup)
                        {
                            string activeOtherId = _convOtherUserId.TryGetValue(_activeConvId, out var oid) ? oid : "";
                            if (activeOtherId == capturedUserId)
                            {
                                if (!string.IsNullOrWhiteSpace(displayName))
                                {
                                    _lblChatName.Text = displayName;
                                    _chatAvatar.SetName(displayName);
                                }
                                else if (!string.IsNullOrWhiteSpace(capturedUsername))
                                {
                                    _lblChatName.Text = capturedUsername;
                                    _chatAvatar.SetName(capturedUsername);
                                }
                                _ = RefreshAvatarForConversationAsync(_activeConvId, capturedUrl);
                            }
                        }
                    }
                    // Refresh sidebar previews in case display name changed
                    RefreshAllSidebarPreviews();
                    // Rebuild messages if sender display name changed in active conversation
                    if (!string.IsNullOrWhiteSpace(_activeConvId) && !string.IsNullOrWhiteSpace(capturedUsername))
                    {
                        var activeConv = _convs.Find(c => c.Id == _activeConvId);
                        if (activeConv != default)
                        {
                            bool needsRebuild = false;
                            foreach (var msg in _currentMsgs)
                            {
                                if (msg.Sender == capturedUsername)
                                {
                                    needsRebuild = true;
                                    break;
                                }
                            }
                            if (needsRebuild)
                                BuildMessages();
                        }
                    }
                    // Refresh right sidebar if open
                    if (_isSidebarOpen)
                        _ = LoadRightSidebarContentAsync();
                });
            };
            _signalRClient.ConversationDeleted += async convId =>
            {
                SafeBeginInvoke(() =>
                {
                    if (_convs.RemoveAll(c => c.Id == convId) > 0)
                    {
                        _allMsgs.TryRemove(convId, out _);
                        _syncedConversations.Remove(convId);
                        _myMemberIdByConv.TryRemove(convId, out _);
                        if (_convAvatarCache.TryGetValue(convId, out var oldImg))
                        {
                            oldImg?.Dispose();
                            _convAvatarCache.Remove(convId);
                        }
                        else
                            _convAvatarCache.Remove(convId);

                        if (_activeConvId == convId)
                        {
                            if (_convs.Count > 0)
                            {
                                var first = _convs.FirstOrDefault(c => !string.IsNullOrWhiteSpace(_savedMessagesConvId) && c.Id != _savedMessagesConvId);
                                _activeConvId = first.Id ?? _convs[0].Id;
                            }
                            else
                            {
                                _activeConvId = string.Empty;
                            }
                            if (string.IsNullOrEmpty(_activeConvId))
                                UpdateChatEmptyStateUI();
                            else
                                LoadConversation(_activeConvId);
                        }
                        RemoveConvRow(convId);
                        if (!string.IsNullOrWhiteSpace(_activeConvId))
                            UpdateConvRowHighlight(_activeConvId);
                    }
                });
            };
            _signalRClient.ConversationUpdated += async (convId, version) =>
            {
                if (_convVersions.TryGetValue(convId, out var localV) && version < localV)
                    return;
                _convVersions[convId] = version;

                var (ok, conv, _) = await _messageService.GetConversationAsync(convId);
                if (!ok || conv is null) return;
                SafeBeginInvoke(() =>
                {
                    var idx = _convs.FindIndex(c => c.Id == convId);
                    if (idx < 0) return;
                    var old = _convs[idx];
                    string display = !string.IsNullOrWhiteSpace(conv.Name) ? conv.Name! : (old.IsGroup ? "Group" : "Conversation");
                    _convs[idx] = (convId, display, old.Preview, old.Time, old.Unread, old.IsGroup);

                    if (!old.IsGroup && !string.IsNullOrWhiteSpace(conv.OtherUserId))
                        _convOtherUserId[convId] = conv.OtherUserId;

                    if (!string.IsNullOrWhiteSpace(conv.AvatarURL))
                    {
                        _ = RefreshAvatarForConversationAsync(convId, conv.AvatarURL);
                    }

                    if (_activeConvId == convId)
                    {
                        _lblChatName.Text = display;
                        _chatAvatar.SetName(display);
                        if (!old.IsGroup && _convOtherUserId.TryGetValue(_activeConvId, out var otherId))
                            _ = _signalRClient?.QueryUserPresenceAsync(otherId);
                    }
                    UpdateConvRowName(convId, display);
                });
            };
            _signalRClient.GroupSettingsUpdated += async (convId, version) =>
            {
                if (_convVersions.TryGetValue(convId, out var localV) && version < localV)
                    return;
                _convVersions[convId] = version;

                var (ok, conv, _) = await _messageService.GetConversationAsync(convId);
                if (!ok || conv is null) return;
                SafeBeginInvoke(() =>
                {
                    var idx = _convs.FindIndex(c => c.Id == convId);
                    if (idx < 0) return;
                    var old = _convs[idx];
                    string display = !string.IsNullOrWhiteSpace(conv.Name) ? conv.Name! : (old.IsGroup ? "Group" : "Conversation");
                    _convs[idx] = (convId, display, old.Preview, old.Time, old.Unread, old.IsGroup);
                    if (_activeConvId == convId)
                        _lblChatName.Text = display;
                    UpdateConvRowName(convId, display);
                });
            };
            _signalRClient.MessagesCleared += async convId =>
            {
                SafeBeginInvoke(() =>
                {
                    // Always remove cached messages so this conversation re-fetches from server
                    _allMsgs.TryRemove(convId, out _);
                    _syncedConversations.Remove(convId);

                    // Only refresh UI state when this conversation is currently visible
                    if (_activeConvId == convId)
                    {
                        _pinnedMessageIds.Clear();
                        _pinnedByMap.Clear();
                        UpdatePinnedBar();
                        LoadConversation(_activeConvId);
                    }

                    RefreshConversationItem(convId, string.Empty, true, string.Empty, string.Empty);
                });
            };
            _signalRClient.MemberAdded += async (convId, userId) =>
            {
                // Fetch and cache the conversation if we don't have it yet
                var (ok, conv, _) = await _messageService.GetConversationAsync(convId);
                if (!ok || conv is null) return;
                SafeBeginInvoke(() =>
                {
                    var idx = _convs.FindIndex(c => c.Id == convId);
                    if (idx < 0) return;
                    var old = _convs[idx];
                    _convs[idx] = (convId, old.Name, old.Preview, old.Time, old.Unread, old.IsGroup);

                    // Refresh right sidebar if open and showing this conversation
                    if (_isSidebarOpen && _activeConvId == convId)
                        _ = LoadRightSidebarContentAsync();

                    // Skip notification if the member is ourselves
                    if (userId == _currentUserId || (!string.IsNullOrWhiteSpace(_currentUsername) && userId == _currentUsername))
                        return;

                    var s = NotificationSettings.Default;
                    if (!s.ContactJoinedNotifications)
                        return;
                    if (convId == _activeConvId)
                        return;

                    if (s.DesktopNotifications)
                        NotificationManager.ShowDesktopNotification(old.Name, LocalizationService.Translate("New member joined"));
                    if (s.FlashTaskbar)
                        NotificationManager.FlashWindow(this.Handle);
                    if (s.AllowSound)
                        NotificationManager.PlayNotificationSound(s.Volume);
                });
            };
            _signalRClient.MemberRemoved += async (convId, userId) =>
            {
                SafeBeginInvoke(() =>
                {
                    // Refresh right sidebar if open and showing this conversation
                    if (_isSidebarOpen && _activeConvId == convId)
                        _ = LoadRightSidebarContentAsync();
                });
            };
            _signalRClient.UserStatusChanged += async (userId, status, lastSeenUtc) =>
            {
                _userPresence[userId] = (status == "Online", lastSeenUtc);
                GlobalPresence[userId] = (status == "Online", lastSeenUtc);
                GlobalUserStatusChanged?.Invoke(userId, status, lastSeenUtc);

                SafeBeginInvoke(() =>
                {
                    // Update sidebar avatar for affected conversation
                    foreach (Control c in _pnlConvList.Controls)
                    {
                        if (c.Tag is string convId
                            && _convOtherUserId.TryGetValue(convId, out var otherId)
                            && otherId == userId)
                        {
                            var avatar = c.Controls.OfType<AvatarControl>().FirstOrDefault();
                            if (avatar != null)
                            {
                                avatar.ShowOnline = status == "Online";
                                avatar.Invalidate();
                            }
                            break;
                        }
                    }

                    // Update chat header if this is the active conversation partner
                    if (!string.IsNullOrWhiteSpace(_activeConvId))
                    {
                        var conv = _convs.Find(c => c.Id == _activeConvId);
                        if (conv != default && !conv.IsGroup
                            && _convOtherUserId.TryGetValue(_activeConvId, out var activeOtherId)
                            && activeOtherId == userId)
                        {
                            RestoreChatStatus();
                            if (_isSidebarOpen)
                                _ = LoadRightSidebarContentAsync();
                        }
                        else if (conv != default && conv.IsGroup && _isSidebarOpen)
                        {
                            _ = LoadRightSidebarContentAsync();
                        }
                    }
                });
            };
            _signalRClient.MessagePinned += async (convId, messageId, pinnedByUserId, pinnedByName) =>
            {
                SafeBeginInvoke(() =>
                {
                    if (!_pinnedMessageIds.Add(messageId))
                        return;

                    if (!string.IsNullOrWhiteSpace(pinnedByName))
                        _pinnedByMap[messageId] = pinnedByName;
                    UpdatePinnedBar();
                    if (_activeConvId == convId)
                        BuildMessages();

                    var s = NotificationSettings.Default;
                    if (!s.PinnedMessageNotifications)
                        return;
                    if (convId == _activeConvId)
                        return;

                    int idx = _convs.FindIndex(c => c.Id == convId);
                    if (idx < 0) return;

                    string convName = _convs[idx].Name;
                    string byName = !string.IsNullOrWhiteSpace(pinnedByName) ? pinnedByName : LocalizationService.Translate("Someone");
                    if (s.DesktopNotifications)
                        NotificationManager.ShowDesktopNotification(convName, string.Format(LocalizationService.Translate("Pinned a message by {0}"), byName));
                    if (s.FlashTaskbar)
                        NotificationManager.FlashWindow(this.Handle);
                    if (s.AllowSound)
                        NotificationManager.PlayNotificationSound(s.Volume);
                });
            };
            _signalRClient.MessageUnpinned += async (convId, messageId) =>
            {
                SafeBeginInvoke(() =>
                {
                    if (_pinnedMessageIds.Remove(messageId))
                    {
                        UpdatePinnedBar();
                        if (_activeConvId == convId)
                            BuildMessages();
                    }
                });
            };
            _signalRClient.Closed += async _ =>
            {
                if (_isLoggingOut) return;
                SafeBeginInvoke(() =>
                {
                    MessageBox.Show(this, LocalizationService.Translate("Connection to server lost. Please re-login."), LocalizationService.Translate("Disconnected"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            };
            _signalRClient.Reconnecting += async _ =>
            {
                // No notification — SignalR automatic reconnect handles it silently
            };
            _signalRClient.Reconnected += async _ =>
            {
                await ReRegisterPublicKeyAsync();

                // Rejoin ALL conversation groups — reconnect does NOT re-trigger
                // OnConnectedAsync, so group membership is lost after reconnect
                var allConvs = _convs.ToList();
                foreach (var c in allConvs)
                {
                    try { await _signalRClient.JoinConversationAsync(c.Id); } catch { /* best-effort per conversation */ }
                }

                // Force re-sync ALL conversations — clear sync guards so
                // SyncMessagesForActiveConversationAsync re-fetches from API
                _syncedConversations.Clear();
                lock (_processedMessageIdsLock)
                {
                    _processedMessageIds.Clear();
                }

                // Full message sync for active conversation only
                if (!string.IsNullOrWhiteSpace(_activeConvId))
                {
                    await SyncMessagesForActiveConversationAsync(_activeConvId);
                }

                // Metadata refresh for ALL conversations (preview, unread, sidebar order).
                // Internally calls LoadConversation for the active conversation, which
                // triggers mark-read, delivery-status refresh, and pin reload.
                await SyncConversationsAsync();

                SafeBeginInvoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(_activeConvId))
                    {
                        var conv = _convs.Find(c => c.Id == _activeConvId);
                        if (conv != default && !conv.IsGroup && _convOtherUserId.TryGetValue(_activeConvId, out var otherId))
                        {
                            var _q = _signalRClient.QueryUserPresenceAsync(otherId);
                        }
                    }

                    // Re-query presence for all sidebar conversations
                    foreach (var kvp in _convOtherUserId)
                    {
                        var _p = _signalRClient.QueryUserPresenceAsync(kvp.Value);
                    }

                    if (_isSidebarOpen)
                    {
                        var _s = LoadRightSidebarContentAsync();
                    }
                });
            };

            try
            {
                await _signalRClient.StartAsync();
                if (!string.IsNullOrWhiteSpace(_activeConvId))
                    await _signalRClient.JoinConversationAsync(_activeConvId);
                // Query presence for all known conversation partners on initial connect
                foreach (var kvp in _convOtherUserId)
                {
                    var _p = _signalRClient.QueryUserPresenceAsync(kvp.Value);
                }
            }
            catch (Exception ex)
            {
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("SignalR"), MessageBoxButtons.OK, MessageBoxIcon.Error));
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
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("SignalR"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
            }
        }

        private void SafeBeginInvoke(Action action)
        {
            if (_isClosing || IsDisposed) return;
            BeginInvoke(new Action(() =>
            {
                if (_isClosing || IsDisposed) return;
                action();
            }));
        }

        private static List<string> ParseMentions(string text, string currentUsername)
        {
            var mentions = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(currentUsername))
                return mentions;

            int idx = text.IndexOf($"@{currentUsername}", StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                // Verify it's a word boundary (space, start of string, or punctuation before @)
                if (idx == 0 || char.IsWhiteSpace(text[idx - 1]) || char.IsPunctuation(text[idx - 1]))
                {
                    mentions.Add(currentUsername);
                    break;
                }
                idx = text.IndexOf($"@{currentUsername}", idx + 1, StringComparison.OrdinalIgnoreCase);
            }

            return mentions;
        }

        private bool IsConversationMuted(string convId)
        {
            if (string.IsNullOrWhiteSpace(convId)) return false;
            if (_convMuteUntil.TryGetValue(convId, out var until))
            {
                if (until.HasValue && until.Value > DateTime.UtcNow)
                    return true;
                if (!until.HasValue) // muted forever
                    return true;
                // expired mute — clean up
                _convMuteUntil.Remove(convId);
            }
            return false;
        }

        private bool IsCurrentUserMentioned(MessageResponse message)
        {
            if (message.MentionedMemberIDs is null || message.MentionedMemberIDs.Count == 0)
                return false;
            if (_myMemberIdByConv.TryGetValue(message.ConversationID, out var myMemberId)
                && !string.IsNullOrWhiteSpace(myMemberId))
            {
                return message.MentionedMemberIDs.Contains(myMemberId);
            }
            return false;
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

                SafeBeginInvoke(() =>
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
                        var msgTime = message.SentAt.ToLocalTime();
                        _messageDates[dm.Id] = msgTime;

                        // Insert in chronological order (oldest first)
                        int insertIdx = list.Count;
                        for (int i = list.Count - 1; i >= 0; i--)
                        {
                            if (_messageDates.TryGetValue(list[i].Id, out var existingTime) && existingTime <= msgTime)
                            {
                                insertIdx = i + 1;
                                break;
                            }
                        }
                        list.Insert(insertIdx, (dm.Id, dm.Text, dm.Out, dm.Time, dm.Sender));

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
                            UpdateTitleBar();
                        }

                        if (message.ConversationID == _activeConvId)
                        {
                            BuildMessages();

                            // Tin đến khi đang mở conversation → mark read ngay lập tức
                            if (!dm.Out)
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var http = SecureChat.Client.Services.ApiClient.Instance.GetHttpClient();
                                        await http.PostAsync(
                                            $"api/conversations/{message.ConversationID}/messages/{message.MessageID}/read",
                                            null);
                                    }
                                    catch { }
                                });
                            }
                        }

                        // For call-type system messages, skip regular notifications
                        bool isCallMessage = dm.Raw.Type == MessageType.Call;
                        bool isMention = !dm.Out && IsCurrentUserMentioned(message);
                        string convIdForMute = message.ConversationID;

                // Skip notification for saved messages (self-messages)
                if (!string.IsNullOrWhiteSpace(_savedMessagesConvId) && message.ConversationID == _savedMessagesConvId)
                    return;

                if (!dm.Out && idx >= 0 && message.ConversationID != _activeConvId && !isCallMessage)
                {
                    // Skip if conversation is muted
                    if (IsConversationMuted(convIdForMute))
                        return;

                            var conv2 = _convs[idx];
                            var s = NotificationSettings.Default;
                            bool convCategoryOk = conv2.IsGroup ? s.GroupNotifications : s.PrivateChatNotifications;

                            if (convCategoryOk)
                            {
                                string mentionPrefix = isMention ? "📌 " : "";

                                if (s.DesktopNotifications)
                                {
                                    string convName = conv2.Name;
                                    string senderName = (string.IsNullOrEmpty(dm.Sender) ? "" : dm.Sender);
                                    if (!string.IsNullOrEmpty(dm.Sender) && _senderDisplayNameMap.TryGetValue(dm.Sender, out var dn) && !string.IsNullOrEmpty(dn))
                                        senderName = dn;
                                    string title = isMention ? string.Format(LocalizationService.Translate("Mention in {0}"), convName) : convName;
                                    string preview = dm.Text.Length > 100 ? dm.Text[..100] + "..." : dm.Text;
                                    NotificationManager.ShowDesktopNotification(title, $"{mentionPrefix}{senderName}: {preview}");
                                }
                                if (s.FlashTaskbar)
                                    NotificationManager.FlashWindow(this.Handle);
                                if (s.AllowSound)
                                    NotificationManager.PlayNotificationSound(s.Volume);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SignalR] UI update failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] HandleSignalRMessageAsync failed: {ex.Message}");
            }
        }

        private async Task HandleSignalRRecalledAsync(MessageResponse message)
        {
            try
            {
                SafeBeginInvoke(() =>
                {
                    try
                    {
                        // Update in _allMsgs for the recalled message's conversation
                        if (_allMsgs.TryGetValue(message.ConversationID, out var list))
                        {
                            int idx = list.FindIndex(m => m.Id == message.MessageID);
                            if (idx >= 0)
                            {
                                var old = list[idx];
                                list[idx] = (old.Id, "recalled::", old.Out, old.Time, old.Sender);
                            }
                        }

                        // Unpin if pinned
                        if (_pinnedMessageIds.Remove(message.MessageID))
                        {
                            _pinnedByMap.TryRemove(message.MessageID, out _);
                            UpdatePinnedBar();
                        }

                        // Clean up associated metadata (keep _messageDates for date grouping)
                        _forwardMetadata.TryRemove(message.MessageID, out _);
                        _forwardOriginalSenderId.TryRemove(message.MessageID, out _);

                        // Update sidebar preview
                        int convIdx = _convs.FindIndex(c => c.Id == message.ConversationID);
                        if (convIdx >= 0)
                        {
                            var c = _convs[convIdx];
                            RefreshConversationItem(message.ConversationID, "recalled::", false, "", DateTime.Now.ToLocalTime().ToString("HH:mm"), c.Unread);
                        }

                        // Re-render if this is the active conversation
                        if (message.ConversationID == _activeConvId)
                            BuildMessages();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SignalR] Recall UI update failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] HandleSignalRRecalledAsync failed: {ex.Message}");
            }
        }

        private async Task HandleSignalREditedAsync(string conversationId, MessageResponse message)
        {
            try
            {
                // Decrypt before dispatching to UI thread to avoid deadlock
                var dm = await _decryptor.ProcessAsync(message, null);

                SafeBeginInvoke(() =>
                {
                    try
                    {
                        if (!_allMsgs.TryGetValue(conversationId, out var list))
                            return;

                        var idx = list.FindIndex(m => m.Id == message.MessageID);
                        if (idx < 0) return;

                        var old = list[idx];
                        list[idx] = (old.Id, dm.Text, old.Out, old.Time, old.Sender);

                        if (conversationId == _activeConvId)
                            BuildMessages();

                        RefreshSidebarPreview();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SignalR] Edit UI update failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] HandleSignalREditedAsync failed: {ex.Message}");
            }
        }

        private async Task HandleSignalRDeletedAsync(string conversationId, string messageId)
        {
            try
            {
                SafeBeginInvoke(() =>
                {
                    try
                    {
                        // Remove from current message list
                        var idx = _currentMsgs.FindIndex(m => m.Id == messageId);
                        if (idx >= 0)
                        {
                            _currentMsgs.RemoveAt(idx);

                            if (conversationId == _activeConvId)
                                BuildMessages();

                            RefreshSidebarPreview();
                        }

                        // Clean up metadata regardless of active conversation
                        _messageDates.Remove(messageId);
                        _forwardMetadata.TryRemove(messageId, out _);
                        _forwardOriginalSenderId.TryRemove(messageId, out _);
                        _pinnedByMap.TryRemove(messageId, out _);
                        _pinnedMessageIds.Remove(messageId);

                        // Also remove from _allMsgs cache
                        if (_allMsgs.TryGetValue(conversationId, out var list))
                        {
                            int allIdx = list.FindIndex(m => m.Id == messageId);
                            if (allIdx >= 0)
                                list.RemoveAt(allIdx);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SignalR] Delete UI update failed: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] HandleSignalRDeletedAsync failed: {ex.Message}");
            }
        }

        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _pendingCallSignals = new();
        private readonly ConcurrentDictionary<string, byte> _activeCallIds = new();
        private readonly ConcurrentDictionary<string, Form> _activeIncomingDialogs = new();
        private bool _isCallInitiating;

        private Task HandleSignalRCallSignalAsync(string callId, string signal)
        {
            if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(signal))
                return Task.CompletedTask;

            // CALL_ENDED / CALL_REJECTED while an incoming dialog is showing — close it
            if ((signal == "CALL_ENDED" || signal == "CALL_REJECTED") && _activeIncomingDialogs.TryRemove(callId, out var dialog))
            {
                SafeBeginInvoke(() =>
                {
                    try { if (!dialog.IsDisposed) dialog.Close(); } catch { }
                });
                return Task.CompletedTask;
            }

            // If a frmVideoCall is already open for this call, it handles signals directly
            if (_activeCallIds.ContainsKey(callId))
                return Task.CompletedTask;

            var queue = _pendingCallSignals.GetOrAdd(callId, _ => new ConcurrentQueue<string>());
            queue.Enqueue(signal);
            return Task.CompletedTask;
        }

        private Task HandleCallIncomingAsync(string callId, string callerName, CallType callType, string conversationId)
        {
            if (IsDisposed) return Task.CompletedTask;

            // Prevent duplicate incoming dialogs for the same callId
            if (_activeIncomingDialogs.ContainsKey(callId))
                return Task.CompletedTask;

            // Check if caller already cancelled before we show the dialog
            if (_pendingCallSignals.TryGetValue(callId, out var existing))
            {
                if (existing.Any(s => s == "CALL_ENDED" || s == "CALL_REJECTED"))
                {
                    _pendingCallSignals.TryRemove(callId, out _);
                    _activeCallIds.TryRemove(callId, out _);
                    return Task.CompletedTask;
                }
            }

            DeviceConfig.Load();
            if (!DeviceConfig.AcceptCallsOnThisDevice)
            {
                _pendingCallSignals.TryRemove(callId, out _);
                _activeCallIds.TryRemove(callId, out _);
                _ = Task.Run(async () =>
                {
                    try { if (_signalRClient != null) await _signalRClient.SendCallSignalAsync(callId, "CALL_REJECTED"); } catch { }
                });
                return Task.CompletedTask;
            }

            var s = NotificationSettings.Default;
            if (s.FlashTaskbar)
                NotificationManager.FlashWindow(this.Handle);
            if (s.AllowSound)
                NotificationManager.PlayNotificationSound(s.Volume);

            SafeBeginInvoke(async () =>
            {
                try
                {
                    var form = new Forms.Call.frmIncomingCall(callerName, callType);
                    _activeIncomingDialogs[callId] = form;

                    // Heartbeat: poll call status every 5s while dialog is visible.
                    // Guards against orphaned dialogs when SignalR CALL_ENDED is missed
                    // (e.g. connection drop during ringing, server restart).
                    using var heartbeat = new System.Windows.Forms.Timer { Interval = 5000 };
                    heartbeat.Tick += async (s, e) =>
                    {
                        if (form.IsDisposed) { heartbeat.Stop(); return; }
                        try
                        {
                            var hc = ApiClient.Instance.GetHttpClient();
                            var rsp = await hc.GetAsync($"api/conversations/{conversationId}/calls/{callId}");
                            if (rsp.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                heartbeat.Stop();
                                if (!form.IsDisposed) form.Close();
                                return;
                            }
                            if (rsp.IsSuccessStatusCode)
                            {
                                var json = await rsp.Content.ReadAsStringAsync();
                                var cd = System.Text.Json.JsonSerializer.Deserialize<CallResponse>(json,
                                    new System.Text.Json.JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true,
                                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                                    });
                                if (cd != null && (cd.Status == CallStatus.Ended || cd.Status == CallStatus.Missed))
                                {
                                    heartbeat.Stop();
                                    if (!form.IsDisposed) form.Close();
                                }
                            }
                        }
                        catch { /* transient — retry on next tick */ }
                    };
                    heartbeat.Start();

                    form.ShowDialog(this);
                    _activeIncomingDialogs.TryRemove(callId, out _);
                    heartbeat.Stop();

                    if (!form.Accepted)
                    {
                        // Check if the dialog was closed because caller cancelled
                        bool wasCancelled = _pendingCallSignals.TryRemove(callId, out var signals)
                            && signals.Any(s => s == "CALL_ENDED");
                        _pendingCallSignals.TryRemove(callId, out _);
                        _activeCallIds.TryRemove(callId, out _);
                        if (!wasCancelled)
                            try { if (_signalRClient != null) await _signalRClient.SendCallSignalAsync(callId, "CALL_REJECTED"); } catch { }
                        return;
                    }

                    try
                    {
                        var http = ApiClient.Instance.GetHttpClient();
                        var joinResponse = await http.PostAsync($"api/conversations/{conversationId}/calls/{callId}/join", null);
                        if (!joinResponse.IsSuccessStatusCode)
                        {
                            var body = await joinResponse.Content.ReadAsStringAsync();
                            MessageBox.Show(string.Format(LocalizationService.Translate("Could not join call: {0}"), body),
                                LocalizationService.Translate("Call Failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            _pendingCallSignals.TryRemove(callId, out _);
                            _activeCallIds.TryRemove(callId, out _);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(LocalizationService.Translate("Could not join call: {0}"), ex.Message),
                            LocalizationService.Translate("Call Failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _pendingCallSignals.TryRemove(callId, out _);
                        _activeCallIds.TryRemove(callId, out _);
                        return;
                    }

                    try { if (_signalRClient != null) await _signalRClient.SendCallSignalAsync(callId, "CALL_JOINED"); } catch { }

                    if (IsDisposed) return;

                    _activeCallIds.TryAdd(callId, 0);

                    try
                    {
                        var convInfo = _convs.Find(c => c.Id == conversationId);
                        bool isGroupCall = convInfo.IsGroup;
                        var callForm = new Forms.Call.frmVideoCall(callerName, callId, conversationId, _signalRClient!, isGroupCall);
                        callForm.FormClosed += (s, e) =>
                        {
                            _pendingCallSignals.TryRemove(callId, out _);
                            _activeCallIds.TryRemove(callId, out _);
                            this.Activate();
                        };
                        if (_pendingCallSignals.TryRemove(callId, out var pending))
                        {
                            callForm.ReplayPendingSignals(pending);
                        }
                        callForm.Show();
                    }
                    catch (Exception ex)
                    {
                        // Join succeeded but form failed — send CALL_ENDED to avoid orphan call
                        try { if (_signalRClient != null) await _signalRClient.SendCallSignalAsync(callId, "CALL_ENDED"); } catch { }
                        _pendingCallSignals.TryRemove(callId, out _);
                        _activeCallIds.TryRemove(callId, out _);
                        System.Diagnostics.Debug.WriteLine($"[Call] Failed to open call form: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _pendingCallSignals.TryRemove(callId, out _);
                    _activeCallIds.TryRemove(callId, out _);
                    System.Diagnostics.Debug.WriteLine($"[Call] HandleCallIncomingAsync failed: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }

        private Task HandleCallMissedAsync(string callId, string conversationId, string callerName, CallType callType)
        {
            if (IsDisposed) return Task.CompletedTask;

            SafeBeginInvoke(() =>
            {
                var callTypeName = callType == CallType.Video ? "video" : "voice";
                var body = $"Missed {callTypeName} call from {callerName}";

                var s = NotificationSettings.Default;
                if (s.DesktopNotifications)
                    NotificationManager.ShowDesktopNotification("Missed call", body);
                if (s.FlashTaskbar)
                    NotificationManager.FlashWindow(this.Handle);
                if (s.AllowSound)
                    NotificationManager.PlayNotificationSound(s.Volume);
            });

            return Task.CompletedTask;
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
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("SignalR"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
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
                SafeBeginInvoke(() =>
                {
                    if (_lblChatStatus != null)
                        _lblChatStatus.Text = string.Format(LocalizationService.Translate("{0} is typing..."), displayName);
                });
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
                SafeBeginInvoke(() => RestoreChatStatus());
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
            SafeBeginInvoke(() =>
            {
                foreach (Control c in _pnlMessages.Controls)
                {
                    if (c.Tag is string tag && tag == messageId)
                    {
                        c.Invalidate(true); // trigger OnPaint lại — tick sẽ đọc _msgDelivery mới
                        break;
                    }
                }
            });

            return Task.CompletedTask;
        }

        private void RestoreChatStatus()
        {
            if (_lblChatStatus == null || string.IsNullOrWhiteSpace(_activeConvId))
                return;

            // Saved Messages subtitle
            if (!string.IsNullOrWhiteSpace(_savedMessagesConvId) && _activeConvId == _savedMessagesConvId)
            {
                _lblChatStatus.Text = LocalizationService.Translate("Your notes, links, and media");
                return;
            }

            lock (_typingLock)
            {
                if (_typingUsernames.TryGetValue(_activeConvId, out var set) && set.Count > 0)
                {
                    var first = set.First();
                    _lblChatStatus.Text = string.Format(LocalizationService.Translate("{0} is typing..."), first);
                    return;
                }
            }

            var conv = _convs.Find(c => c.Id == _activeConvId);
            if (conv == default)
            {
                _lblChatStatus.Text = "";
                return;
            }

            if (conv.IsGroup)
            {
                _lblChatStatus.Text = "";
                return;
            }

            if (_convOtherUserId.TryGetValue(_activeConvId, out var otherId) &&
                _userPresence.TryGetValue(otherId, out var presence))
                _lblChatStatus.Text = Helpers.PresenceFormatter.GetPresenceText(presence.IsOnline, presence.LastSeenUtc);
            else
                _lblChatStatus.Text = "";
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
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                SafeBeginInvoke(() => MessageBox.Show(this, ex.Message, LocalizationService.Translate("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning));
            }
        }

        // ════════════════════════════════════════════
        //  SEND MESSAGE
        // ════════════════════════════════════════════
        private async void SendMessage()
        {
            // ─────────────────────────────────────────────────────────────────
            // DOUBLE-SEND GUARD: chặn Enter spam / click liên tục
            // ─────────────────────────────────────────────────────────────────
            if (_isSending) return;
            _isSending = true;

            // ─────────────────────────────────────────────────────────────────
            // VALIDATION: Kiểm tra input và trạng thái
            // ─────────────────────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(_tbMessage.Text))
            {
                _isSending = false;
                return;
            }

            string text = _tbMessage.Text.Trim();

            // Validate message length
            if (text.Length > 4096)
            {
                _isSending = false;
                MessageBox.Show(this,
                    LocalizationService.Translate("Message is too long. Please limit to 4096 characters."),
                    LocalizationService.Translate("Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Check for active conversation
            if (string.IsNullOrWhiteSpace(_activeConvId))
            {
                _isSending = false;
                MessageBox.Show(this,
                    LocalizationService.Translate("Please select a conversation before sending a message."),
                    LocalizationService.Translate("Error"),
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
            _currentMsgs.Add((tempMessageId, finalMessageText, true, DateTime.Now.ToString("HH:mm"), ""));
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
                        LocalizationService.Translate("Could not get the encryption key for this conversation. ") +
                        LocalizationService.Translate("Please try reloading the conversation."));
                }

                // Validate conversation key
                var encryptionService = new MessageEncryptionService();
                if (!encryptionService.ValidateConversationKey(conversationKey))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Translate("Invalid encryption key. Please try reloading the conversation."));
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
                        string.Format(LocalizationService.Translate("Cannot encrypt message: {0}"), ex.Message), ex);
                }

                // Validate encryption output
                if (string.IsNullOrWhiteSpace(encryptedContent) || string.IsNullOrWhiteSpace(contentIV))
                {
                    throw new InvalidOperationException(
                        LocalizationService.Translate("Message encryption failed: empty result."));
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
                        string.Format(LocalizationService.Translate("Could not send message to server: {0}"), errorMessage));
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
                    string timeStr = messageResponse.SentAt.ToLocalTime().ToString("HH:mm");
                    _currentMsgs[index] = (
                        messageResponse.MessageID,
                        finalMessageText,
                        true,
                        timeStr,
                        ""
                    );

                    // EXPIRATION TRACKING: track TRƯỚC khi BuildMessages() để
                    // hasExpiryTimer = true ngay từ lúc tính minBw lần đầu
                    // (tránh bubble hẹp rồi timer/timestamp tràn ra ngoài)
                    if (messageResponse.ExpiresAt.HasValue)
                        _expirationService.TrackMessage(messageResponse.MessageID, messageResponse.ExpiresAt.Value);

                    BuildMessages();
                    RefreshConversationItem(_activeConvId, finalMessageText, true, "", timeStr);
                }
                _isSending = false;
            }
            catch (InvalidOperationException ex)
            {
                _isSending = false;
                // ─────────────────────────────────────────────────────────────────
                // ERROR HANDLING: Xử lý lỗi và thông báo cho user
                // ─────────────────────────────────────────────────────────────────
                SafeBeginInvoke(() =>
                {
                    MessageBox.Show(this,
                        string.Format(LocalizationService.Translate("Cannot send message:\n{0}"), ex.Message),
                        LocalizationService.Translate("Send message error"),
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
                });
            }
            catch (Exception ex)
            {
                _isSending = false;
                // ─────────────────────────────────────────────────────────────────
                // UNEXPECTED ERROR: Xử lý lỗi không mong đợi
                // ─────────────────────────────────────────────────────────────────
                SafeBeginInvoke(() =>
                {
                    MessageBox.Show(this,
                        string.Format(LocalizationService.Translate("Unexpected error when sending message:\n{0}\n\nPlease try again."), ex.Message),
                        LocalizationService.Translate("Error"),
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
                });

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
            SafeBeginInvoke(() =>
            {
                _hiddenMessageIds.Add(messageId);
                _messageDates.Remove(messageId);
                _forwardMetadata.TryRemove(messageId, out _);
                _forwardOriginalSenderId.TryRemove(messageId, out _);
                _pinnedByMap.TryRemove(messageId, out _);
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
            });

            // Untrack message (đã được xóa tự động trong service, nhưng gọi để chắc chắn)
            _expirationService.UntrackMessage(messageId);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _isClosing = true;
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

            // Stop and dispose typing debounce timer
            _typingDebounceTimer?.Stop();
            _typingDebounceTimer?.Dispose();

            // Dispose audio recorder
            _audioRecorder?.Dispose();

            // Stop and dispose SignalR client
            if (_signalRClient is not null)
            {
                try
                {
                    _signalRClient.StopAsync().GetAwaiter().GetResult();
                    _signalRClient.DisposeAsync().GetAwaiter().GetResult();
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
            _senderAvatarMap.Clear();
            _allMsgs.Clear();
            _myMemberIdByConv.Clear();
            _syncedConversations.Clear();
            _hiddenMessageIds.Clear();
            lock (_processedMessageIdsLock)
            {
                _processedMessageIds.Clear();
            }

            // Restore original icon so Form.Dispose doesn't dispose _monochromeIcon
            if (_originalIcon != null && this.Icon != _originalIcon)
                this.Icon = _originalIcon;
            _monochromeIcon?.Dispose();
            _monochromeIcon = null;

            // Dispose cached fonts
            _fontTimerActive?.Dispose();
            _fontTimerInactive?.Dispose();

            base.OnFormClosed(e);
        }

        private sealed class ChatMenuColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected           => TG.SidebarHover;
            public override Color MenuItemBorder             => TG.Divider;
            public override Color ToolStripDropDownBackground => TG.SidebarBg;
            public override Color SeparatorDark              => TG.Divider;
            public override Color SeparatorLight             => TG.Divider;
            public override Color ImageMarginGradientBegin   => TG.SidebarBg;
            public override Color ImageMarginGradientMiddle  => TG.SidebarBg;
            public override Color ImageMarginGradientEnd     => TG.SidebarBg;
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

        private void UpdateCachedBackground()
        {
            // Kiểm tra nếu Panel chưa có kích thước hoặc bị ẩn thì không làm gì cả
            if (_pnlMessages.Width <= 0 || _pnlMessages.Height <= 0) return;

            // 1. Tạo một Bitmap mới khớp hoàn toàn với kích thước hiện tại của Panel
            Bitmap newBmp = new Bitmap(_pnlMessages.Width, _pnlMessages.Height);

            using (Graphics g = Graphics.FromImage(newBmp))
            {
                // 2. Tô nền bằng màu mặc định trước (phòng trường hợp ảnh không phủ hết hoặc lỗi)
                g.Clear(TG.ChatBg);

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
            var oldBmp = _pnlMessages.CachedWallpaper;
            _pnlMessages.CachedWallpaper = newBmp;
            _pnlMessages.Invalidate();

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