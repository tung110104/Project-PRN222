using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin")]
public class PromotionsController : Controller
{
    private readonly IPromotionService _promotionService;
    public PromotionsController(IPromotionService promotionService) => _promotionService = promotionService;

    public async Task<IActionResult> Index() => View(await _promotionService.GetAllAsync());

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Promotion promo)
    {
        var (success, message) = await _promotionService.CreateAsync(promo);
        if (!success)
        {
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
        return View(promo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Promotion promo)
    {
        var (success, message) = await _promotionService.UpdateAsync(promo);
        if (!success)
        {
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
        await _promotionService.ToggleActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
