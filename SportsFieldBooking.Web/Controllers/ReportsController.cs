using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin,Staff,Owner")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    public ReportsController(IReportService reportService) => _reportService = reportService;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Now);
        var fromDate = from ?? toDate.AddDays(-30);
        ViewBag.From = fromDate;
        ViewBag.To = toDate;
        // Owner chi xem bao cao san cua minh; Admin/Staff xem toan he thong
        int? ownerId = User.IsInRole("Owner") && !User.IsInRole("Admin") ? CurrentUserId : null;
        ViewBag.IsOwnerScope = ownerId.HasValue;
        return View(await _reportService.GetSummaryAsync(fromDate, toDate, ownerId));
    }
}
