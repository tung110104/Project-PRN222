using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class HomeController : Controller
{
    private readonly IFieldService _fieldService;
    public HomeController(IFieldService fieldService) => _fieldService = fieldService;

    public async Task<IActionResult> Index(string? keyword, int? fieldTypeId, string? city, decimal? maxPrice, int? minRating)
    {
        var fields = await _fieldService.SearchAsync(keyword, fieldTypeId, city, maxPrice, minRating);
        ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
        ViewBag.Cities = await _fieldService.GetCitiesAsync();
        ViewBag.Keyword = keyword;
        ViewBag.FieldTypeId = fieldTypeId;
        ViewBag.City = city;
        ViewBag.MaxPrice = maxPrice;
        ViewBag.MinRating = minRating;
        return View(fields);
    }

    public IActionResult Error() => View();
}
