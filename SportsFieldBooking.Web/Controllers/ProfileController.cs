using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// Trang ho so ca nhan dung chung cho moi role (Customer / Owner / Admin):
/// xem - sua thong tin, doi mat khau va thong ke rieng theo vai tro.
/// Super Account (id = 0, khong co trong DB) khong co ho so.
/// </summary>
[Authorize(Roles = "Customer,Owner,Admin")]
public class ProfileController : Controller
{
    private readonly IAuthService _authService;
    private readonly IBookingService _bookingService;
    private readonly IFieldService _fieldService;
    private readonly IWalletService _walletService;
    private readonly IPointService _pointService;
    private readonly IReportService _reportService;

    public ProfileController(IAuthService authService, IBookingService bookingService, IFieldService fieldService,
        IWalletService walletService, IPointService pointService, IReportService reportService)
    {
        _authService = authService;
        _bookingService = bookingService;
        _fieldService = fieldService;
        _walletService = walletService;
        _pointService = pointService;
        _reportService = reportService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        var user = await _authService.GetProfileAsync(CurrentUserId);
        if (user == null) return NotFound();

        // Thong ke chung cho moi role: booking + vi + diem
        var bookings = await _bookingService.GetByUserAsync(CurrentUserId);
        ViewBag.TotalBookings = bookings.Count;
        ViewBag.CompletedBookings = bookings.Count(b => b.Status == "Completed");
        ViewBag.CancelledBookings = bookings.Count(b => b.Status == "Cancelled");
        ViewBag.TotalSpent = bookings
            .Where(b => b.Payments.Any(p => p.Status == "Paid"))
            .Sum(b => b.TotalAmount);
        ViewBag.Wallet = await _walletService.GetOrCreateAsync(CurrentUserId);
        ViewBag.Tier = MembershipTiers.GetTier(user.LifetimePoints);
        ViewBag.RecentBookings = bookings.Take(5).ToList();

        // Thong ke rieng cho chu san
        if (User.IsInRole("Owner") || User.IsInRole("Admin"))
        {
            var myFields = await _fieldService.GetByOwnerAsync(CurrentUserId);
            ViewBag.MyFields = myFields;
            var today = DateOnly.FromDateTime(DateTime.Now);
            var report = await _reportService.GetSummaryAsync(today.AddDays(-30), today, CurrentUserId);
            ViewBag.OwnerRevenue = report.TotalRevenue;
            ViewBag.OwnerBookings = report.TotalBookings;
        }

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string fullName, string? phone)
    {
        var (ok, message) = await _authService.UpdateProfileAsync(CurrentUserId, fullName, phone);
        TempData[ok ? "Success" : "Error"] = message;

        // Cap nhat lai ten hien thi tren thanh dieu huong (claim Name)
        if (ok)
        {
            var user = await _authService.GetProfileAsync(CurrentUserId);
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new(ClaimTypes.Name, user.FullName),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.Role, user.Role.RoleName)
                };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            }
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Xác nhận mật khẩu mới không khớp.";
            return RedirectToAction(nameof(Index));
        }
        var (ok, message) = await _authService.ChangePasswordAsync(CurrentUserId, currentPassword, newPassword);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
