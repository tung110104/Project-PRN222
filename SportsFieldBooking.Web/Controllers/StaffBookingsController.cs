using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// Quản lý đặt lịch (mục 6): Owner duyệt booking sân mình; Staff/Admin duyệt mọi booking;
/// Staff/Admin còn ĐẶT HỘ khách walk-in / qua điện thoại (mục 10).
/// </summary>
[Authorize(Roles = "Admin,Owner,Staff")]
public class StaffBookingsController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IFieldService _fieldService;
    private readonly IUserService _userService;
    private readonly IWebHostEnvironment _env;

    public StaffBookingsController(
        IBookingService bookingService,
        IFieldService fieldService,
        IUserService userService,
        IWebHostEnvironment env)
    {
        _bookingService = bookingService;
        _fieldService = fieldService;
        _userService = userService;
        _env = env;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwnerOnly => User.IsInRole("Owner") && !User.IsInRole("Admin");

    public async Task<IActionResult> Index(string? status)
    {
        await _bookingService.CompletePastBookingsAsync();
        int? ownerId = IsOwnerOnly ? CurrentUserId : null;   // Owner: sân mình; Staff/Admin: tất cả
        var bookings = await _bookingService.GetForStaffAsync(ownerId, status);
        ViewBag.Status = status;
        return View(bookings);
    }

    /// <summary>Owner chỉ thao tác booking thuộc sân mình; Staff/Admin thao tác tất cả.</summary>
    private async Task<bool> CanManageAsync(int bookingId)
    {
        if (!IsOwnerOnly) return true;
        var booking = await _bookingService.GetDetailAsync(bookingId);
        return booking != null && booking.Field.OwnerId == CurrentUserId;
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

    // ============================== ĐẶT HỘ KHÁCH (mục 10) ==============================

    [HttpGet]
    public async Task<IActionResult> BookFor()
    {
        ViewBag.Customers = await _userService.GetCustomersAsync();
        ViewBag.Fields = IsOwnerOnly
            ? await _fieldService.GetByOwnerAsync(CurrentUserId)
            : await _fieldService.GetAllForAdminAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BookFor(
        int customerId, string? guestName, string? guestPhone,
        int fieldId, DateOnly date, int timeSlotId, string? promoCode, string? note)
    {
        // customerId = 0 → khách vãng lai: đơn đứng tên người tạo, kèm tên/SĐT khách
        var isGuest = customerId <= 0;
        if (isGuest && string.IsNullOrWhiteSpace(guestName))
        {
            TempData["Error"] = "Khách vãng lai cần nhập tên khách.";
            return RedirectToAction(nameof(BookFor));
        }

        var request = new BookingRequest(
            CustomerId: isGuest ? CurrentUserId : customerId,
            CreatedById: CurrentUserId,
            GuestName: isGuest ? guestName : null,
            GuestPhone: isGuest ? guestPhone : null,
            FieldId: fieldId,
            TimeSlotId: timeSlotId,
            Date: date,
            PromoCode: promoCode,
            PointsToUse: 0,          // đặt hộ không quy đổi điểm của khách
            Note: note);

        var (success, message, bookingId) = await _bookingService.CreateBookingAsync(request);
        TempData[success ? "Success" : "Error"] = message +
            (success ? $" (Booking #{bookingId} — đặt hộ)" : "");
        return RedirectToAction(success ? nameof(Index) : nameof(BookFor));
    }

    /// <summary>Slot trống của 1 sân trong 1 ngày (AJAX cho form đặt hộ).</summary>
    [HttpGet]
    public async Task<IActionResult> FreeSlots(int fieldId, DateOnly date)
    {
        var field = await _fieldService.GetDetailAsync(fieldId);
        if (field == null) return Json(Array.Empty<object>());

        var booked = await _bookingService.GetBookedSlotIdsAsync(fieldId, date);
        var prices = await _bookingService.GetSlotPricesAsync(fieldId, date);
        var result = field.TimeSlots
            .Where(t => t.IsActive && !booked.Contains(t.TimeSlotId))
            .OrderBy(t => t.StartTime)
            .Select(t => new
            {
                id = t.TimeSlotId,
                label = $"{t.StartTime:HH\\:mm} - {t.EndTime:HH\\:mm}",
                price = prices.TryGetValue(t.TimeSlotId, out var p) ? p : 0
            });
        return Json(result);
    }

    // ============================== QR MOMO (chỉ Admin) ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadQr(IFormFile qrImage)
    {
        if (qrImage == null || qrImage.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn ảnh QR.";
            return RedirectToAction(nameof(Index));
        }

        var ext = Path.GetExtension(qrImage.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg"))
        {
            TempData["Error"] = "Chỉ chấp nhận ảnh PNG hoặc JPG.";
            return RedirectToAction(nameof(Index));
        }

        var imagesDir = Path.Combine(_env.WebRootPath, "images");
        Directory.CreateDirectory(imagesDir);
        var savePath = Path.Combine(imagesDir, "momo-qr.png");

        using (var stream = new FileStream(savePath, FileMode.Create))
        {
            await qrImage.CopyToAsync(stream);
        }

        TempData["Success"] = "Đã cập nhật ảnh QR Momo thanh toán.";
        return RedirectToAction(nameof(Index));
    }
}
