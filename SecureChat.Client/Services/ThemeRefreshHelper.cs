using System;
using System.Drawing;
using System.Windows.Forms;
using SecureChat.Client.Resources.Themes;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Lớp tiện ích hỗ trợ cập nhật giao diện cho các form khi chuyển Night Mode.
    /// 
    /// Thay vì mỗi form tự viết lại logic subscribe/unsubscribe sự kiện và duyệt
    /// cây control, form chỉ cần gọi ThemeRefreshHelper.Hook(this) một lần trong
    /// constructor để tự động được cập nhật khi theme thay đổi.
    /// 
    /// Cách dùng trong constructor của mỗi form:
    ///   ThemeRefreshHelper.Hook(this);
    /// </summary>
    internal static class ThemeRefreshHelper
    {
        /// <summary>
        /// Đăng ký form vào hệ thống Night Mode.
        /// Khi NightModeService.ThemeChanged được kích hoạt, form sẽ tự động:
        ///   1. Gọi lại trên UI thread (an toàn với InvokeRequired)
        ///   2. Cập nhật BackColor của form về TG.WindowBg
        ///   3. Duyệt đệ quy tất cả control con và cập nhật màu
        /// Khi form đóng, tự động hủy đăng ký để tránh memory leak.
        /// </summary>
        /// <param name="form">Form cần đăng ký vào hệ thống Night Mode</param>
        public static void Hook(Form form)
        {
            // Handler được đăng ký với NightModeService.ThemeChanged
            Action handler = null!;
            handler = () =>
            {
                // Nếu đang ở thread khác, chuyển về UI thread trước
                if (form.InvokeRequired)
                {
                    form.Invoke(handler);
                    return;
                }
                ApplyTo(form);
            };

            // Đăng ký khi form được tạo
            NightModeService.ThemeChanged += handler;

            // Tự hủy đăng ký khi form bị đóng — tránh memory leak
            form.FormClosed += (_, __) => NightModeService.ThemeChanged -= handler;
        }

        /// <summary>
        /// Áp dụng theme hiện tại lên một control và toàn bộ con cháu của nó.
        /// Duyệt đệ quy cây control, bỏ qua các màu đặc biệt như Transparent,
        /// màu accent (TG.Blue, TG.SidebarActive) để giữ nguyên thiết kế.
        /// </summary>
        /// <param name="root">Control gốc (thường là Form)</param>
        public static void ApplyTo(Control root)
        {
            // Cập nhật giao diện form gốc
            root.BackColor = TG.WindowBg;
            root.Invalidate(true); // Yêu cầu vẽ lại toàn bộ

            // Duyệt đệ quy cập nhật tất cả control con
            UpdateControls(root.Controls);
        }

        /// <summary>
        /// Duyệt đệ quy và cập nhật màu cho từng control.
        /// Chỉ đổi BackColor nếu không phải màu accent đặc biệt.
        /// </summary>
        private static void UpdateControls(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                // Bỏ qua màu Transparent — control đang kế thừa màu từ parent
                if (c.BackColor == Color.Transparent)
                    goto recurse;

                // Bỏ qua các màu accent có chủ đích — không được đổi theo theme
                if (c.BackColor == TG.Blue        ||  // Nút xanh chính
                    c.BackColor == TG.SidebarActive||  // Conversation đang chọn
                    c.BackColor == TG.TitleBarBg)      // Thanh tiêu đề
                    goto recurse;

                // Đổi sang màu nền của theme hiện tại
                c.BackColor = TG.WindowBg;

                // Đổi màu chữ nếu không phải màu trắng đặc biệt (chữ trên nền màu)
                if (c.ForeColor != Color.White)
                    c.ForeColor = TG.TextPrimary;

                c.Invalidate(); // Vẽ lại control này

                recurse:
                // Tiếp tục với các control con
                if (c.Controls.Count > 0)
                    UpdateControls(c.Controls);
            }
        }
    }
}
