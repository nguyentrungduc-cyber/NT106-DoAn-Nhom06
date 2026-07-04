using System;
using System.IO;
using System.Text;

namespace SecureChat.Client.Settings
{
    /// <summary>
    /// Quản lý việc lưu và đọc trạng thái Night Mode từ ổ đĩa.
    /// 
    /// Dữ liệu được lưu dưới dạng file văn bản đơn giản:
    ///   "1" = Night Mode bật, "0" = Night Mode tắt
    /// 
    /// Đường dẫn file: %AppData%\SecureChat\nightmode.config
    /// Ví dụ: C:\Users\Username\AppData\Roaming\SecureChat\nightmode.config
    /// </summary>
    internal static class NightModeSettings
    {
        // Tên file cấu hình lưu trên ổ đĩa
        private const string FileName = "nightmode.config";

        /// <summary>
        /// Đường dẫn đầy đủ đến file cấu hình.
        /// Dùng thư mục AppData/Roaming để đảm bảo quyền ghi và tồn tại
        /// ngay cả khi ứng dụng không có quyền Administrator.
        /// </summary>
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SecureChat", FileName);

        /// <summary>
        /// Trạng thái Night Mode đã được đọc từ file.
        /// Được đặt bởi Load() và cập nhật bởi NightModeService.Toggle().
        /// </summary>
        public static bool IsEnabled { get; set; }

        /// <summary>
        /// Đọc trạng thái Night Mode từ file cấu hình.
        /// Nếu file chưa tồn tại hoặc đọc lỗi → mặc định Light Mode (IsEnabled = false).
        /// Phải gọi trong NightModeService.Initialize() trước khi hiển thị bất kỳ form nào.
        /// </summary>
        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return; // Lần đầu chạy → dùng Light Mode mặc định

                var text = File.ReadAllText(FilePath, Encoding.UTF8);
                IsEnabled = text.Trim() == "1"; // "1" = Night Mode bật, bất kỳ giá trị khác = tắt
            }
            catch
            {
                // Nếu có lỗi đọc file (bị khóa, quyền hạn...) → dùng Light Mode an toàn
                IsEnabled = false;
            }
        }

        /// <summary>
        /// Lưu trạng thái Night Mode hiện tại xuống file.
        /// Tự động tạo thư mục nếu chưa tồn tại.
        /// Được gọi ngay sau mỗi lần Toggle() để đảm bảo trạng thái không bị mất
        /// khi ứng dụng đóng đột ngột.
        /// </summary>
        public static void Save()
        {
            try
            {
                // Tạo thư mục SecureChat trong AppData nếu chưa có
                var dir = Path.GetDirectoryName(FilePath);
                if (dir != null) Directory.CreateDirectory(dir);

                // Ghi "1" hoặc "0" — đơn giản, nhỏ, dễ đọc
                File.WriteAllText(FilePath, IsEnabled ? "1" : "0", Encoding.UTF8);
            }
            catch { } // Bỏ qua lỗi ghi — không để crash ứng dụng vì không lưu được setting
        }
    }
}
