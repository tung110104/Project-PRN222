using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

// Admin tao ma toan he thong; Owner (chu san) tao ma cho san cua minh va gui email cho khach
[Authorize(Roles = "Admin,Owner")]
public class PromotionsController : Controller
{
    private readonly IPromotionService _promotionService;
    public PromotionsController(IPromotionService promotionService) => _promotionService = promotionService;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");
    // Admin: null (xem/gui tat ca); Owner: chi ma cua minh
    private int? ScopeOwnerId => IsAdmin ? null : CurrentUserId;

    public async Task<IActionResult> Index() => View(await _promotionService.GetAllAsync(ScopeOwnerId));

    [HttpGet]
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Promotion promo)
    {
        // Owner tao -> ma gan voi chu san (chi ap dung cho san cua ho); Admin tao -> ma he thong
        promo.OwnerId = IsAdmin ? null : CurrentUserId;
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
        if (!IsAdmin && promo.OwnerId != CurrentUserId) return Forbid();
        return View(promo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Promotion promo)
    {
        var existing = await _promotionService.GetByIdAsync(promo.PromotionId);
        if (existing == null) return NotFound();
        if (!IsAdmin && existing.OwnerId != CurrentUserId) return Forbid();

        existing.Code = promo.Code;
        existing.Description = promo.Description;
        existing.DiscountPercent = promo.DiscountPercent;
        existing.MaxDiscount = promo.MaxDiscount;
        existing.StartDate = promo.StartDate;
        existing.EndDate = promo.EndDate;
        existing.Quantity = promo.Quantity;

        var (success, message) = await _promotionService.UpdateAsync(existing);
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
        var promo = await _promotionService.GetByIdAsync(id);
        if (promo == null) return NotFound();
        if (!IsAdmin && promo.OwnerId != CurrentUserId) return Forbid();
        await _promotionService.ToggleActiveAsync(id);
        return RedirectToAction(nameof(Index));
    }

    // ----- Gui ma qua email: chon nguoi nhan truoc khi gui (khong phat offline) -----
    [HttpGet]
    public async Task<IActionResult> SendEmail(int id)
    {
        var promo = await _promotionService.GetByIdAsync(id);
        if (promo == null) return NotFound();
        if (!IsAdmin && promo.OwnerId != CurrentUserId) return Forbid();

        ViewBag.Recipients = await _promotionService.GetRecipientCandidatesAsync(id, ScopeOwnerId);
        return View(promo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(int id, List<int> userIds)
    {
        var (success, message, _) = await _promotionService.SendPromotionEmailAsync(id, ScopeOwnerId, userIds);
        if (!success)
        {
            var promo = await _promotionService.GetByIdAsync(id);
            if (promo == null) return NotFound();
            ViewBag.Error = message;
            ViewBag.Recipients = await _promotionService.GetRecipientCandidatesAsync(id, ScopeOwnerId);
            return View(promo);
        }
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }
}
