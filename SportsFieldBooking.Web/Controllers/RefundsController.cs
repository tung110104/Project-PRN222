using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

/// <summary>
/// Quan ly yeu cau hoan tien ve tai khoan ngan hang (phat sinh khi booking cua san
/// KHONG nhan vi tien ao bi huy). Chu san chi thay yeu cau cua san minh, Admin thay tat ca.
/// </summary>
[Authorize(Roles = "Admin,Owner")]
public class RefundsController : Controller
{
    private readonly IRefundService _refundService;
    public RefundsController(IRefundService refundService) => _refundService = refundService;

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("Admin");
    private int? ScopeOwnerId => IsAdmin ? null : CurrentUserId;

    public async Task<IActionResult> Index(string? status)
    {
        ViewBag.Status = status;
        ViewBag.IsAdmin = IsAdmin;
        return View(await _refundService.GetRefundRequestsAsync(ScopeOwnerId, status));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkTransferred(int id, string? note)
    {
        if (!await BelongsToScopeAsync(id)) return Forbid();
        var (ok, message) = await _refundService.MarkTransferredAsync(id, CurrentUserId, note);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note)
    {
        if (!await BelongsToScopeAsync(id)) return Forbid();
        var (ok, message) = await _refundService.RejectRefundAsync(id, CurrentUserId, note);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Chu san chi thao tac tren yeu cau thuoc san cua minh.</summary>
    private async Task<bool> BelongsToScopeAsync(int refundRequestId)
    {
        if (IsAdmin) return true;
        var mine = await _refundService.GetRefundRequestsAsync(CurrentUserId, null);
        return mine.Any(r => r.RefundRequestId == refundRequestId);
    }
}
