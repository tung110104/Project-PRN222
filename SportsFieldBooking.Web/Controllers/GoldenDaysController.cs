using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// Admin cau hinh ngay vang TOAN HE THONG (FieldId = null - ap dung moi san).
/// Ngay vang rieng tung san do chu san tu them o trang Gia &amp; Khung gio (FieldPricingController).
/// </summary>
[Authorize(Roles = "Admin")]
public class GoldenDaysController : Controller
{
    private readonly IPricingService _pricingService;
    public GoldenDaysController(IPricingService pricingService) => _pricingService = pricingService;

    public async Task<IActionResult> Index()
    {
        // fieldId = null -> xem tat ca ngay vang (he thong + tung san) de admin nam toan canh
        return View(await _pricingService.GetGoldenDaysAsync(null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(DateOnly date, string name, decimal priceMultiplier, int pointsMultiplier)
    {
        var (success, message) = await _pricingService.AddGoldenDayAsync(new GoldenDay
        {
            FieldId = null, // ngay vang he thong - ap dung moi san
            Date = date,
            Name = name,
            PriceMultiplier = priceMultiplier,
            PointsMultiplier = pointsMultiplier
        });
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _pricingService.DeleteGoldenDayAsync(id);
        TempData["Success"] = "Đã xóa ngày vàng.";
        return RedirectToAction(nameof(Index));
    }
}
