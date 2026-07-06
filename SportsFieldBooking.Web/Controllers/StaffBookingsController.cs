using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class StaffBookingsController : Controller
{
    private readonly IBookingService _bookingService;
    public StaffBookingsController(IBookingService bookingService) => _bookingService = bookingService;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(string? status)
    {
        await _bookingService.CompletePastBookingsAsync();
        int? ownerId = User.IsInRole("Admin") ? null : CurrentUserId;
        var bookings = await _bookingService.GetForStaffAsync(ownerId, status);
        ViewBag.Status = status;
        return View(bookings);
    }

    /// <summary>Staff chi duoc thao tac tren booking thuoc san cua minh; Admin thao tac tat ca.</summary>
    private async Task<bool> CanManageAsync(int bookingId)
    {
        if (User.IsInRole("Admin")) return true;
        var booking = await _bookingService.GetDetailAsync(bookingId);
        return booking != null && booking.BookingDetails.Any(d => d.Field.OwnerId == CurrentUserId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        if (!await CanManageAsync(id)) return Forbid();
        var (success, message) = await _bookingService.ConfirmAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        if (!await CanManageAsync(id)) return Forbid();
        var (success, message) = await _bookingService.CancelAsync(id, CurrentUserId, isStaff: true);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
