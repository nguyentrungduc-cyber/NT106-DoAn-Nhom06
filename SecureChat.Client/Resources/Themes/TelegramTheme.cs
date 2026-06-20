using System;
using System.Drawing;

namespace SecureChat.Client
{
    public static class TG
    {
        // ── Light palette ──────────────────────────────────────────────
        private static readonly Color
            Blue_L         = Color.FromArgb(0x2A, 0xAB, 0xEE),
            BlueHover_L    = Color.FromArgb(0x22, 0x9A, 0xD9),
            BlueActive_L   = Color.FromArgb(0x1A, 0x86, 0xBD),
            BlueDark_L     = Color.FromArgb(0x16, 0x78, 0xAB),
            WindowBg_L     = Color.FromArgb(0xFF, 0xFF, 0xFF),
            SidebarBg_L    = Color.FromArgb(0xFF, 0xFF, 0xFF),
            SidebarHover_L = Color.FromArgb(0xF4, 0xF4, 0xF4),
            SidebarActive_L= Color.FromArgb(0x2A, 0xAB, 0xEE),
            TitleBarBg_L   = Color.FromArgb(0x2A, 0xAB, 0xEE),
            TitleBarFg_L   = Color.FromArgb(0xFF, 0xFF, 0xFF),
            TitleBarSub_L  = Color.FromArgb(0xD0, 0xEE, 0xFF),
            TextPrimary_L  = Color.FromArgb(0x00, 0x00, 0x00),
            TextSecondary_L= Color.FromArgb(0x70, 0x70, 0x70),
            TextHint_L     = Color.FromArgb(0xAA, 0xAA, 0xAA),
            TextBlue_L     = Color.FromArgb(0x2A, 0xAB, 0xEE),
            TextName_L     = Color.FromArgb(0x1A, 0x1A, 0x1A),
            TextTime_L     = Color.FromArgb(0x99, 0x99, 0x99),
            ChatBg_L       = Color.FromArgb(0xDB, 0xE6, 0xF0),
            MsgInBg_L      = Color.FromArgb(0xFF, 0xFF, 0xFF),
            MsgOutBg_L     = Color.FromArgb(0xEF, 0xFD, 0xDE),
            MsgOutBgBlue_L = Color.FromArgb(0x40, 0xA7, 0xE3),
            InputBg_L      = Color.FromArgb(0xFF, 0xFF, 0xFF),
            InputBorder_L  = Color.FromArgb(0xE5, 0xE5, 0xE5),
            InputFocused_L = Color.FromArgb(0x2A, 0xAB, 0xEE),
            Divider_L      = Color.FromArgb(0xE4, 0xE4, 0xE4),
            DividerLight_L = Color.FromArgb(0xF0, 0xF0, 0xF0),
            BadgeBg_L      = Color.FromArgb(0x2A, 0xAB, 0xEE),
            BadgeFg_L      = Color.FromArgb(0xFF, 0xFF, 0xFF),
            BadgeMuted_L   = Color.FromArgb(0xB0, 0xB0, 0xB0),
            CAccent_L      = Color.FromArgb(0x33, 0x99, 0xFF),
            AccentGreen_L  = Color.FromArgb(0x24, 0xAA, 0x6B),
            SeekBg_L       = Color.FromArgb(0xC8, 0xC8, 0xC8),
            FileSizeColor_L= Color.FromArgb(0x50, 0xA0, 0x78);

