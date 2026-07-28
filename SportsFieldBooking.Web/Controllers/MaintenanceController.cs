using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

// Owner gui yeu cau bao tri san cua minh; Admin duyet/tu choi.
// Duyet -> san khoa dat trong khoang bao tri, booking trung lich tu dong huy + hoan tien + email.
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
        if (IsAdmin)
        {
            ViewBag.Status = status;
            return View("Admin", await _maintenanceService.GetAllForAdminAsync(status));
        }
        ViewBag.Fields = await _fieldService.GetByOwnerAsync(CurrentUserId);
        return View("Owner", await _maintenanceService.GetByOwnerAsync(CurrentUserId));
    }

    [Authorize(Roles = "Owner")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int fieldId, string reason, DateOnly startDate, DateOnly endDate)
    {
        var (success, message) = await _maintenanceService.CreateAsync(CurrentUserId, fieldId, reason, startDate, endDate);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? adminNote)
    {
        var (success, message) = await _maintenanceService.ApproveAsync(id, adminNote);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? adminNote)
    {
        var (success, message) = await _maintenanceService.RejectAsync(id, adminNote);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }
}
