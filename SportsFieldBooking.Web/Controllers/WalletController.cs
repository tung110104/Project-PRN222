using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

// Trang "Ví & Điểm" cua nguoi dung: so du vi, lich su giao dich, nap tien, diem + hang thanh vien
[Authorize(Roles = "Customer,Admin,Owner")]
public class WalletController : Controller
{
    private readonly IWalletService _walletService;
    private readonly IPointService _pointService;
    private readonly IUserService _userService;

    public WalletController(IWalletService walletService, IPointService pointService, IUserService userService)
    {
        _walletService = walletService;
        _pointService = pointService;
        _userService = userService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        var wallet = await _walletService.GetOrCreateAsync(CurrentUserId);
        var user = (await _userService.GetAllAsync()).First(u => u.UserId == CurrentUserId);

        ViewBag.Wallet = wallet;
        ViewBag.WalletTransactions = await _walletService.GetTransactionsAsync(CurrentUserId);
        ViewBag.PointTransactions = await _pointService.GetHistoryAsync(CurrentUserId);
        ViewBag.Tier = MembershipTiers.GetTier(user.LifetimePoints);
        ViewBag.AllTiers = MembershipTiers.All;
        ViewBag.DepositBonusTiers = AppConfigSingleton.Instance.DepositBonusTiers;
        ViewBag.VoucherOptions = AppConfigSingleton.Instance.VoucherOptions;
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deposit(decimal amount, string method = "VNPay")
    {
        // Nap tien luon di qua cong thanh toan (VNPay sandbox that neu da cau hinh, khong thi cong demo);
        // tien chi vao vi sau khi cong bao giao dich thanh cong.
        return RedirectToAction("Checkout", "PaymentGateway", new { type = "deposit", amount, method });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedeemVoucher(int option)
    {
        var (ok, message) = await _pointService.RedeemVoucherAsync(CurrentUserId, option);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    // ----- Admin: quan tri vi toan he thong -----
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage()
    {
        return View(await _walletService.GetAllForAdminAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int id)
    {
        await _walletService.ToggleLockAsync(id);
        TempData["Success"] = "Đã cập nhật trạng thái khóa ví.";
        return RedirectToAction(nameof(Manage));
    }
}
