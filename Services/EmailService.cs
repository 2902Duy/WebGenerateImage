using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity.UI.Services; // Sửa lại interface đúng

namespace WebGenerateImage.Services
{
    public class EmailService   
    {
        private readonly EmailSettings _settings;
        private const string SmtpServer = "smtp.gmail.com";
        private const int SmtpPort = 587;

        public EmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
            if (string.IsNullOrWhiteSpace(_settings.SmtpUser))
                throw new ArgumentException("SmtpUser is null or empty in EmailSettings.");
            if (string.IsNullOrWhiteSpace(_settings.SmtpPass))
                throw new ArgumentException("SmtpPass is null or empty in EmailSettings.");
        }

        // Phương thức chuẩn bắt buộc của IEmailSender
        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("WebApp", _settings.SmtpUser));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;

            message.Body = new TextPart("html")
            {
                Text = htmlMessage
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpServer, SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        // Phương thức riêng của bạn để gửi OTP, có thể gọi trong code khác
        public async Task SendOtpAsync(string toEmail, string code)
        {
            string subject = "Mã OTP xác thực đăng ký";
            string body = $"Mã OTP của bạn là: {code}. Mã có hiệu lực trong 5 phút.";
            await SendEmailAsync(toEmail, subject, body);
        }
    }
}
