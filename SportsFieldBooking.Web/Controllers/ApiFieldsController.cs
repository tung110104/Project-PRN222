using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// [API] Endpoint JSON phuc vu tim kiem san khong tai lai trang (AJAX o trang chu)
/// va goi y ten san khi go tu khoa. Cong khai - khong yeu cau dang nhap.
/// </summary>
[ApiController]
[Route("api/fields")]
public class ApiFieldsController : ControllerBase
{
    private readonly IFieldService _fieldService;
    private readonly IPricingService _pricingService;

    public ApiFieldsController(IFieldService fieldService, IPricingService pricingService)
    {
        _fieldService = fieldService;
        _pricingService = pricingService;
    }

    /// <summary>GET /api/fields/search?keyword=&amp;fieldTypeId=&amp;province=&amp;maxPrice=&amp;minRating=</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? keyword, int? fieldTypeId, string? province,
        decimal? maxPrice, int? minRating)
    {
        var fields = await _fieldService.SearchAsync(keyword, fieldTypeId, province, maxPrice, minRating);

        var result = fields.Select(f => new
        {
            fieldId = f.FieldId,
            fieldName = f.FieldName,
            fieldType = f.FieldType.TypeName,
            address = $"{f.Address}, {f.Ward}, {f.Province}",
            province = f.Province,
            pricePerHour = f.PricePerHour,
            priceText = $"{f.PricePerHour:N0}đ/giờ",
            acceptWallet = f.AcceptWalletPayment,
            cashbackPercent = f.CashbackPercent,
            imageUrl = f.FieldImages.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                       ?? f.FieldImages.FirstOrDefault()?.ImageUrl,
            rating = f.Reviews.Any() ? Math.Round(f.Reviews.Average(r => r.Rating), 1) : 0,
            reviewCount = f.Reviews.Count,
            detailUrl = Url.Action("Detail", "Field", new { id = f.FieldId })
        });

        return Ok(new { total = fields.Count, data = result });
    }

    /// <summary>GET /api/fields/suggest?q=... - goi y ten san khi go (toi da 8 ket qua).</summary>
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Array.Empty<object>());

        var fields = await _fieldService.SearchAsync(q, null, null, null, null);
        return Ok(fields.Take(8).Select(f => new
        {
            fieldId = f.FieldId,
            fieldName = f.FieldName,
            area = $"{f.Ward}, {f.Province}",
            priceText = $"{f.PricePerHour:N0}đ/giờ"
        }));
    }

    /// <summary>GET /api/fields/{id}/slots?date=yyyy-MM-dd - khung gio con trong kem gia cua ngay do.</summary>
    [HttpGet("{id:int}/slots")]
    public async Task<IActionResult> Slots(int id, DateOnly? date)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound(new { message = "Không tìm thấy sân." });

        var day = date ?? DateOnly.FromDateTime(DateTime.Now);
        var prices = await _pricingService.GetSlotPricesAsync(field, day);
        var golden = await _pricingService.GetGoldenDayAsync(id, day);

        return Ok(new
        {
            fieldId = field.FieldId,
            fieldName = field.FieldName,
            date = day.ToString("yyyy-MM-dd"),
            goldenDay = golden == null ? null : new { golden.Name, golden.PriceMultiplier, golden.PointsMultiplier },
            slots = field.TimeSlots.OrderBy(s => s.StartTime).Select(s => new
            {
                timeSlotId = s.TimeSlotId,
                start = s.StartTime.ToString("HH\\:mm"),
                end = s.EndTime.ToString("HH\\:mm"),
                price = prices.TryGetValue(s.TimeSlotId, out var p) ? p : field.PricePerHour
            })
        });
    }

    /// <summary>GET /api/fields/filters - du lieu do vao bo loc (loai san, tinh/thanh).</summary>
    [HttpGet("filters")]
    public async Task<IActionResult> Filters()
    {
        var types = await _fieldService.GetFieldTypesAsync();
        var provinces = await _fieldService.GetProvincesAsync();
        return Ok(new
        {
            fieldTypes = types.Select(t => new { t.FieldTypeId, t.TypeName }),
            provinces
        });
    }
}
