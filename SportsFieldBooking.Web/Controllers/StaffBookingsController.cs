using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class StaffBookingsController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IWebHostEnvironment _env;
    public StaffBookingsController(IBookingService bookingService, IWebHostEnvironment env)
    {
        _bookingService = bookingService;
        _env = env;
    }

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

    /// <summary>Chi Admin duoc upload anh QR Momo dung cho trang thanh toan.</summary>
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
