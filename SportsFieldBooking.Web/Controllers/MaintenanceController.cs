using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Mục 8: Owner gửi yêu cầu bảo trì → Admin duyệt/từ chối (tự hủy + hoàn tiền booking trùng).</summary>
[Authorize(Roles = "Admin,Owner")]
public class MaintenanceController : Controller
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly IFieldService _fieldService;

    public MaintenanceController(IMaintenanceService maintenanceService, IFieldService fieldService)
    {
        _maintenanceService = maintenanceService;
        _fieldService = fieldService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    public async Task<IActionResult> Index(string? status)
    {
        ViewBag.IsAdmin = IsAdmin;
        ViewBag.Status = status;
        if (IsAdmin)
            return View(await _maintenanceService.GetAllAsync(status));

        ViewBag.Fields = await _fieldService.GetByOwnerAsync(CurrentUserId);
        return View(await _maintenanceService.GetByOwnerAsync(CurrentUserId));
    }

    /// <summary>Owner gửi yêu cầu bảo trì.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Create(int fieldId, DateOnly fromDate, DateOnly toDate, string reason)
    {
        var (success, message) = await _maintenanceService.CreateAsync(
            fieldId, CurrentUserId, fromDate, toDate, reason);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Admin duyệt: sân → Maintenance, tự hủy + hoàn tiền + email các booking dính lịch.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(int id, string? adminNote)
    {
        var (success, message) = await _maintenanceService.ApproveAsync(id, adminNote);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(int id, string? adminNote)
    {
        var (success, message) = await _maintenanceService.RejectAsync(id, adminNote);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
