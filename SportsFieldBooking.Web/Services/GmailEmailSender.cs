using System.Net;
using System.Net.Mail;
using System.Text;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Services;

public class GmailEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public GmailEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private (SmtpClient Smtp, string SenderEmail, string SenderName) CreateClient()
    {
        var settings = _configuration.GetSection("EmailSettings");
        var host = settings["SmtpServer"] ?? "smtp.gmail.com";
        var port = int.TryParse(settings["Port"], out var configuredPort) ? configuredPort : 587;
        var senderEmail = settings["SenderEmail"];
        var username = settings["Username"];
        var password = settings["Password"];
        var senderName = settings["SenderName"] ?? "Sports Field Booking";

        if (string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Chưa cấu hình EmailSettings cho Gmail SMTP.");
        }

        var smtp = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password)
        };
        return (smtp, senderEmail, senderName);
    }

    public async Task SendEmailAsync(
        string recipientEmail, string recipientName, string subject, string htmlBody)
    {
        var (smtp, senderEmail, senderName) = CreateClient();
        using (smtp)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName, Encoding.UTF8),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            message.To.Add(new MailAddress(recipientEmail, recipientName, Encoding.UTF8));
            await smtp.SendMailAsync(message);
        }
    }

    public Task SendPasswordResetOtpAsync(
        string recipientEmail, string recipientName, string otp)
    {
        var body = $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto">
                <h2 style="color:#198754">Sports Field Booking</h2>
                <p>Xin chào {WebUtility.HtmlEncode(recipientName)},</p>
                <p>Mã OTP để đặt lại mật khẩu của bạn là:</p>
                <div style="font-size:32px;font-weight:bold;letter-spacing:8px;
                            color:#198754;margin:24px 0">{otp}</div>
                <p>Mã có hiệu lực trong <strong>5 phút</strong>.</p>
                <p>Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email này.</p>
            </div>
            """;
        return SendEmailAsync(recipientEmail, recipientName, "Mã OTP đặt lại mật khẩu", body);
    }
}
