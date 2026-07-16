using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SecureChat.Services
{
    public sealed class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly string _apiKey;
        private readonly string _senderEmail;
        private readonly string _senderName;

        // Cấu hình SMTP thật (VD: Gmail) — dùng khi không cấu hình SendGrid.
        // Đây là cách gửi email hồi trước khi deploy cloud (chạy MariaDB local).
        private readonly string? _smtpHost;
        private readonly int _smtpPort;
        private readonly bool _smtpEnableSsl;
        private readonly string? _smtpPassword;
        private readonly bool _useSmtp;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;

            var sendGridApiKey = _config["SendGrid:ApiKey"];
            _smtpHost = _config["EmailSettings:SmtpHost"];
            _smtpPort = int.TryParse(_config["EmailSettings:SmtpPort"], out var p) ? p : 587;
            _smtpEnableSsl = !bool.TryParse(_config["EmailSettings:EnableSsl"], out var ssl) || ssl;
            _smtpPassword = _config["EmailSettings:SenderPassword"];

            // Ưu tiên SendGrid nếu có API key thật (luôn bắt đầu bằng "SG."). Nếu không, và có
            // cấu hình SmtpHost, chuyển sang gửi qua SMTP thật (Gmail...) — tránh trường hợp lỡ
            // dùng App Password Gmail làm SendGrid API key (2 định dạng khác nhau, sẽ luôn lỗi 401).
            var hasValidSendGridKey = !string.IsNullOrWhiteSpace(sendGridApiKey)
                && sendGridApiKey.StartsWith("SG.", StringComparison.OrdinalIgnoreCase);

            _useSmtp = !hasValidSendGridKey && !string.IsNullOrWhiteSpace(_smtpHost);

            _apiKey = sendGridApiKey ?? string.Empty;
            _senderEmail = _config["SendGrid:FromEmail"] ?? _config["EmailSettings:SenderEmail"] ?? string.Empty;
            _senderName = _config["SendGrid:FromName"] ?? _config["EmailSettings:SenderName"] ?? "SecureChat";

            if (!hasValidSendGridKey && !string.IsNullOrWhiteSpace(sendGridApiKey))
            {
                _logger.LogWarning(
                    "SendGrid:ApiKey không đúng định dạng (phải bắt đầu bằng 'SG.'). " +
                    "Sẽ {Mode}.", _useSmtp ? "chuyển sang gửi qua SMTP" : "bỏ qua và có thể gửi email thất bại");
            }
        }

        public Task<bool> SendOtpEmailAsync(string toEmail, string otp)
            => _useSmtp ? SendOtpViaSmtpAsync(toEmail, otp) : SendOtpViaSendGridAsync(toEmail, otp);

        private static string BuildOtpHtml(string otp) => $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0;"">
    <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #f4f4f4;"">
        <tr><td style=""padding: 20px 0;"">
            <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);"">
                <tr><td style=""padding: 40px 30px; text-align: center; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-radius: 8px 8px 0 0;"">
                    <h1 style=""color: #ffffff; margin: 0; font-size: 24px;"">SecureChat</h1>
                </td></tr>
                <tr><td style=""padding: 30px;"">
                    <h2 style=""color: #333333; margin: 0 0 20px 0; font-size: 20px;"">Your One-Time Password</h2>
                    <p style=""color: #666666; font-size: 16px; line-height: 1.5; margin: 0 0 20px 0;"">Use the following OTP to complete your login:</p>
                    <div style=""text-align: center; margin: 30px 0;"">
                        <span style=""display: inline-block; font-size: 36px; font-weight: bold; letter-spacing: 8px; color: #667eea; background-color: #f0f0ff; padding: 15px 30px; border-radius: 8px;"">{otp}</span>
                    </div>
                    <p style=""color: #999999; font-size: 14px; line-height: 1.5; margin: 0;"">This code will expire in <strong>5 minutes</strong>. If you did not request this code, please ignore this email.</p>
                </td></tr>
                <tr><td style=""padding: 20px 30px; text-align: center; border-top: 1px solid #eeeeee;"">
                    <p style=""color: #999999; font-size: 12px; margin: 0;"">SecureChat &copy; 2026. All rights reserved.</p>
                </td></tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";

        private async Task<bool> SendOtpViaSendGridAsync(string toEmail, string otp)
        {
            try
            {
                var client = new SendGridClient(_apiKey);
                var from = new EmailAddress(_senderEmail, _senderName);
                var to = new EmailAddress(toEmail);
                var subject = "SecureChat - Your OTP code";
                var htmlContent = BuildOtpHtml(otp);

                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);

                _logger.LogInformation("SendGrid send starting. Sender={Sender} Recipient={Recipient}", _senderEmail, toEmail);

                var response = await client.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("SendGrid email sent to {Recipient}. Status={Status}", toEmail, response.StatusCode);
                    return true;
                }
                else
                {
                    var body = await response.Body.ReadAsStringAsync();
                    _logger.LogError("SendGrid API error. Status={Status} Body={Body}", response.StatusCode, body);
                    _logger.LogWarning("OTP fallback for {Email}: code is {Otp}", toEmail, otp);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendGrid exception for {Email}", toEmail);
                try { _logger.LogWarning("OTP fallback for {Email}: code is {Otp}", toEmail, otp); } catch { }
                return false;
            }
        }

        private async Task<bool> SendOtpViaSmtpAsync(string toEmail, string otp)
        {
            try
            {
                using var client = new SmtpClient(_smtpHost, _smtpPort)
                {
                    EnableSsl = _smtpEnableSsl,
                    Credentials = new NetworkCredential(_senderEmail, _smtpPassword)
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(_senderEmail, _senderName),
                    Subject = "SecureChat - Your OTP code",
                    Body = BuildOtpHtml(otp),
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                _logger.LogInformation("SMTP send starting. Host={Host}:{Port} Sender={Sender} Recipient={Recipient}",
                    _smtpHost, _smtpPort, _senderEmail, toEmail);

                await client.SendMailAsync(mail);

                _logger.LogInformation("SMTP email sent to {Recipient}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP exception for {Email}", toEmail);
                try { _logger.LogWarning("OTP fallback for {Email}: code is {Otp}", toEmail, otp); } catch { }
                return false;
            }
        }
    }
}
