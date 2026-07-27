using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

// SuperAdmin (tai khoan cuu ho trong appsettings) cung vao duoc trang nay de cap/thu hoi Admin
[Authorize(Roles = "Admin,SuperAdmin")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    private readonly IWalletService _walletService;
    private readonly IPointService _pointService;

    public UsersController(IUserService userService, IWalletService walletService, IPointService pointService)
    {
        _userService = userService;
        _walletService = walletService;
        _pointService = pointService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Roles = await _userService.GetRolesAsync();
        return View(await _userService.GetAllAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _userService.ToggleActiveAsync(id);
        TempData["Success"] = "Đã cập nhật trạng thái tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, int roleId)
    {
        await _userService.ChangeRoleAsync(id, roleId);
        TempData["Success"] = "Đã đổi vai trò tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    // [SUPER ACCOUNT] Cap / thu hoi quyen Admin nhanh - chuc nang cuu ho khi mat tai khoan admin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmin(int id, bool grant)
    {
        var (success, message) = await _userService.SetAdminAsync(id, grant);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    // ----- Admin dieu chinh vi / diem thu cong (co ly do) -----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustWallet(int id, decimal amount, string reason)
    {
        var (success, message) = await _walletService.AdjustAsync(id, amount, reason);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustPoints(int id, int points, string reason)
    {
        var (success, message) = await _pointService.AdjustAsync(id, points, reason);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
