using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin,Staff,Owner")]
public class StaffBookingsController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IFieldService _fieldService;
    private readonly IUserService _userService;
    private readonly IPaymentService _paymentService;

    public StaffBookingsController(IBookingService bookingService, IFieldService fieldService,
        IUserService userService, IPaymentService paymentService)
    {
        _bookingService = bookingService;
        _fieldService = fieldService;
        _userService = userService;
        _paymentService = paymentService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwnerOnly => User.IsInRole("Owner") && !User.IsInRole("Admin");

    public async Task<IActionResult> Index(string? status)
    {
        await _bookingService.CompletePastBookingsAsync();
        // Owner chi thay booking san cua minh; Admin/Staff thay tat ca
        int? ownerId = IsOwnerOnly ? CurrentUserId : null;
        var bookings = await _bookingService.GetForStaffAsync(ownerId, status);
        ViewBag.Status = status;
        return View(bookings);
    }

    /// <summary>Owner chi thao tac booking thuoc san cua minh; Admin/Staff thao tac tat ca.</summary>
    private async Task<bool> CanManageAsync(int bookingId)
    {
        if (!IsOwnerOnly) return true;
        var booking = await _bookingService.GetDetailAsync(bookingId);
        return booking != null && booking.Field.OwnerId == CurrentUserId;
    }

    // ----- Dat ho khach (walk-in / dat qua dien thoai) -----
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Fields = IsOwnerOnly
            ? await _fieldService.GetByOwnerAsync(CurrentUserId)
            : await _fieldService.GetAllForAdminAsync();
        ViewBag.Customers = await _userService.GetCustomersAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int customerId, int fieldId, DateOnly date, List<int> timeSlotIds,
        string? promoCode, string? note, bool cashPaid = false)
    {
        // Dat ho: booking dung ten khach (customerId), CreatedById ghi nguoi thao tac de truy vet
        var noteFull = string.IsNullOrWhiteSpace(note) ? "Đặt hộ tại quầy" : $"Đặt hộ: {note}";
        var (success, message, bookingIds) = await _bookingService.CreateBookingAsync(
            customerId, fieldId, date, timeSlotIds, promoCode, noteFull, createdById: CurrentUserId);

        if (!success)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Create));
        }

        // Khach tra tien mat ngay tai quay -> ghi nhan thanh toan Cash luon
        if (cashPaid)
        {
            foreach (var id in bookingIds)
                await _paymentService.PayAsync(id, customerId, "Cash");
            message += " Đã ghi nhận thanh toán tiền mặt.";
        }

        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    // API nho cho form dat ho: lay khung gio trong cua san theo ngay (tra JSON)
    [HttpGet]
    public async Task<IActionResult> AvailableSlots(int fieldId, DateOnly date)
    {
        var field = await _fieldService.GetDetailAsync(fieldId);
        if (field == null) return NotFound();
        var bookedIds = await _bookingService.GetBookedSlotIdsAsync(fieldId, date);
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var today = DateOnly.FromDateTime(DateTime.Now);

        var slots = field.TimeSlots
            .Where(s => s.IsActive && !bookedIds.Contains(s.TimeSlotId))
            .Where(s => date != today || s.StartTime > now)
            .OrderBy(s => s.StartTime)
            .Select(s => new { s.TimeSlotId, Start = s.StartTime.ToString("HH:mm"), End = s.EndTime.ToString("HH:mm") });
        return Json(slots);
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
