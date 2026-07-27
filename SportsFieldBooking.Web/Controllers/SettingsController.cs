using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Admin cấu hình tham số hệ thống (mục 5): tỷ lệ tiền↔điểm, ngưỡng hạng, % giảm, bonus nạp…</summary>
[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ISettingsService _settingsService;
    public SettingsController(ISettingsService settingsService) => _settingsService = settingsService;

    public async Task<IActionResult> Index()
    {
        return View(await _settingsService.GetAllAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            TempData["Error"] = "Thiếu khóa hoặc giá trị.";
            return RedirectToAction(nameof(Index));
        }

        await _settingsService.UpdateAsync(key.Trim(), value.Trim());
        TempData["Success"] = $"Đã cập nhật {key}.";
        return RedirectToAction(nameof(Index));
    }
}
