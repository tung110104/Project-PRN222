using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.Web.Models;

namespace SportsFieldBooking.Web.Controllers;

[Authorize(Roles = "Customer")]
public class ProfileController : Controller
{
    private readonly IUserService _userService;
    private readonly IBookingService _bookingService;

    public ProfileController(
        IUserService userService,
        IBookingService bookingService)
    {
        _userService = userService;
        _bookingService = bookingService;
    }

    private int CurrentUserId
    {
        get
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.Parse(userId!);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userService.GetByIdAsync(
            CurrentUserId);

        if (user == null)
            return NotFound();

        await _bookingService.CompletePastBookingsAsync();

        var bookings =
            await _bookingService.GetByUserAsync(
                CurrentUserId);

        var model = new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            CreatedAt = user.CreatedAt,
            Bookings = bookings
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadProfileDataAsync(model);
            return View(model);
        }

        var result =
            await _userService.UpdateProfileAsync(
                CurrentUserId,
                model.FullName,
                model.Phone);

        if (!result.Success)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Message);

            await LoadProfileDataAsync(model);
            return View(model);
        }

        TempData["Success"] = result.Message;

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadProfileDataAsync(
        ProfileViewModel model)
    {
        var user = await _userService.GetByIdAsync(
            CurrentUserId);

        if (user != null)
        {
            model.Email = user.Email;
            model.CreatedAt = user.CreatedAt;
        }

        model.Bookings =
            await _bookingService.GetByUserAsync(
                CurrentUserId);
    }
}