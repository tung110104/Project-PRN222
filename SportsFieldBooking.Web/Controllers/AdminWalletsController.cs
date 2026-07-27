using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Admin quản trị ví (mục 4.7): xem mọi ví, cộng/trừ thủ công có lý do, khóa ví.</summary>
[Authorize(Roles = "Admin")]
public class AdminWalletsController : Controller
{
    private readonly IWalletService _walletService;
    public AdminWalletsController(IWalletService walletService) => _walletService = walletService;

    public async Task<IActionResult> Index()
    {
        return View(await _walletService.GetAllWalletsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int userId, decimal amount, string reason)
    {
        var (success, message) = await _walletService.AdjustAsync(userId, amount, reason);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int userId)
    {
        await _walletService.ToggleLockAsync(userId);
        TempData["Success"] = "Đã đổi trạng thái khóa ví.";
        return RedirectToAction(nameof(Index));
    }
}
