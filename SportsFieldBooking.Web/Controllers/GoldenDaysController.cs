using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Ngày vàng (mục 1): Admin cấu hình toàn hệ thống; Owner cấu hình cho sân mình.</summary>
[Authorize(Roles = "Admin,Owner")]
public class GoldenDaysController : Controller
{
    private readonly IGoldenDayService _goldenDayService;
    private readonly IFieldService _fieldService;

    public GoldenDaysController(IGoldenDayService goldenDayService, IFieldService fieldService)
    {
        _goldenDayService = goldenDayService;
        _fieldService = fieldService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    public async Task<IActionResult> Index()
    {
        ViewBag.IsAdmin = IsAdmin;
        ViewBag.Fields = IsAdmin
            ? await _fieldService.GetAllForAdminAsync()
            : await _fieldService.GetByOwnerAsync(CurrentUserId);
        var days = IsAdmin
            ? await _goldenDayService.GetAllAsync()
            : await _goldenDayService.GetByOwnerAsync(CurrentUserId);
        return View(days);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GoldenDay day)
    {
        var (success, message) = await _goldenDayService.CreateAsync(day, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var (success, message) = await _goldenDayService.ToggleActiveAsync(id, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, message) = await _goldenDayService.DeleteAsync(id, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