        // ── Dark palette (Telegram Desktop Dark) ──────────────────────
        private static readonly Color
            Blue_D         = Color.FromArgb(0x2A, 0xAB, 0xEE),
            BlueHover_D    = Color.FromArgb(0x22, 0x9A, 0xD9),
            BlueActive_D   = Color.FromArgb(0x1A, 0x86, 0xBD),
            BlueDark_D     = Color.FromArgb(0x16, 0x78, 0xAB),
            WindowBg_D     = Color.FromArgb(0x17, 0x21, 0x2B),
            SidebarBg_D    = Color.FromArgb(0x17, 0x21, 0x2B),
            SidebarHover_D = Color.FromArgb(0x22, 0x30, 0x3D),
            SidebarActive_D= Color.FromArgb(0x2A, 0xAB, 0xEE),
            TitleBarBg_D   = Color.FromArgb(0x17, 0x21, 0x2B),
            TitleBarFg_D   = Color.FromArgb(0xFF, 0xFF, 0xFF),
            TitleBarSub_D  = Color.FromArgb(0xA3, 0xAD, 0xB8),
            TextPrimary_D  = Color.FromArgb(0xFF, 0xFF, 0xFF),
            TextSecondary_D= Color.FromArgb(0xA3, 0xAD, 0xB8),
            TextHint_D     = Color.FromArgb(0x5C, 0x6A, 0x7A),
            TextBlue_D     = Color.FromArgb(0x2A, 0xAB, 0xEE),
            TextName_D     = Color.FromArgb(0xFF, 0xFF, 0xFF),
            TextTime_D     = Color.FromArgb(0x86, 0x96, 0xA7),
            ChatBg_D       = Color.FromArgb(0x0E, 0x16, 0x21),
            MsgInBg_D      = Color.FromArgb(0x18, 0x25, 0x33),
            MsgOutBg_D     = Color.FromArgb(0x2B, 0x52, 0x78),
            MsgOutBgBlue_D = Color.FromArgb(0x2B, 0x52, 0x78),
            InputBg_D      = Color.FromArgb(0x17, 0x21, 0x2B),
            InputBorder_D  = Color.FromArgb(0x23, 0x2E, 0x3C),
            InputFocused_D = Color.FromArgb(0x2A, 0xAB, 0xEE),
            Divider_D      = Color.FromArgb(0x23, 0x2E, 0x3C),
            DividerLight_D = Color.FromArgb(0x23, 0x2E, 0x3C),
            BadgeBg_D      = Color.FromArgb(0x2A, 0xAB, 0xEE),
            BadgeFg_D      = Color.FromArgb(0xFF, 0xFF, 0xFF),
            BadgeMuted_D   = Color.FromArgb(0x5C, 0x6A, 0x7A),
            CAccent_D      = Color.FromArgb(0x33, 0x99, 0xFF),
            AccentGreen_D  = Color.FromArgb(0x24, 0xAA, 0x6B),
            SeekBg_D       = Color.FromArgb(0x5C, 0x6A, 0x7A),
            FileSizeColor_D= Color.FromArgb(0xA3, 0xAD, 0xB8);

        // ── Active properties (initially Light) ───────────────────────
        public static Color Blue          { get; internal set; } = Blue_L;
        public static Color BlueHover     { get; internal set; } = BlueHover_L;
        public static Color BlueActive    { get; internal set; } = BlueActive_L;
        public static Color BlueDark      { get; internal set; } = BlueDark_L;
        public static Color WindowBg      { get; internal set; } = WindowBg_L;
        public static Color SidebarBg     { get; internal set; } = SidebarBg_L;
        public static Color SidebarHover  { get; internal set; } = SidebarHover_L;
        public static Color SidebarActive { get; internal set; } = SidebarActive_L;
        public static Color TitleBarBg    { get; internal set; } = TitleBarBg_L;
        public static Color TitleBarFg    { get; internal set; } = TitleBarFg_L;
        public static Color TitleBarSub   { get; internal set; } = TitleBarSub_L;
        public static Color TextPrimary   { get; internal set; } = TextPrimary_L;
        public static Color TextSecondary { get; internal set; } = TextSecondary_L;
        public static Color TextHint      { get; internal set; } = TextHint_L;
        public static Color TextBlue      { get; internal set; } = TextBlue_L;
        public static Color TextName      { get; internal set; } = TextName_L;
        public static Color TextTime      { get; internal set; } = TextTime_L;
        public static Color ChatBg        { get; internal set; } = ChatBg_L;
        public static Color MsgInBg       { get; internal set; } = MsgInBg_L;
        public static Color MsgOutBg      { get; internal set; } = MsgOutBg_L;
        public static Color MsgOutBgBlue  { get; internal set; } = MsgOutBgBlue_L;
        public static Color InputBg       { get; internal set; } = InputBg_L;
        public static Color InputBorder   { get; internal set; } = InputBorder_L;
        public static Color InputFocused  { get; internal set; } = InputFocused_L;
        public static Color Divider       { get; internal set; } = Divider_L;
        public static Color DividerLight  { get; internal set; } = DividerLight_L;
        public static Color BadgeBg       { get; internal set; } = BadgeBg_L;
        public static Color BadgeFg       { get; internal set; } = BadgeFg_L;
        public static Color BadgeMuted    { get; internal set; } = BadgeMuted_L;
        public static Color CAccent       { get; internal set; } = CAccent_L;
        public static Color AccentGreen   { get; internal set; } = AccentGreen_L;
        public static Color SeekBg        { get; internal set; } = SeekBg_L;
        public static Color FileSizeColor { get; internal set; } = FileSizeColor_L;

