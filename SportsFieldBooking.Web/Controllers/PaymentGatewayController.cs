using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.Web.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// [LUONG CONG THANH TOAN] Nap vi va thanh toan booking deu di qua cong thanh toan nhu that:
/// Checkout -> redirect sang VNPay sandbox (neu da cau hinh TmnCode/HashSecret trong appsettings)
///          -> hoac trang cong demo noi bo (Demo.cshtml) neu chua cau hinh
/// -> nguoi dung xac nhan tren cong -> VnPayReturn / DemoComplete xac thuc va ghi nhan tien.
/// </summary>
[Authorize(Roles = "Customer,Staff,Admin,Owner")]
public class PaymentGatewayController : Controller
{
    private readonly IVnPayService _vnPayService;
    private readonly IPaymentService _paymentService;
    private readonly IWalletService _walletService;
    private readonly IPointService _pointService;
    private readonly IBookingService _bookingService;

    public PaymentGatewayController(IVnPayService vnPayService, IPaymentService paymentService,
        IWalletService walletService, IPointService pointService, IBookingService bookingService)
    {
        _vnPayService = vnPayService;
        _paymentService = paymentService;
        _walletService = walletService;
        _pointService = pointService;
        _bookingService = bookingService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ---------- Buoc 1: khoi tao giao dich ----------
    // type = "booking" (id + pointsToUse) hoac "deposit" (amount)
    public async Task<IActionResult> Checkout(string type, int id = 0, string method = "VNPay",
        int pointsToUse = 0, decimal amount = 0)
    {
        if (type == "booking")
        {
            var booking = await _bookingService.GetDetailAsync(id);
            if (booking == null || booking.UserId != CurrentUserId) return NotFound();
            if (booking.Status == "Cancelled" || booking.Payments.Any(p => p.Status == "Paid"))
            {
                TempData["Error"] = "Booking không thể thanh toán.";
                return RedirectToAction("Index", "Booking");
            }

            // Kiem tra som so diem (PayAsync se kiem tra lai lan cuoi khi chot)
            if (pointsToUse > 0)
            {
                var maxPoints = await _pointService.GetMaxRedeemableAsync(CurrentUserId, booking.TotalAmount);
                if (pointsToUse > maxPoints)
                {
                    TempData["Error"] = $"Chỉ được dùng tối đa {maxPoints} điểm cho booking này.";
                    return RedirectToAction("Pay", "Booking", new { id });
                }
            }
            var due = booking.TotalAmount - pointsToUse * AppConfigSingleton.Instance.PointValueVnd;
            if (due < 0) due = 0;

            // Da cau hinh VNPay that -> redirect sang cong sandbox; TxnRef nhung bookingId + soDiem de xu ly khi quay ve
            if (method == "VNPay" && _vnPayService.IsConfigured && due > 0)
            {
                var txnRef = $"BK-{id}-{pointsToUse}-{DateTime.Now:HHmmssfff}";
                var returnUrl = Url.Action(nameof(VnPayReturn), "PaymentGateway", null, Request.Scheme)!;
                var payUrl = _vnPayService.CreatePaymentUrl(HttpContext, due, txnRef,
                    $"Thanh toan booking #{id} san {booking.Field.FieldName}", returnUrl);
                return Redirect(payUrl);
            }

            // Chua cau hinh -> cong demo noi bo (van co buoc redirect + xac nhan nhu that)
            ViewBag.Type = "booking";
            ViewBag.Id = id;
            ViewBag.Method = method;
            ViewBag.PointsToUse = pointsToUse;
            ViewBag.Amount = due;
            ViewBag.OrderInfo = $"Thanh toán booking #{id} - {booking.Field.FieldName} ({booking.BookingDate:dd/MM/yyyy} {booking.TimeSlot.StartTime:HH\\:mm})";
            return View("Demo");
        }

        if (type == "deposit")
        {
            if (amount < AppConfigSingleton.Instance.MinDepositAmount)
            {
                TempData["Error"] = $"Số tiền nạp tối thiểu là {AppConfigSingleton.Instance.MinDepositAmount:N0}đ.";
                return RedirectToAction("Index", "Wallet");
            }

            if (method == "VNPay" && _vnPayService.IsConfigured)
            {
                var txnRef = $"DP-{CurrentUserId}-{DateTime.Now:HHmmssfff}";
                var returnUrl = Url.Action(nameof(VnPayReturn), "PaymentGateway", null, Request.Scheme)!;
                var payUrl = _vnPayService.CreatePaymentUrl(HttpContext, amount, txnRef,
                    $"Nap {amount:N0}d vao vi SportBooking", returnUrl);
                return Redirect(payUrl);
            }

            ViewBag.Type = "deposit";
            ViewBag.Id = 0;
            ViewBag.Method = method;
            ViewBag.PointsToUse = 0;
            ViewBag.Amount = amount;
            ViewBag.OrderInfo = $"Nạp {amount:N0}đ vào ví SportBooking";
            return View("Demo");
        }

        return NotFound();
    }

    // ---------- Buoc 2a: nguoi dung bam xac nhan / huy tren cong DEMO ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DemoComplete(string type, int id, string method, int pointsToUse,
        decimal amount, bool success)
    {
        if (!success)
        {
            TempData["Error"] = "Giao dịch đã bị hủy trên cổng thanh toán.";
            return type == "booking"
                ? RedirectToAction("Detail", "Booking", new { id })
                : RedirectToAction("Index", "Wallet");
        }

        if (type == "booking")
        {
            // Chot giao dich: PayAsync tu tinh lai tien tren server (khong tin amount tu client)
            var (ok, message) = await _paymentService.PayAsync(id, CurrentUserId, method, pointsToUse);
            TempData[ok ? "Success" : "Error"] = message;
            return RedirectToAction("Detail", "Booking", new { id });
        }

        var (dOk, dMessage) = await _walletService.DepositAsync(CurrentUserId, amount);
        TempData[dOk ? "Success" : "Error"] = dMessage;
        return RedirectToAction("Index", "Wallet");
    }

