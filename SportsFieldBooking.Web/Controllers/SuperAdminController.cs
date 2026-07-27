using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// Trang cứu hộ (mục 3) — chỉ Super Account (đăng nhập từ appsettings, không có trong DB).
/// Mục đích chính: cấp / thu hồi quyền Admin khi tài khoản Admin bị mất hoặc bị khóa.
/// </summary>
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : Controller
{
    private readonly IUserService _userService;
    public SuperAdminController(IUserService userService) => _userService = userService;

    public async Task<IActionResult> Index()
    {
        return View(await _userService.GetAllAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GrantAdmin(int id)
    {
        var (success, message) = await _userService.GrantAdminAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeAdmin(int id)
    {
        var (success, message) = await _userService.RevokeAdminAsync(id);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        await _userService.ToggleActiveAsync(id);
        TempData["Success"] = "Đã cập nhật trạng thái tài khoản.";
        return RedirectToAction(nameof(Index));
    }
}
