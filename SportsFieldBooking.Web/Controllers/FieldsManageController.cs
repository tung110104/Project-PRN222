using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class FieldsManageController : Controller
{
    private readonly IFieldService _fieldService;
    private readonly IWebHostEnvironment _environment;

    public FieldsManageController(
        IFieldService fieldService,
        IWebHostEnvironment environment)
    {
        _fieldService = fieldService;
        _environment = environment;
    }

    private int CurrentUserId =>
        int.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!);

    private bool IsAdmin =>
        User.IsInRole("Admin");

    public async Task<IActionResult> Index()
    {
        var fields = IsAdmin
            ? await _fieldService.GetAllForAdminAsync()
            : await _fieldService.GetByOwnerAsync(
                CurrentUserId);

        return View(fields);
    }

    // ==============================
    // TẠO SÂN
    // ==============================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.FieldTypes =
            await _fieldService.GetFieldTypesAsync();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Field field,
        List<IFormFile> images)
    {
        if (string.IsNullOrWhiteSpace(
                field.FieldName) ||
            field.PricePerHour <= 0)
        {
            ViewBag.FieldTypes =
                await _fieldService.GetFieldTypesAsync();

            ViewBag.Error =
                "Vui lòng nhập đầy đủ thông tin hợp lệ.";

            return View(field);
        }

        field.OwnerId = CurrentUserId;

        if (field.PeakPricePerHour <= 0)
        {
            field.PeakPricePerHour =
                field.PricePerHour;
        }

        var imageResult =
            await SaveImagesAsync(field, images);

        if (!imageResult.Success)
        {
            ViewBag.FieldTypes =
                await _fieldService.GetFieldTypesAsync();

            ViewBag.Error = imageResult.Message;

            return View(field);
        }

        await _fieldService.CreateAsync(field);

        TempData["Success"] =
            "Tạo sân và tải ảnh thành công.";

        return RedirectToAction(nameof(Index));
    }

    // ==============================
    // CHỈNH SỬA SÂN
    // ==============================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var field =
            await _fieldService.GetDetailAsync(id);

        if (field == null)
            return NotFound();

        if (!IsAdmin &&
            field.OwnerId != CurrentUserId)
        {
            return Forbid();
        }

        ViewBag.FieldTypes =
            await _fieldService.GetFieldTypesAsync();

        return View(field);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Field field,
        List<IFormFile> images)
    {
        var existing =
            await _fieldService.GetDetailAsync(
                field.FieldId);

        if (existing == null)
            return NotFound();

        if (!IsAdmin &&
            existing.OwnerId != CurrentUserId)
        {
            return Forbid();
        }

        existing.FieldName = field.FieldName;
        existing.FieldTypeId = field.FieldTypeId;
        existing.Address = field.Address;
        existing.District = field.District;
        existing.City = field.City;
        existing.PricePerHour =
            field.PricePerHour;
        existing.PeakPricePerHour =
            field.PeakPricePerHour;
        existing.Description =
            field.Description;
        existing.Status = field.Status;

        var imageResult =
            await SaveImagesAsync(
                existing,
                images);

        if (!imageResult.Success)
        {
            ViewBag.FieldTypes =
                await _fieldService.GetFieldTypesAsync();

            ViewBag.Error = imageResult.Message;

            return View(existing);
        }

        await _fieldService.UpdateAsync(existing);

        TempData["Success"] =
            "Cập nhật sân thành công.";

        return RedirectToAction(nameof(Index));
    }

    // ==============================
    // XÓA HOẶC ĐÓNG SÂN
    // ==============================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var field =
            await _fieldService.GetDetailAsync(id);

        if (field == null)
            return NotFound();

        if (!IsAdmin &&
            field.OwnerId != CurrentUserId)
        {
            return Forbid();
        }

        await _fieldService.DeleteAsync(id);

        TempData["Success"] =
            "Đã xóa hoặc đóng sân.";

        return RedirectToAction(nameof(Index));
    }

    // ==============================
    // LƯU ẢNH SÂN
    // ==============================

    private async Task<(bool Success, string Message)>
     SaveImagesAsync(
         Field field,
         List<IFormFile>? images)
    {
        if (images == null || images.Count == 0)
        {
            return (true, string.Empty);
        }

        var validImages = images
            .Where(image => image.Length > 0)
            .ToList();

        if (validImages.Count == 0)
        {
            return (true, string.Empty);
        }

        // Mỗi sân có tối đa 10 ảnh
        if (field.FieldImages.Count +
            validImages.Count > 10)
        {
            return (
                false,
                "Mỗi sân chỉ được có tối đa 10 ảnh."
            );
        }

        var allowedExtensions = new[]
        {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

        const long maxFileSize =
            5 * 1024 * 1024;

        // Kiểm tra tất cả ảnh trước khi lưu
        foreach (var image in validImages)
        {
            var extension = Path
                .GetExtension(image.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return (
                    false,
                    "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP."
                );
            }

            if (image.Length > maxFileSize)
            {
                return (
                    false,
                    "Mỗi ảnh không được vượt quá 5 MB."
                );
            }

            if (string.IsNullOrWhiteSpace(image.ContentType) ||
                !image.ContentType.StartsWith("image/"))
            {
                return (
                    false,
                    "Tệp được chọn không phải là hình ảnh."
                );
            }
        }

        // Xác định đường dẫn wwwroot
        var webRootPath = _environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            webRootPath = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot");
        }

        Directory.CreateDirectory(webRootPath);

        var uploadFolder = Path.Combine(
            webRootPath,
            "uploads",
            "fields");

        Directory.CreateDirectory(uploadFolder);

        // Bỏ trạng thái đại diện của tất cả ảnh cũ
        foreach (var oldImage in field.FieldImages)
        {
            oldImage.IsPrimary = false;
        }

        // Ảnh mới đầu tiên sẽ là ảnh đại diện
        var isFirstNewImage = true;

        foreach (var image in validImages)
        {
            var extension = Path
                .GetExtension(image.FileName)
                .ToLowerInvariant();

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var physicalPath = Path.Combine(
                uploadFolder,
                fileName);

            await using var stream = new FileStream(
                physicalPath,
                FileMode.Create);

            await image.CopyToAsync(stream);

            field.FieldImages.Add(new FieldImage
            {
                ImageUrl =
                    $"/uploads/fields/{fileName}",

                IsPrimary = isFirstNewImage
            });

            isFirstNewImage = false;
        }

        return (true, string.Empty);
    }
}