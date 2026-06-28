using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
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
        private readonly bool _enableSsl;
        private readonly string _senderEmail;
        private readonly string _senderPassword;
        private readonly string _senderName;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;

            _smtpHost = _config["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(_config["EmailSettings:SmtpPort"], out var p) ? p : 587;
            _enableSsl = bool.TryParse(_config["EmailSettings:EnableSsl"], out var s) ? s : true;
            _senderEmail = _config["EmailSettings:SenderEmail"] ?? string.Empty;
            _senderPassword = _config["EmailSettings:SenderPassword"] ?? string.Empty;
            _senderName = _config["EmailSettings:SenderName"] ?? "SecureChat";
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otp)
        {
            try
            {
                var from = new MailAddress(_senderEmail, _senderName);
                var to = new MailAddress(toEmail);
                using var msg = new MailMessage(from, to)
                {
                    Subject = "SecureChat - Your OTP code",
                    Body = $"Your SecureChat OTP is: {otp}\nThis code will expire in 5 minutes.",
                    IsBodyHtml = false
                };

                _logger.LogInformation("SMTP send starting. Host={Host} Port={Port} EnableSsl={Ssl} Sender={Sender} Recipient={Recipient}",
                    _smtpHost, _smtpPort, _enableSsl, _senderEmail, toEmail);

                using var smtp = new SmtpClient(_smtpHost, _smtpPort)
                {
                    EnableSsl = _enableSsl,
                    Credentials = new NetworkCredential(_senderEmail, _senderPassword),
                    UseDefaultCredentials = false,
                    Timeout = 15000
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await smtp.SendMailAsync(msg, cts.Token);

                _logger.LogInformation("SMTP send completed successfully. Recipient={Recipient}", toEmail);
                _logger.LogInformation("OTP email sent to {Email}", toEmail);
                return true;
            }
            catch (SmtpException ex)
            {
                // Log SMTP-specific exception details to aid debugging
                _logger.LogError(ex, "SMTP error while sending OTP email to {Email}. Host={Host} Port={Port}", toEmail, _smtpHost, _smtpPort);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
                return false;
            }
        }
    }
}
