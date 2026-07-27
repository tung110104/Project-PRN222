using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public AccountController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        // [SUPER ACCOUNT] Tai khoan cuu ho: doi chieu appsettings TRUOC khi query database.
        // Khong ton tai trong DB -> khong the bi khoa/xoa; dung de cap lai quyen Admin khi mat tai khoan admin.
        var superEmail = _config["SuperAccount:Email"];
        var superPassword = _config["SuperAccount:Password"];
        if (!string.IsNullOrEmpty(superEmail) &&
            string.Equals(email, superEmail, StringComparison.OrdinalIgnoreCase) &&
            password == superPassword)
        {
            var superClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "0"), // id 0 = khong co trong DB
                new(ClaimTypes.Name, "Super Account"),
                new(ClaimTypes.Email, superEmail),
                new(ClaimTypes.Role, "SuperAdmin")
            };
            var superIdentity = new ClaimsIdentity(superClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(superIdentity));
            TempData["Success"] = "Đăng nhập Super Account (tài khoản cứu hộ). Bạn có thể cấp/thu hồi quyền Admin.";
            return RedirectToAction("Index", "Users");
        }

        var user = await _authService.LoginAsync(email, password);
        if (user == null)
        {
            ViewBag.Error = "Email hoặc mật khẩu không đúng.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.RoleName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Moi role co luong lam viec rieng -> dieu huong ve trang chinh cua role do
        return user.Role.RoleName switch
        {
            "Admin" => RedirectToAction("Index", "Reports"),
            "Owner" => RedirectToAction("Index", "FieldsManage"),
            "Staff" => RedirectToAction("Index", "StaffBookings"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string fullName, string email, string password, string confirmPassword, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Vui lòng nhập đầy đủ thông tin.";
            return View();
        }
        if (password.Length < 6)
        {
            ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
            return View();
        }
        if (password != confirmPassword)
        {
            ViewBag.Error = "Xác nhận mật khẩu không khớp.";
            return View();
        }

        var (success, message) = await _authService.RegisterAsync(fullName, email, password, phone);
        if (!success)
        {
            ViewBag.Error = message;
            return View();
        }
        TempData["Success"] = message + " Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
