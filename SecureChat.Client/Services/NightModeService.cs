using System;
using System.Linq;
using System.Windows.Forms;
using SecureChat.Client.Settings;

namespace SecureChat.Client.Services
{
    /// <summary>
    /// Dịch vụ quản lý chế độ ban đêm (Night Mode / Dark Mode) cho toàn bộ ứng dụng.
    /// 
    /// Cơ chế hoạt động:
    /// - Khi bật Night Mode: áp dụng bảng màu tối (ApplyDark) lên hệ thống token TG.*
    /// - Khi tắt Night Mode: áp dụng bảng màu sáng (ApplyLight)
    /// - Mọi control dùng TG.* token sẽ tự động hiển thị đúng màu sau khi Invalidate()
    /// - Trạng thái được lưu vào file JSON và khôi phục khi khởi động lại ứng dụng
    /// 
    /// Luồng xử lý khi Toggle():
    ///   Toggle() → ApplyDark/Light → Save() → RefreshOpenForms() → ThemeChanged?.Invoke()
    /// </summary>
    internal static class NightModeService
    {
        // Trạng thái hiện tại - true = đang ở chế độ tối, false = chế độ sáng
        public static bool IsEnabled { get; private set; }

        /// <summary>
        /// Sự kiện được kích hoạt sau khi chuyển theme thành công.
        /// Các form đang mở subscribe sự kiện này để tự cập nhật giao diện.
        /// </summary>
        public static event Action? ThemeChanged;

        /// <summary>
        /// Khởi tạo dịch vụ khi ứng dụng mở.
        /// Đọc trạng thái đã lưu từ NightModeSettings và áp dụng theme tương ứng.
        /// Phải gọi trước khi bất kỳ form nào được hiển thị.
        /// </summary>
        public static void Initialize()
        {
            NightModeSettings.Load();
            if (NightModeSettings.IsEnabled)
            {
                TG.ApplyDark();   // Áp dụng bảng màu tối vào toàn bộ token TG.*
                IsEnabled = true;
            }
            // Nếu IsEnabled = false → mặc định đã là light, không cần ApplyLight()
        }

        /// <summary>
        /// Chuyển đổi giữa chế độ sáng và tối.
        /// Lưu trạng thái mới rồi yêu cầu tất cả form đang mở vẽ lại giao diện.
        /// </summary>
        public static void Toggle()
        {
            if (IsEnabled)
            {
                TG.ApplyLight();  // Đặt lại token TG.* sang bảng màu sáng
                IsEnabled = false;
            }
            else
            {
                TG.ApplyDark();   // Đặt token TG.* sang bảng màu tối
                IsEnabled = true;
            }

            // Lưu ngay sau khi đổi để tránh mất trạng thái nếu app bị đóng đột ngột
            NightModeSettings.IsEnabled = IsEnabled;
            NightModeSettings.Save();

            // Cập nhật giao diện tất cả form đang mở
            RefreshOpenForms();

            // Thông báo cho các subscriber (form phụ, dialog...) tự cập nhật
            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// Yêu cầu tất cả form đang mở vẽ lại.
        /// frmMainChat được xử lý riêng qua OnNightModeChanged() để rebuild
        /// các thành phần phức tạp như bubble tin nhắn và wallpaper.
        /// 
        /// Lưu ý: KHÔNG gọi OnNightModeChanged() trực tiếp ở đây —
        /// ThemeChanged?.Invoke() phía trên đã trigger nó qua event subscription.
        /// Gọi 2 lần → BuildMessages() chạy 2× mỗi Toggle.
        /// </summary>
        private static void RefreshOpenForms()
        {
            var forms = Application.OpenForms.Cast<Form>().ToArray();
            foreach (var form in forms)
            {
                if (form is frmMainChat main)
                    main.OnNightModeChanged();  // Xử lý đặc biệt: rebuild bubble, wallpaper, toggles
                form.Invalidate(true);          // Vẽ lại toàn bộ control con
            }
        }
    }
}
