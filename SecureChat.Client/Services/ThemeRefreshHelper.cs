using System.Drawing;
using System.Windows.Forms;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Generic "repaint with current TG palette" helper for simple dialog forms
    /// (Settings, EditGroup, MuteNotifications, ...) that don't have custom
    /// per-control theme logic. Form chỉ cần gọi ApplyTo(this) khi
    /// NightModeService.ThemeChanged bắn ra.
    ///
    /// Quy ước: dùng control.Tag = "accent" để giữ nguyên nền màu nhấn (vd nút Blue),
    /// và Tag = "white-fg" để giữ chữ trắng cố định (vd label trên nền màu).
    /// </summary>
    internal static class ThemeRefreshHelper
    {
        public static void ApplyTo(Form form)
        {
            form.BackColor = TG.WindowBg;
            ApplyThemeToControls(form.Controls);
            form.Invalidate(true);
        }

        private static void ApplyThemeToControls(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                if (c.BackColor != Color.Transparent &&
                    c.BackColor != TG.Blue &&
                    c.BackColor != TG.SidebarActive &&
                    c.BackColor != TG.TitleBarBg &&
                    c.Tag as string != "accent")
                    c.BackColor = TG.WindowBg;

                if (c.ForeColor != Color.White &&
                    c.Tag as string != "white-fg")
                    c.ForeColor = TG.TextPrimary;

                c.Invalidate();
                ApplyThemeToControls(c.Controls);
            }
        }

        /// <summary>
        /// Gọi trong constructor của form: tự subscribe/unsubscribe ThemeChanged
        /// theo vòng đời của form, tránh leak event handler.
        /// </summary>
        public static void Hook(Form form)
        {
            void OnThemeChanged()
            {
                if (form.InvokeRequired) { form.Invoke(new System.Action(OnThemeChanged)); return; }
                if (form.IsDisposed) return;
                ApplyTo(form);
            }

            NightModeService.ThemeChanged += OnThemeChanged;
            form.FormClosed += (_, __) => NightModeService.ThemeChanged -= OnThemeChanged;
        }
    }
}
