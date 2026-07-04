using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    public ReportsController(IReportService reportService) => _reportService = reportService;

    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to)
    {
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Now);
        var fromDate = from ?? toDate.AddDays(-30);
        ViewBag.From = fromDate;
        ViewBag.To = toDate;
        return View(await _reportService.GetSummaryAsync(fromDate, toDate));
    }
}
