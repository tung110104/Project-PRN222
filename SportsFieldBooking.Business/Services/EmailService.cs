using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SportsFieldBooking.Business.Services;

public interface IEmailService
{
    /// <summary>Gui email that qua SMTP neu appsettings co cau hinh Smtp; chua cau hinh thi ghi log console (demo).</summary>
    Task SendAsync(string to, string subject, string htmlBody);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    public EmailService(IConfiguration config) => _config = config;

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            // Fallback demo: chua cau hinh SMTP -> in ra console de van demo duoc luong gui mail
            Console.WriteLine($"[EMAIL-DEMO] To: {to} | Subject: {subject}\n{htmlBody}");
            return;
        }

        var port = int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
        var user = _config["Smtp:User"];
        var pass = _config["Smtp:Password"];
        var fromName = _config["Smtp:FromName"] ?? "SportBooking";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass)
        };
        using var mail = new MailMessage
        {
            From = new MailAddress(user ?? "no-reply@sportbooking.local", fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(to);

        try
        {
            await client.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            // Loi gui mail khong duoc lam hong nghiep vu chinh (dat san, huy san van phai thanh cong)
            Console.WriteLine($"[EMAIL-ERROR] {to}: {ex.Message}");
        }
    }
}
