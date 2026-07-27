using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class HomeController : Controller
{
    private readonly IFieldService _fieldService;
    public HomeController(IFieldService fieldService) => _fieldService = fieldService;

    public async Task<IActionResult> Index(
        string? keyword, int? fieldTypeId, string? provinceCode, string? wardCode,
        decimal? maxPrice, int? minRating)
    {
        var fields = await _fieldService.SearchAsync(
            keyword, fieldTypeId, provinceCode, wardCode, maxPrice, minRating);

        ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
        ViewBag.Keyword = keyword;
        ViewBag.FieldTypeId = fieldTypeId;
        ViewBag.ProvinceCode = provinceCode;
        ViewBag.WardCode = wardCode;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.MinRating = minRating;
        return View(fields);
    }

    public IActionResult Error() => View();
}
