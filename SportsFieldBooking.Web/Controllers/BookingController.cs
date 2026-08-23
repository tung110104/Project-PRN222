using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

// Moi role dang nhap deu dat va quan ly booking cua minh duoc (Customer, Admin, Owner).
// SuperAdmin la tai khoan cuu ho khong co trong DB nen khong dat san.
[Authorize(Roles = "Customer,Admin,Owner")]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly IPaymentService _paymentService;
    private readonly IReviewService _reviewService;
    private readonly IPointService _pointService;
    private readonly IWalletService _walletService;

    public BookingController(IBookingService bookingService, IPaymentService paymentService,
        IReviewService reviewService, IPointService pointService, IWalletService walletService)
    {
        _bookingService = bookingService;
        _paymentService = paymentService;
        _reviewService = reviewService;
        _pointService = pointService;
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

    // GET trang thanh toan: chon phuong thuc (VNPay/Momo/Cash/Wallet) + so diem muon dung
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

        // Du lieu cho form: so diem toi da dung duoc, gia tri 1 diem, so du vi, san co nhan vi khong
        ViewBag.MaxRedeemPoints = await _pointService.GetMaxRedeemableAsync(CurrentUserId, booking.TotalAmount);
        ViewBag.PointValue = AppConfigSingleton.Instance.PointValueVnd;
        var wallet = await _walletService.GetOrCreateAsync(CurrentUserId);
        ViewBag.WalletBalance = wallet.Balance;
        ViewBag.WalletLocked = wallet.IsLocked;
        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, string method, int pointsToUse = 0)
    {
        // Phuong thuc online (VNPay/Momo) -> chuyen sang cong thanh toan (sandbox that hoac demo);
        // Vi noi bo & tien mat chot ngay khong qua cong.
        if (method is "VNPay" or "Momo")
            return RedirectToAction("Checkout", "PaymentGateway", new { type = "booking", id, method, pointsToUse });

        var (success, message) = await _paymentService.PayAsync(id, CurrentUserId, method, pointsToUse);
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
}
