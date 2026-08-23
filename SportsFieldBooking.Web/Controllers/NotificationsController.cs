using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>API JSON cho chuong thong bao tren navbar (_Layout.cshtml goi bang fetch).</summary>
[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetLatest()
    {
        var userId = CurrentUserId;
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
        var notifications = await _notificationService.GetLatestAsync(userId);
        return Json(new
        {
            unreadCount,
            notifications = notifications.Select(n => new
            {
                notificationId = n.NotificationId,
                title = n.Title,
                message = n.Message,
                url = n.Url,
                isRead = n.IsRead,
                createdAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _notificationService.MarkAsReadAsync(id, CurrentUserId);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        await _notificationService.MarkAllAsReadAsync(CurrentUserId);
        return Json(new { success = true });
    }
}