        // ── Palette switch ────────────────────────────────────────────
        internal static void ApplyLight()
        {
            Blue          = Blue_L;
            BlueHover     = BlueHover_L;
            BlueActive    = BlueActive_L;
            BlueDark      = BlueDark_L;
            WindowBg      = WindowBg_L;
            SidebarBg     = SidebarBg_L;
            SidebarHover  = SidebarHover_L;
            SidebarActive = SidebarActive_L;
            TitleBarBg    = TitleBarBg_L;
            TitleBarFg    = TitleBarFg_L;
            TitleBarSub   = TitleBarSub_L;
            TextPrimary   = TextPrimary_L;
            TextSecondary = TextSecondary_L;
            TextHint      = TextHint_L;
            TextBlue      = TextBlue_L;
            TextName      = TextName_L;
            TextTime      = TextTime_L;
            ChatBg        = ChatBg_L;
            MsgInBg       = MsgInBg_L;
            MsgOutBg      = MsgOutBg_L;
            MsgOutBgBlue  = MsgOutBgBlue_L;
            InputBg       = InputBg_L;
            InputBorder   = InputBorder_L;
            InputFocused  = InputFocused_L;
            Divider       = Divider_L;
            DividerLight  = DividerLight_L;
            BadgeBg       = BadgeBg_L;
            BadgeFg       = BadgeFg_L;
            BadgeMuted    = BadgeMuted_L;
            CAccent       = CAccent_L;
            AccentGreen   = AccentGreen_L;
            SeekBg        = SeekBg_L;
            FileSizeColor = FileSizeColor_L;
        }

        internal static void ApplyDark()
        {
            Blue          = Blue_D;
            BlueHover     = BlueHover_D;
            BlueActive    = BlueActive_D;
            BlueDark      = BlueDark_D;
            WindowBg      = WindowBg_D;
            SidebarBg     = SidebarBg_D;
            SidebarHover  = SidebarHover_D;
            SidebarActive = SidebarActive_D;
            TitleBarBg    = TitleBarBg_D;
            TitleBarFg    = TitleBarFg_D;
            TitleBarSub   = TitleBarSub_D;
            TextPrimary   = TextPrimary_D;
            TextSecondary = TextSecondary_D;
            TextHint      = TextHint_D;
            TextBlue      = TextBlue_D;
            TextName      = TextName_D;
            TextTime      = TextTime_D;
            ChatBg        = ChatBg_D;
            MsgInBg       = MsgInBg_D;
            MsgOutBg      = MsgOutBg_D;
            MsgOutBgBlue  = MsgOutBgBlue_D;
            InputBg       = InputBg_D;
            InputBorder   = InputBorder_D;
            InputFocused  = InputFocused_D;
            Divider       = Divider_D;
            DividerLight  = DividerLight_D;
            BadgeBg       = BadgeBg_D;
            BadgeFg       = BadgeFg_D;
            BadgeMuted    = BadgeMuted_D;
            CAccent       = CAccent_D;
            AccentGreen   = AccentGreen_D;
            SeekBg        = SeekBg_D;
            FileSizeColor = FileSizeColor_D;
        }

        // === AVATAR COLORS (same in both modes) ===
        public static readonly Color[] AvatarColors = {
            Color.FromArgb(0xFF, 0x61, 0x6A),
            Color.FromArgb(0xFF, 0xA8, 0x43),
            Color.FromArgb(0xA0, 0xDE, 0x7E),
            Color.FromArgb(0x72, 0xD5, 0xFD),
            Color.FromArgb(0x2A, 0xAB, 0xEE),
            Color.FromArgb(0xE0, 0x71, 0x7D),
            Color.FromArgb(0xA9, 0x5D, 0xD8),
        };

        // === FONTS ===
        public static Font FontTitle(float size)  => new Font("Segoe UI", size, FontStyle.Bold);
        public static Font FontRegular(float size)=> new Font("Segoe UI", size, FontStyle.Regular);
        public static Font FontSemiBold(float size)=> new Font("Segoe UI Semibold", size, FontStyle.Regular);
        public static Font FontMono(float size)   => new Font("Consolas", size, FontStyle.Regular);

        // === CORNER RADIUS ===
        public const int RadiusSmall  = 6;
        public const int RadiusMedium = 12;
        public const int RadiusLarge  = 18;
        public const int RadiusBubble = 16;

        public static Color GetAvatarColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return AvatarColors[0];
            return AvatarColors[Math.Abs(name.GetHashCode()) % AvatarColors.Length];
        }
    }
}
