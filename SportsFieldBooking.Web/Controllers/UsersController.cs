using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly IUserService _userService;
    public UsersController(IUserService userService) => _userService = userService;

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
}
