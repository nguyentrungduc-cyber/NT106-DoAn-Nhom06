using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SecureChat.Services
{
    public sealed class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _senderName;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;

            _smtpHost = _config["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(_config["EmailSettings:SmtpPort"], out var p) ? p : 587;
            _senderEmail = _config["EmailSettings:SenderEmail"] ?? string.Empty;
            _senderPassword = _config["EmailSettings:SenderPassword"] ?? string.Empty;
            _senderName = _config["EmailSettings:SenderName"] ?? "SecureChat";
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp)
        {
            try
            {
                var msg = new MimeMessage();
                msg.From.Add(new MailboxAddress(_senderName, _senderEmail));
                msg.To.Add(new MailboxAddress(toEmail, toEmail));
                msg.Subject = "SecureChat - Your OTP code";
                msg.Body = new TextPart("plain")
                {
                    Text = $"Your SecureChat OTP is: {otp}\nThis code will expire in 5 minutes."
                };

                _logger.LogInformation("SMTP send starting. Host={Host} Port={Port} Sender={Sender} Recipient={Recipient}",
                    _smtpHost, _smtpPort, _senderEmail, toEmail);

                using var smtp = new SmtpClient();
                smtp.Timeout = 15000;

                await smtp.ConnectAsync(_smtpHost, _smtpPort, SecureSocketOptions.Auto);
                await smtp.AuthenticateAsync(_senderEmail, _senderPassword);
                await smtp.SendAsync(msg);
                await smtp.DisconnectAsync(true);

                _logger.LogInformation("SMTP send completed successfully. Recipient={Recipient}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to {Email}. Host={Host} Port={Port}", toEmail, _smtpHost, _smtpPort);
                try
                {
                    _logger.LogWarning("OTP fallback for {Email}: code is {Otp}", toEmail, otp);
                }
                catch { }
                return false;
            }
        }
    }
}
