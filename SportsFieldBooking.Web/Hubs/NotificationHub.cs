using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SportsFieldBooking.Web.Hubs;

/// <summary>
/// Hub SignalR day thong bao real-time (chuong tren navbar).
/// Moi user khi ket noi duoc gom vao group "user_{userId}" - server chi can gui vao group
/// la moi tab/trinh duyet dang mo cua user do deu nhan duoc.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private string? CurrentUserId => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        await base.OnDisconnectedAsync(exception);
    }
}
