namespace SportsFieldBooking.Business.Patterns;

/// <summary>Factory Pattern: tao cac loai thong bao (Email / SMS).
/// Ban demo ghi log ra console; co the thay bang SMTP that.</summary>
public interface INotificationSender
{
    Task SendAsync(string to, string subject, string message);
}

public class EmailNotificationSender : INotificationSender
{
    public Task SendAsync(string to, string subject, string message)
    {
        Console.WriteLine($"[EMAIL] To: {to} | {subject} | {message}");
        return Task.CompletedTask;
    }
}

public class SmsNotificationSender : INotificationSender
{
    public Task SendAsync(string to, string subject, string message)
    {
        Console.WriteLine($"[SMS] To: {to} | {message}");
        return Task.CompletedTask;
    }
}

public enum NotificationType { Email, Sms }

public static class NotificationFactory
{
    public static INotificationSender Create(NotificationType type) => type switch
    {
        NotificationType.Email => new EmailNotificationSender(),
        NotificationType.Sms => new SmsNotificationSender(),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
