using Microsoft.AspNetCore.SignalR;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.Web.Hubs;

namespace SportsFieldBooking.Web.Services;

/// <summary>
/// Implement IRealtimeNotifier cua lop Business bang SignalR.
/// Nho lop trung gian nay ma Business khong can tham chieu ASP.NET Core / SignalR (giu dung 3 lop).
/// </summary>
public class SignalRNotifier : IRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PushAsync(int userId, object payload) =>
        _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", payload);
}
