using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Mục 11: Admin quản lý mã toàn hệ thống; Owner tự tạo mã cho sân mình + gửi email cho khách.</summary>
[Authorize(Roles = "Admin,Owner")]
public class PromotionsController : Controller
{
    private readonly IPromotionService _promotionService;
    private readonly IFieldService _fieldService;

    public PromotionsController(IPromotionService promotionService, IFieldService fieldService)
    {
        _promotionService = promotionService;
        _fieldService = fieldService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    public async Task<IActionResult> Index()
    {
        var promos = IsAdmin
            ? await _promotionService.GetAllAsync()
            : await _promotionService.GetByOwnerAsync(CurrentUserId);
        return View(promos);
    }

    private async Task LoadFieldsAsync()
    {
        ViewBag.Fields = IsAdmin
            ? await _fieldService.GetAllForAdminAsync()
            : await _fieldService.GetByOwnerAsync(CurrentUserId);
        ViewBag.IsAdmin = IsAdmin;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadFieldsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Promotion promo)
    {
        var (success, message) = await _promotionService.CreateAsync(promo, CurrentUserId, IsAdmin);
        if (!success)
        {
            await LoadFieldsAsync();
            ViewBag.Error = message;
            return View(promo);
        }
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var promo = await _promotionService.GetByIdAsync(id);
        if (promo == null) return NotFound();
        if (!IsAdmin && promo.OwnerId != CurrentUserId) return Forbid();
        await LoadFieldsAsync();
        return View(promo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Promotion promo)
    {
        var (success, message) = await _promotionService.UpdateAsync(promo, CurrentUserId, IsAdmin);
        if (!success)
        {
            await LoadFieldsAsync();
            ViewBag.Error = message;
            return View(promo);
        }
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var (success, message) = await _promotionService.ToggleActiveAsync(id, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Gửi mã qua email cho khách từng đặt sân liên quan (mục 11).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int id)
    {
        var (success, message) = await _promotionService.SendToCustomersAsync(id, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
