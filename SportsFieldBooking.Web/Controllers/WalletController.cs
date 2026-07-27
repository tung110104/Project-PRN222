using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Ví tiền ảo & điểm thưởng của người dùng (mục 4, 5).</summary>
[Authorize]
public class WalletController : Controller
{
    private readonly IWalletService _walletService;
    private readonly IPointService _pointService;
    private readonly IUserService _userService;
    private readonly ISettingsService _settingsService;

    public WalletController(
        IWalletService walletService,
        IPointService pointService,
        IUserService userService,
        ISettingsService settingsService)
    {
        _walletService = walletService;
        _pointService = pointService;
        _userService = userService;
        _settingsService = settingsService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        if (CurrentUserId <= 0)
        {
            TempData["Error"] = "Super Account không có ví.";
            return RedirectToAction("Index", "Home");
        }

        var wallet = await _walletService.GetOrCreateAsync(CurrentUserId);
        var user = await _userService.GetByIdAsync(CurrentUserId);
        var (tierName, tierPercent) = await _settingsService.GetTierAsync(user?.LifetimePoints ?? 0);

        ViewBag.Wallet = wallet;
        ViewBag.PointBalance = user?.PointBalance ?? 0;
        ViewBag.LifetimePoints = user?.LifetimePoints ?? 0;
        ViewBag.TierName = tierName;
        ViewBag.TierPercent = tierPercent;
        ViewBag.WalletHistory = await _walletService.GetHistoryAsync(CurrentUserId);
        ViewBag.PointHistory = await _pointService.GetHistoryAsync(CurrentUserId);
        ViewBag.VoucherCost = await _settingsService.GetIntAsync("VoucherCostPoints", 200);
        return View();
    }

    /// <summary>Nạp tiền — giả lập cổng thanh toán, cộng ngay (mục 4.1).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(decimal amount)
    {
        var (success, message) = await _walletService.DepositAsync(CurrentUserId, amount);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Đổi điểm lấy voucher gửi qua email (mục 5.5).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedeemVoucher()
    {
        var (success, message) = await _pointService.RedeemVoucherAsync(CurrentUserId);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
