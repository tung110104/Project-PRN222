using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Controllers;

// Owner quan ly san cua minh, Admin quan ly tat ca (Staff khong quan ly san - chi quan ly booking)
[Authorize(Roles = "Admin,Owner")]
public class FieldsManageController : Controller
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB/anh

    private readonly IFieldService _fieldService;
    private readonly IWebHostEnvironment _env;

    public FieldsManageController(IFieldService fieldService, IWebHostEnvironment env)
    {
        _fieldService = fieldService;
        _env = env;
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

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Field field, List<IFormFile> imageFiles)
    {
        if (string.IsNullOrWhiteSpace(field.FieldName) || field.PricePerHour <= 0 ||
            string.IsNullOrWhiteSpace(field.Province) || string.IsNullOrWhiteSpace(field.Ward))
        {
            ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
            ViewBag.Error = "Vui lòng nhập đầy đủ thông tin hợp lệ (chọn Tỉnh/Thành và Phường/Xã từ danh sách).";
            return View(field);
        }
        if (field.CashbackPercent is < 0 or > 50)
        {
            ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
            ViewBag.Error = "Cashback phải từ 0 đến 50%.";
            return View(field);
        }
        field.OwnerId = CurrentUserId;
        await _fieldService.CreateAsync(field);

        var (urls, skipped) = await SaveImageFilesAsync(imageFiles);
        await _fieldService.AddImagesAsync(field.FieldId, urls);

        TempData["Success"] = "Tạo sân thành công (đã tự tạo khung giờ 06:00-22:00). Vào \"Giá & Khung giờ\" để cấu hình bảng giá chi tiết."
            + (skipped > 0 ? $" Lưu ý: {skipped} ảnh bị bỏ qua (sai định dạng hoặc quá 5MB)." : "");
        return RedirectToAction(nameof(Index));
    }

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
    public async Task<IActionResult> Edit(Field field, List<IFormFile> imageFiles)
    {
        var existing = await _fieldService.GetDetailAsync(field.FieldId);
        if (existing == null) return NotFound();
        if (!IsAdmin && existing.OwnerId != CurrentUserId) return Forbid();
        if (field.CashbackPercent is < 0 or > 50)
        {
            ViewBag.FieldTypes = await _fieldService.GetFieldTypesAsync();
            ViewBag.Error = "Cashback phải từ 0 đến 50%.";
            return View(existing);
        }

        existing.FieldName = field.FieldName;
        existing.FieldTypeId = field.FieldTypeId;
        existing.Address = field.Address;
        existing.Ward = field.Ward;
        existing.Province = field.Province;
        existing.PricePerHour = field.PricePerHour;
        existing.AcceptWalletPayment = field.AcceptWalletPayment;
        existing.CashbackPercent = field.CashbackPercent;
        existing.Description = field.Description;
        existing.Status = field.Status;

        await _fieldService.UpdateAsync(existing);

        var (urls, skipped) = await SaveImageFilesAsync(imageFiles);
        await _fieldService.AddImagesAsync(existing.FieldId, urls);

        TempData["Success"] = "Cập nhật sân thành công."
            + (skipped > 0 ? $" Lưu ý: {skipped} ảnh bị bỏ qua (sai định dạng hoặc quá 5MB)." : "");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await _fieldService.GetImageAsync(id);
        if (image == null) return NotFound();
        if (!IsAdmin && image.Field.OwnerId != CurrentUserId) return Forbid();

        var fieldId = image.FieldId;
        var url = await _fieldService.DeleteImageAsync(id);
        DeletePhysicalFile(url);

        TempData["Success"] = "Đã xóa ảnh.";
        return RedirectToAction(nameof(Edit), new { id = fieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPrimaryImage(int id)
    {
        var image = await _fieldService.GetImageAsync(id);
        if (image == null) return NotFound();
        if (!IsAdmin && image.Field.OwnerId != CurrentUserId) return Forbid();

        await _fieldService.SetPrimaryImageAsync(image.FieldId, id);
        TempData["Success"] = "Đã đổi ảnh đại diện.";
        return RedirectToAction(nameof(Edit), new { id = image.FieldId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();
        if (!IsAdmin && field.OwnerId != CurrentUserId) return Forbid();

        var imageUrls = field.FieldImages.Select(i => i.ImageUrl).ToList();
        await _fieldService.DeleteAsync(id);

        // Chi xoa file anh khi san bi xoa han (co booking thi san chi bi dong, giu lai anh)
        if (await _fieldService.GetDetailAsync(id) == null)
            imageUrls.ForEach(DeletePhysicalFile);

        TempData["Success"] = "Đã xóa/đóng sân.";
        return RedirectToAction(nameof(Index));
    }

    // Luu cac file anh hop le vao wwwroot/uploads/fields, tra ve URL + so file bi bo qua
    private async Task<(List<string> Urls, int Skipped)> SaveImageFilesAsync(List<IFormFile>? files)
    {
        var urls = new List<string>();
        var skipped = 0;
        if (files == null || files.Count == 0) return (urls, skipped);

        var folder = Path.Combine(_env.WebRootPath, "uploads", "fields");
        Directory.CreateDirectory(folder);

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (file.Length > MaxImageSizeBytes || !AllowedImageExtensions.Contains(ext))
            {
                skipped++;
                continue;
            }

            var fileName = $"{Guid.NewGuid():N}{ext}";
            await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
            {
                await file.CopyToAsync(stream);
            }
            urls.Add($"/uploads/fields/{fileName}");
        }
        return (urls, skipped);
    }

    private void DeletePhysicalFile(string? imageUrl)
    {
        // Chi xoa file do minh upload, khong dong den anh seed /images/...
        if (imageUrl == null || !imageUrl.StartsWith("/uploads/")) return;
        var path = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}
