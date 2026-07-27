using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class FieldController : Controller
{
    private readonly IFieldService _fieldService;
    private readonly IBookingService _bookingService;
    private readonly ISettingsService _settingsService;
    private readonly IUserService _userService;

    public FieldController(
        IFieldService fieldService,
        IBookingService bookingService,
        ISettingsService settingsService,
        IUserService userService)
    {
        _fieldService = fieldService;
        _bookingService = bookingService;
        _settingsService = settingsService;
        _userService = userService;
    }

    public async Task<IActionResult> Detail(int id, DateOnly? date)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();

        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        ViewBag.SelectedDate = selectedDate;
        ViewBag.BookedSlotIds = await _bookingService.GetBookedSlotIdsAsync(id, selectedDate);
        ViewBag.SlotPrices = await _bookingService.GetSlotPricesAsync(id, selectedDate);
        ViewBag.GoldenDay = await _bookingService.GetGoldenDayAsync(selectedDate, id);
        ViewBag.AvgRating = await _fieldService.GetAverageRatingAsync(id);

        // Thông tin phục vụ form đặt: điểm hiện có + hạng của người đang đăng nhập
        if (User.Identity?.IsAuthenticated == true &&
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) && uid > 0)
        {
            var me = await _userService.GetByIdAsync(uid);
            if (me != null)
            {
                var (tierName, tierPercent) = await _settingsService.GetTierAsync(me.LifetimePoints);
                ViewBag.MyPoints = me.PointBalance;
                ViewBag.MyTier = tierName;
                ViewBag.MyTierPercent = tierPercent;
            }
        }
        return View(field);
    }

    /// <summary>Mục 9 + 10: 1 lần đặt = 1 khung giờ; mọi role đăng nhập đều đặt được.</summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(
        int fieldId, DateOnly date, int timeSlotId, string? promoCode, int pointsToUse, string? note)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (userId <= 0)
        {
            TempData["Error"] = "Super Account không dùng để đặt sân.";
            return RedirectToAction(nameof(Detail), new { id = fieldId, date });
        }

        var request = new BookingRequest(
            CustomerId: userId,
            CreatedById: null,
            GuestName: null,
            GuestPhone: null,
            FieldId: fieldId,
            TimeSlotId: timeSlotId,
            Date: date,
            PromoCode: promoCode,
            PointsToUse: pointsToUse,
            Note: note);

        var (success, message, bookingId) = await _bookingService.CreateBookingAsync(request);

        if (!success)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Detail), new { id = fieldId, date });
        }
        TempData["Success"] = message;
        return RedirectToAction("Pay", "Booking", new { id = bookingId });
    }
}
