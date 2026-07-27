using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>Quản lý sân — role Owner (chủ sân) + Admin (mục 6: Owner tách khỏi Staff).</summary>
[Authorize(Roles = "Admin,Owner")]
public class FieldsManageController : Controller
{
    private readonly IFieldService _fieldService;
    private readonly IWebHostEnvironment _environment;

    public FieldsManageController(IFieldService fieldService, IWebHostEnvironment environment)
    {
        _fieldService = fieldService;
        _environment = environment;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");

    public async Task<IActionResult> Index()
    {
        var fields = IsAdmin
            ? await _fieldService.GetAllForAdminAsync()
            : await _fieldService.GetByOwnerAsync(CurrentUserId);
        return View(fields);
    }

    // ============================== TẠO SÂN ==============================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Field field, List<IFormFile> images)
    {
        if (string.IsNullOrWhiteSpace(field.FieldName) || field.PricePerHour <= 0 ||
            string.IsNullOrWhiteSpace(field.Street) ||
            string.IsNullOrWhiteSpace(field.ProvinceCode) ||
            string.IsNullOrWhiteSpace(field.WardCode))
        {
            ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
            ViewBag.Error = "Vui lòng nhập đủ tên sân, giá và chọn địa chỉ từ danh sách.";
            return View(field);
        }

        field.OwnerId = CurrentUserId;
        if (field.CashbackPercent is < 0 or > 100) field.CashbackPercent = 0;

        var imageResult = await SaveImagesAsync(field, images);
        if (!imageResult.Success)
        {
            ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
            ViewBag.Error = imageResult.Message;
            return View(field);
        }

        await _fieldService.CreateAsync(field);
        TempData["Success"] = "Tạo sân thành công (đã tự tạo khung giờ 06:00-22:00).";
        return RedirectToAction(nameof(Index));
    }

    // ============================== CHỈNH SỬA SÂN ==============================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();
        if (!IsAdmin && field.OwnerId != CurrentUserId) return Forbid();

        ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
        return View(field);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Field field, List<IFormFile> images)
    {
        var existing = await _fieldService.GetDetailAsync(field.FieldId);
        if (existing == null) return NotFound();
        if (!IsAdmin && existing.OwnerId != CurrentUserId) return Forbid();

        existing.FieldName = field.FieldName;
        existing.FieldTypeId = field.FieldTypeId;
        existing.Street = field.Street;
        existing.WardCode = field.WardCode;
        existing.WardName = field.WardName;
        existing.ProvinceCode = field.ProvinceCode;
        existing.ProvinceName = field.ProvinceName;
        existing.PricePerHour = field.PricePerHour;
        existing.Description = field.Description;
        existing.Status = field.Status;
        existing.AcceptWalletPayment = field.AcceptWalletPayment;   // mục 4: bật/tắt nhận ví
        existing.CashbackPercent = field.CashbackPercent is < 0 or > 100 ? 0 : field.CashbackPercent;

        var imageResult = await SaveImagesAsync(existing, images);
        if (!imageResult.Success)
        {
            ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
            ViewBag.Error = imageResult.Message;
            return View(existing);
        }

        await _fieldService.UpdateAsync(existing);
        TempData["Success"] = "Cập nhật sân thành công.";
        return RedirectToAction(nameof(Index));
    }

    // ============================== XÓA / ĐÓNG SÂN ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();
        if (!IsAdmin && field.OwnerId != CurrentUserId) return Forbid();

        await _fieldService.DeleteAsync(id);
        TempData["Success"] = "Đã xóa hoặc đóng sân.";
        return RedirectToAction(nameof(Index));
    }

    // ============================== BẢNG GIÁ THEO SÂN (mục 2) ==============================

    [HttpGet]
    public async Task<IActionResult> Rules(int id)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();
        if (!IsAdmin && field.OwnerId != CurrentUserId) return Forbid();

        ViewBag.Field = field;
        return View(await _fieldService.GetRulesAsync(id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRule(FieldPricingRule rule)
    {
        var (success, message) = await _fieldService.AddRuleAsync(rule, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Rules), new { id = rule.FieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(int id, int fieldId)
    {
        var (success, message) = await _fieldService.DeleteRuleAsync(id, CurrentUserId, IsAdmin);
        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Rules), new { id = fieldId });
    }

    // ============================== LƯU ẢNH SÂN ==============================

    private async Task<(bool Success, string Message)> SaveImagesAsync(
        Field field, List<IFormFile>? images)
    {
        if (images == null || images.Count == 0) return (true, string.Empty);

        var validImages = images.Where(image => image.Length > 0).ToList();
        if (validImages.Count == 0) return (true, string.Empty);

        if (field.FieldImages.Count + validImages.Count > 10)
            return (false, "Mỗi sân chỉ được có tối đa 10 ảnh.");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        const long maxFileSize = 5 * 1024 * 1024;

        foreach (var image in validImages)
        {
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return (false, "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP.");
            if (image.Length > maxFileSize)
                return (false, "Mỗi ảnh không được vượt quá 5 MB.");
            if (string.IsNullOrWhiteSpace(image.ContentType) ||
                !image.ContentType.StartsWith("image/"))
                return (false, "Tệp được chọn không phải là hình ảnh.");
        }

        var webRootPath = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
        Directory.CreateDirectory(webRootPath);

        var uploadFolder = Path.Combine(webRootPath, "uploads", "fields");
        Directory.CreateDirectory(uploadFolder);

        foreach (var oldImage in field.FieldImages)
            oldImage.IsPrimary = false;

        var isFirstNewImage = true;
        foreach (var image in validImages)
        {
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(uploadFolder, fileName);

            await using var stream = new FileStream(physicalPath, FileMode.Create);
            await image.CopyToAsync(stream);

            field.FieldImages.Add(new FieldImage
            {
                ImageUrl = $"/uploads/fields/{fileName}",
                IsPrimary = isFirstNewImage
            });
            isFirstNewImage = false;
        }

        return (true, string.Empty);
    }
}
