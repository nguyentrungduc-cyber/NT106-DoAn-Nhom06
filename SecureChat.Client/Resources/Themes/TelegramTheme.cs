using System.Drawing;

namespace SecureChat.Client
{
    /// <summary>
    /// Màu sắc chính xác từ Telegram Desktop day-blue theme
    /// </summary>
    public static class TG
    {
        // === CORE COLORS ===
        public static readonly Color Blue         = Color.FromArgb(0x2A, 0xAB, 0xEE); // #2AABEE - màu chủ đạo
        public static readonly Color BlueHover    = Color.FromArgb(0x22, 0x9A, 0xD9);
        public static readonly Color BlueActive   = Color.FromArgb(0x1A, 0x86, 0xBD);
        public static readonly Color BlueDark     = Color.FromArgb(0x16, 0x78, 0xAB);

        // === WINDOW / SIDEBAR ===
        public static readonly Color WindowBg     = Color.FromArgb(0xFF, 0xFF, 0xFF); // nền trắng
        public static readonly Color SidebarBg    = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color SidebarHover = Color.FromArgb(0xF4, 0xF4, 0xF4);
        public static readonly Color SidebarActive= Color.FromArgb(0x2A, 0xAB, 0xEE); // item đang chọn = xanh

        // === TITLEBAR / HEADER ===
        public static readonly Color TitleBarBg   = Color.FromArgb(0x2A, 0xAB, 0xEE);
        public static readonly Color TitleBarFg   = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color TitleBarSub  = Color.FromArgb(0xD0, 0xEE, 0xFF);

        // === TEXT ===
        public static readonly Color TextPrimary  = Color.FromArgb(0x00, 0x00, 0x00);
        public static readonly Color TextSecondary= Color.FromArgb(0x70, 0x70, 0x70);
        public static readonly Color TextHint     = Color.FromArgb(0xAA, 0xAA, 0xAA);
        public static readonly Color TextBlue     = Color.FromArgb(0x2A, 0xAB, 0xEE);
        public static readonly Color TextName     = Color.FromArgb(0x1A, 0x1A, 0x1A);
        public static readonly Color TextTime     = Color.FromArgb(0x99, 0x99, 0x99);

        // === CHAT BACKGROUND ===
        public static readonly Color ChatBg       = Color.FromArgb(0xDB, 0xE6, 0xF0); // xanh nhạt pattern
        public static readonly Color MsgInBg      = Color.FromArgb(0xFF, 0xFF, 0xFF); // bubble nhận = trắng
        public static readonly Color MsgOutBg     = Color.FromArgb(0xEF, 0xFD, 0xDE); // bubble gửi = xanh lá nhạt
        public static readonly Color MsgOutBgBlue = Color.FromArgb(0x40, 0xA7, 0xE3); // bubble gửi mode xanh

        // === INPUT ===
        public static readonly Color InputBg      = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color InputBorder  = Color.FromArgb(0xE5, 0xE5, 0xE5);
        public static readonly Color InputFocused = Color.FromArgb(0x2A, 0xAB, 0xEE);

        // === BORDERS / DIVIDERS ===
        public static readonly Color Divider      = Color.FromArgb(0xE4, 0xE4, 0xE4);
        public static readonly Color DividerLight = Color.FromArgb(0xF0, 0xF0, 0xF0);

        // === BADGE / UNREAD ===
        public static readonly Color BadgeBg      = Color.FromArgb(0x2A, 0xAB, 0xEE);
        public static readonly Color BadgeFg      = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color BadgeMuted   = Color.FromArgb(0xB0, 0xB0, 0xB0);

        // === AVATAR COLORS (Telegram auto-generates) ===
        public static readonly Color[] AvatarColors = {
            Color.FromArgb(0xFF, 0x61, 0x6A), // đỏ
            Color.FromArgb(0xFF, 0xA8, 0x43), // cam
            Color.FromArgb(0xA0, 0xDE, 0x7E), // xanh lá
            Color.FromArgb(0x72, 0xD5, 0xFD), // xanh dương nhạt
            Color.FromArgb(0x2A, 0xAB, 0xEE), // xanh blue
            Color.FromArgb(0xE0, 0x71, 0x7D), // hồng
            Color.FromArgb(0xA9, 0x5D, 0xD8), // tím
        };

        // === FONTS ===
        public static Font FontTitle(float size)  => new Font("Segoe UI", size, FontStyle.Bold);
        public static Font FontRegular(float size)=> new Font("Segoe UI", size, FontStyle.Regular);
        public static Font FontSemiBold(float size)=> new Font("Segoe UI Semibold", size, FontStyle.Regular);
        public static Font FontMono(float size)   => new Font("Consolas", size, FontStyle.Regular);

        // === CORNER RADIUS (cho custom draw) ===
        public const int RadiusSmall  = 6;
        public const int RadiusMedium = 12;
        public const int RadiusLarge  = 18;
        public const int RadiusBubble = 16;

        // === Helper: lấy màu avatar theo tên ===
        public static Color GetAvatarColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return AvatarColors[0];
            return AvatarColors[Math.Abs(name.GetHashCode()) % AvatarColors.Length];
        }
    }
}
