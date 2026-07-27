using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class FieldController : Controller
{
    private readonly IFieldService _fieldService;
    private readonly IBookingService _bookingService;
    private readonly IPricingService _pricingService;

    public FieldController(IFieldService fieldService, IBookingService bookingService, IPricingService pricingService)
    {
        _fieldService = fieldService;
        _bookingService = bookingService;
        _pricingService = pricingService;
    }

    public async Task<IActionResult> Detail(int id, DateOnly? date)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();

        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        ViewBag.SelectedDate = selectedDate;
        ViewBag.BookedSlotIds = await _bookingService.GetBookedSlotIdsAsync(id, selectedDate);
        ViewBag.AvgRating = await _fieldService.GetAverageRatingAsync(id);
        // Gia tung slot theo bang gia da cap (gio/ngay/mua) + ngay vang cua ngay dang chon
        ViewBag.SlotPrices = await _pricingService.GetSlotPricesAsync(field, selectedDate);
        ViewBag.GoldenDay = await _pricingService.GetGoldenDayAsync(id, selectedDate);
        return View(field);
    }

    // Moi role dang nhap (tru SuperAdmin - khong co trong DB) deu dat san cho chinh minh duoc
    [Authorize(Roles = "Customer,Staff,Admin,Owner")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int fieldId, DateOnly date, List<int> timeSlotIds, string? promoCode, string? note)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (success, message, bookingIds) = await _bookingService
            .CreateBookingAsync(userId, fieldId, date, timeSlotIds, promoCode, note);

        if (!success)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Detail), new { id = fieldId, date });
        }
        TempData["Success"] = message;
        // 1 khung gio -> vao thang trang thanh toan; nhieu khung gio (nhieu booking) -> ve danh sach
        return bookingIds.Count == 1
            ? RedirectToAction("Pay", "Booking", new { id = bookingIds[0] })
            : RedirectToAction("Index", "Booking");
    }
}