    // ---------- Buoc 2b: VNPay sandbox redirect ve (giao dich THAT tren cong sandbox) ----------
    public async Task<IActionResult> VnPayReturn()
    {
        var (validSignature, success, txnRef, amount, responseCode) = _vnPayService.ValidateReturn(Request.Query);

        if (!validSignature)
        {
            TempData["Error"] = "Chữ ký VNPay không hợp lệ - giao dịch bị từ chối.";
            return RedirectToAction("Index", "Home");
        }

        var parts = txnRef.Split('-'); // BK-{bookingId}-{points}-{time} | DP-{userId}-{time}
        if (parts.Length >= 3 && parts[0] == "BK" && int.TryParse(parts[1], out var bookingId))
        {
            if (!success)
            {
                TempData["Error"] = $"Thanh toán VNPay không thành công (mã {responseCode}).";
                return RedirectToAction("Detail", "Booking", new { id = bookingId });
            }
            var pointsToUse = parts.Length >= 3 && int.TryParse(parts[2], out var p) ? p : 0;
            var (ok, message) = await _paymentService.PayAsync(bookingId, CurrentUserId, "VNPay", pointsToUse);
            TempData[ok ? "Success" : "Error"] = ok ? $"[VNPay] {message}" : message;
            return RedirectToAction("Detail", "Booking", new { id = bookingId });
        }

        if (parts.Length >= 2 && parts[0] == "DP")
        {
            if (!success)
            {
                TempData["Error"] = $"Nạp tiền VNPay không thành công (mã {responseCode}).";
                return RedirectToAction("Index", "Wallet");
            }
            // So tien lay tu vnp_Amount da duoc xac thuc chu ky
            var (ok, message) = await _walletService.DepositAsync(CurrentUserId, amount);
            TempData[ok ? "Success" : "Error"] = ok ? $"[VNPay] {message}" : message;
            return RedirectToAction("Index", "Wallet");
        }

        TempData["Error"] = "Không nhận dạng được giao dịch.";
        return RedirectToAction("Index", "Home");
    }
}
