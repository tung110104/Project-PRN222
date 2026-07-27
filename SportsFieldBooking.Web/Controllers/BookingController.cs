using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Mục 10: mọi role đăng nhập đều đặt/xem được booking của mình (không chỉ Customer).</summary>
[Authorize]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IPaymentService _paymentService;
    private readonly IReviewService _reviewService;
    private readonly IWalletService _walletService;

    public BookingController(
        IBookingService bookingService,
        IPaymentService paymentService,
        IReviewService reviewService,
        IWalletService walletService)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
        _reviewService = reviewService;
        _walletService = walletService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        await _bookingService.CompletePastBookingsAsync();
        var bookings = await _bookingService.GetByUserAsync(CurrentUserId);
        return View(bookings);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var booking = await _bookingService.GetDetailAsync(id);
        if (booking == null || booking.UserId != CurrentUserId) return NotFound();
        return View(booking);
    }

    [HttpGet]
    public async Task<IActionResult> Pay(int id)
    {
        var booking = await _bookingService.GetDetailAsync(id);
        if (booking == null || booking.UserId != CurrentUserId) return NotFound();
        if (booking.Status == "Cancelled" || booking.Payments.Any(p => p.Status == "Paid"))
        {
            TempData["Error"] = "Booking không thể thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        var wallet = await _walletService.GetOrCreateAsync(CurrentUserId);
        ViewBag.WalletBalance = wallet.Balance;
        ViewBag.WalletLocked = wallet.IsLocked;
        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, string method)
    {
        var (success, message) = await _paymentService.PayAsync(id, CurrentUserId, method);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (success, message) = await _bookingService.CancelAsync(id, CurrentUserId, isStaff: false);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, int rating, string? comment)
    {
        var (success, message) = await _reviewService.AddReviewAsync(id, CurrentUserId, rating, comment);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReview(int id, int rating, string? comment)
    {
        var (success, message) = await _reviewService.UpdateReviewAsync(id, CurrentUserId, rating, comment);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var (success, message) = await _reviewService.DeleteReviewAsync(id, CurrentUserId);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Detail), new { id });
    }
}
