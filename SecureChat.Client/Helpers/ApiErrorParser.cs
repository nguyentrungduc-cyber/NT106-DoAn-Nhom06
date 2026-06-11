using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SecureChat.Client.Helpers
{
    /// <summary>
    /// Bóc tách thông điệp lỗi thân thiện từ response trả về của Server.
    ///
    /// Server có 2 dạng error body phổ biến:
    ///   1) <c>{"error": "Thông tin đăng nhập không hợp lệ."}</c>
    ///   2) <c>{"message": "OTP expired.", "errorCode": "EXPIRED_OTP"}</c>
    ///
    /// Lớp này chỉ giữ phần text con người đọc được, đồng thời ánh xạ một số
    /// errorCode quen thuộc sang câu tiếng Việt.
    /// </summary>
    public static class ApiErrorParser
    {
        /// <summary>
        /// Trả về (Title, Message). Title cố định "Không thể tiếp tục"; caller
        /// có thể override.
        /// </summary>
        public static (string Title, string Message) Parse(string? rawError, string fallbackMessage = "Đã xảy ra lỗi không xác định.")
        {
            if (string.IsNullOrWhiteSpace(rawError))
                return ("Không thể tiếp tục", fallbackMessage);

            var trimmed = rawError.Trim();

            // ApiClient ghép "Lỗi server: <body>" hoặc "Không thể kết nối máy chủ: <ex>".
            const string serverPrefix = "Lỗi server:";
            const string networkPrefix = "Không thể kết nối máy chủ:";
            if (trimmed.StartsWith(networkPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return ("Không thể kết nối máy chủ",
                    "Hãy kiểm tra kết nối mạng và thử lại sau.");
            }
            if (trimmed.StartsWith(serverPrefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(serverPrefix.Length).Trim();
            }

            // Nếu phần còn lại là JSON -> bóc các field đã biết.
            if (TryExtractFromJson(trimmed, out var fromJson))
                return fromJson;

            // Plaintext fallback: hiển thị nguyên văn.
            return ("Không thể tiếp tục", trimmed);
        }

        private static bool TryExtractFromJson(string body, out (string Title, string Message) result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(body) || (body[0] != '{' && body[0] != '['))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return false;

                string? message = TryGetString(root, "message")
                                  ?? TryGetString(root, "error")
                                  ?? TryGetString(root, "title")
                                  ?? TryGetString(root, "detail");
                string? code = TryGetString(root, "errorCode")
                              ?? TryGetString(root, "code");

                // ASP.NET ModelState trả về { errors: { fieldName: [..] } }
                if (string.IsNullOrWhiteSpace(message)
                    && root.TryGetProperty("errors", out var errors)
                    && errors.ValueKind == JsonValueKind.Object)
                {
                    var collected = new List<string>();
                    foreach (var prop in errors.EnumerateObject())
                    {
                        if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                        foreach (var msg in prop.Value.EnumerateArray())
                        {
                            var s = msg.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                                collected.Add(s);
                        }
                    }
                    if (collected.Count > 0)
                        message = string.Join("\n• ", collected.Prepend(string.Empty)).TrimStart();
                }

                if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(code))
                    return false;

                var (title, mapped) = MapByCode(code, message);
                result = (title, mapped);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static (string Title, string Message) MapByCode(string? code, string? message)
        {
            string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            string fallback = string.IsNullOrWhiteSpace(message)
                ? "Đã xảy ra lỗi không xác định."
                : message!;

            return normalized switch
            {
                "INVALID_EMAIL" => ("Email không hợp lệ", "Định dạng email chưa đúng."),
                "INVALID_OTP" => ("OTP không đúng", "Mã OTP không chính xác. Vui lòng kiểm tra lại."),
                "EXPIRED_OTP" => ("OTP đã hết hạn", "Vui lòng yêu cầu mã OTP mới."),
                "USER_NOT_FOUND" => ("Không tìm thấy tài khoản", "Email hoặc tên người dùng chưa đăng ký."),
                "INVALID_IDENTIFIER" => ("Thông tin không hợp lệ", "Vui lòng nhập đúng email hoặc tên đăng nhập."),
                "INVALID_TOKEN" => ("Phiên đặt lại không hợp lệ", "Token đặt lại không còn hiệu lực, vui lòng bắt đầu lại."),
                "EXPIRED_TOKEN" => ("Token đã hết hạn", "Phiên đặt lại mật khẩu đã hết hạn."),
                "WEAK_PASSWORD" => ("Mật khẩu chưa đủ mạnh", "Mật khẩu phải có chữ hoa, chữ thường, số và ký tự đặc biệt."),
                "MISSING_RESET_TOKEN" => ("Thiếu token đặt lại", "Vui lòng thực hiện lại bước xác thực OTP."),
                "NETWORK_ERROR" => ("Không thể kết nối máy chủ", "Hãy kiểm tra kết nối mạng và thử lại."),
                "REQUEST_TIMEOUT" => ("Hết thời gian chờ", "Yêu cầu mất quá nhiều thời gian. Vui lòng thử lại."),
                _ => ("Không thể tiếp tục", fallback),
            };
        }

        private static string? TryGetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var value))
                return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}
