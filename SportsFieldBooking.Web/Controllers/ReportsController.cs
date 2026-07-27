using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Báo cáo: Admin/Staff xem toàn hệ thống; Owner chỉ xem sân của mình (mục 6).</summary>
[Authorize(Roles = "Admin,Owner,Staff")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    public ReportsController(IReportService reportService) => _reportService = reportService;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsOwnerOnly => User.IsInRole("Owner") && !User.IsInRole("Admin");

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Now);
        var fromDate = from ?? toDate.AddDays(-30);
        ViewBag.From = fromDate;
        ViewBag.To = toDate;

        int? ownerId = IsOwnerOnly ? CurrentUserId : null;
        ViewBag.OwnerScoped = ownerId.HasValue;
        return View(await _reportService.GetSummaryAsync(fromDate, toDate, ownerId));
    }
}
