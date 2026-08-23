using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class HomeController : Controller
{
    private readonly IFieldService _fieldService;
    public HomeController(IFieldService fieldService) => _fieldService = fieldService;

    public async Task<IActionResult> Index(string? keyword, int? fieldTypeId, string? province, decimal? maxPrice, int? minRating)
    {
        var fields = await _fieldService.SearchAsync(keyword, fieldTypeId, province, maxPrice, minRating);
        ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
        ViewBag.Provinces = await _fieldService.GetProvincesAsync();
        ViewBag.Keyword = keyword;
        ViewBag.FieldTypeId = fieldTypeId;
        ViewBag.Province = province;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.MinRating = minRating;
        return View(fields);
    }

    public IActionResult Error() => View();
}
