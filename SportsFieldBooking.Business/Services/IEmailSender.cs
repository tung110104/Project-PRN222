namespace SportsFieldBooking.Business.Services;

public interface IEmailSender
{
    Task SendPasswordResetOtpAsync(string recipientEmail, string recipientName, string otp);
}
