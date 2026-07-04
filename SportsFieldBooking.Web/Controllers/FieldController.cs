using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class FieldController : Controller
{
    private readonly IFieldService _fieldService;
    private readonly IBookingService _bookingService;

    public FieldController(IFieldService fieldService, IBookingService bookingService)
    {
        _fieldService = fieldService;
        _bookingService = bookingService;
    }

    public async Task<IActionResult> Detail(int id, DateOnly? date)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();

        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        ViewBag.SelectedDate = selectedDate;
        ViewBag.BookedSlotIds = await _bookingService.GetBookedSlotIdsAsync(id, selectedDate);
        ViewBag.AvgRating = await _fieldService.GetAverageRatingAsync(id);
        return View(field);
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int fieldId, DateOnly date, List<int> timeSlotIds, string? promoCode, string? note)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (success, message, bookingId) = await _bookingService
            .CreateBookingAsync(userId, fieldId, date, timeSlotIds, promoCode, note);

        if (!success)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Detail), new { id = fieldId, date });
        }
        TempData["Success"] = message;
        return RedirectToAction("Pay", "Booking", new { id = bookingId });
    }
}
