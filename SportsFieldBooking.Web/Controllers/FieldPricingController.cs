using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// Trang "Giá &amp; Khung giờ" cua 1 san: chu san (hoac Admin) quan ly
/// khung gio, bang gia da cap (gio/ngay/thang-mua) va ngay vang rieng cua san.
/// </summary>
[Authorize(Roles = "Admin,Owner")]
public class FieldPricingController : Controller
{
    private readonly IFieldService _fieldService;
    private readonly IPricingService _pricingService;

    public FieldPricingController(IFieldService fieldService, IPricingService pricingService)
    {
        _fieldService = fieldService;
        _pricingService = pricingService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    private async Task<Field?> GetOwnedFieldAsync(int fieldId)
    {
        var field = await _fieldService.GetDetailAsync(fieldId);
        if (field == null) return null;
        if (!IsAdmin && field.OwnerId != CurrentUserId) return null;
        return field;
    }

    public async Task<IActionResult> Index(int id)
    {
        var field = await GetOwnedFieldAsync(id);
        if (field == null) return NotFound();
        ViewBag.Rules = await _pricingService.GetRulesAsync(id);
        ViewBag.GoldenDays = await _pricingService.GetGoldenDaysAsync(id);
        ViewBag.AllSlots = await _pricingService.GetAllTimeSlotsAsync(id);
        return View(field);
    }

    // ----- Khung gio -----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeSlot(int fieldId, TimeOnly startTime, TimeOnly endTime)
    {
        if (await GetOwnedFieldAsync(fieldId) == null) return Forbid();
        var (success, message) = await _pricingService.AddTimeSlotAsync(fieldId, startTime, endTime);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { id = fieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTimeSlot(int fieldId, int timeSlotId)
    {
        if (await GetOwnedFieldAsync(fieldId) == null) return Forbid();
        var (success, message) = await _pricingService.ToggleTimeSlotAsync(timeSlotId, fieldId);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { id = fieldId });
    }

    // ----- Bang gia -----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRule(int fieldId, string? ruleName, TimeOnly startTime, TimeOnly endTime,
        string dayType, int? startMonth, int? endMonth, decimal price, int priority)
    {
        if (await GetOwnedFieldAsync(fieldId) == null) return Forbid();
        var (success, message) = await _pricingService.AddRuleAsync(new FieldPricingRule
        {
            FieldId = fieldId,
            RuleName = ruleName,
            StartTime = startTime,
            EndTime = endTime,
            DayType = dayType is "Weekday" or "Weekend" ? dayType : "All",
            StartMonth = startMonth,
            EndMonth = endMonth,
            Price = price,
            Priority = priority
        });
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { id = fieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(int fieldId, int ruleId)
    {
        if (await GetOwnedFieldAsync(fieldId) == null) return Forbid();
        await _pricingService.DeleteRuleAsync(ruleId, fieldId);
        TempData["Success"] = "Đã xóa quy tắc giá.";
        return RedirectToAction(nameof(Index), new { id = fieldId });
    }

    // ----- Ngay vang cua san -----
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGoldenDay(int fieldId, DateOnly date, string name,
        decimal priceMultiplier, int pointsMultiplier)
    {
        if (await GetOwnedFieldAsync(fieldId) == null) return Forbid();
        var (success, message) = await _pricingService.AddGoldenDayAsync(new GoldenDay
        {
            FieldId = fieldId,
            Date = date,
            Name = name,
            PriceMultiplier = priceMultiplier,
            PointsMultiplier = pointsMultiplier
        });
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { id = fieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGoldenDay(int fieldId, int goldenDayId)
    {
        var field = await GetOwnedFieldAsync(fieldId);
        if (field == null) return Forbid();
        // Chu san chi xoa duoc ngay vang cua san minh (khong xoa duoc ngay vang he thong cua Admin)
        var days = await _pricingService.GetGoldenDaysAsync(fieldId);
        var day = days.FirstOrDefault(g => g.GoldenDayId == goldenDayId);
        if (day == null || (day.FieldId == null && !IsAdmin))
        {
            TempData["Error"] = "Không thể xóa ngày vàng hệ thống.";
            return RedirectToAction(nameof(Index), new { id = fieldId });
        }
        await _pricingService.DeleteGoldenDayAsync(goldenDayId);
        TempData["Success"] = "Đã xóa ngày vàng.";
        return RedirectToAction(nameof(Index), new { id = fieldId });
    }
}
