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
    private readonly IFieldService _fieldService;
    public FieldsManageController(IFieldService fieldService) => _fieldService = fieldService;

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
    public async Task<IActionResult> Create(Field field)
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
        TempData["Success"] = "Tạo sân thành công (đã tự tạo khung giờ 06:00-22:00). Vào \"Giá & Khung giờ\" để cấu hình bảng giá chi tiết.";
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
    public async Task<IActionResult> Edit(Field field)
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
        TempData["Success"] = "Cập nhật sân thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var field = await _fieldService.GetDetailAsync(id);
        if (field == null) return NotFound();
        if (!IsAdmin && field.OwnerId != CurrentUserId) return Forbid();

        await _fieldService.DeleteAsync(id);
        TempData["Success"] = "Đã xóa/đóng sân.";
        return RedirectToAction(nameof(Index));
    }
}
