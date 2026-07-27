namespace SportsFieldBooking.Business.Services;

public interface IEmailSender
{
    Task SendPasswordResetOtpAsync(string recipientEmail, string recipientName, string otp);

    /// <summary>Gửi email chung (voucher, khuyến mãi, thông báo bảo trì… — mục 8, 11).</summary>
    Task SendEmailAsync(string recipientEmail, string recipientName, string subject, string htmlBody);
}
