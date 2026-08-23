using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

/// <summary>
/// Cong day thong bao real-time. Business khong tham chieu SignalR truc tiep (giu dung 3 lop) -
/// lop Web implement bang IHubContext&lt;NotificationHub&gt; (xem Web/Services/SignalRNotifier.cs).
/// </summary>
public interface IRealtimeNotifier
{
    Task PushAsync(int userId, object payload);
}

public interface INotificationService
{
    /// <summary>Luu thong bao vao DB roi day real-time toi nguoi nhan qua SignalR.</summary>
    Task NotifyAsync(int userId, string title, string message, string? url = null);
    Task<List<AppNotification>> GetLatestAsync(int userId, int count = 15);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public NotificationService(IUnitOfWork uow, IRealtimeNotifier realtimeNotifier)
    {
        _uow = uow;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task NotifyAsync(int userId, string title, string message, string? url = null)
    {
        // Cat theo do dai cot trong DB (Title 200 / Message 500) - tranh loi truncate khi noi dung qua dai
        static string Cut(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

        var notification = new AppNotification
        {
            UserId = userId,
            Title = Cut(title, 200),
            Message = Cut(message, 500),
            Url = url,
            IsRead = false,
            CreatedAt = DateTime.Now
        };
        await _uow.Notifications.AddAsync(notification);
        await _uow.SaveChangesAsync();

        // Day real-time; loi ket noi SignalR khong duoc lam hong nghiep vu (thong bao van nam trong DB)
        try
        {
            await _realtimeNotifier.PushAsync(userId, new
            {
                notificationId = notification.NotificationId,
                title = notification.Title,
                message = notification.Message,
                url = notification.Url,
                createdAt = notification.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            });
        }
        catch { /* bo qua loi push real-time */ }
    }

    public Task<List<AppNotification>> GetLatestAsync(int userId, int count = 15) =>
        _uow.Notifications.Query()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync();

    public Task<int> GetUnreadCountAsync(int userId) =>
        _uow.Notifications.Query().CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _uow.Notifications.Query()
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
        if (notification == null || notification.IsRead) return;
        notification.IsRead = true;
        _uow.Notifications.Update(notification);
        await _uow.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unread = await _uow.Notifications.Query()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        if (unread.Count == 0) return;
        foreach (var n in unread)
        {
            n.IsRead = true;
            _uow.Notifications.Update(n);
        }
        await _uow.SaveChangesAsync();
    }
}
