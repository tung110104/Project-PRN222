using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;

namespace SportsFieldBooking.Web.Controllers;

public class AccountController : Controller
{
    // [SUPER ACCOUNT] Tai khoan cuu ho KHONG ton tai trong database lan appsettings.json.
    // Chi luu SHA-256 hash mot chieu trong code -> doc source cung khong suy nguoc ra email/mat khau,
    // khong the bi khoa/xoa/sua qua bat ky giao dien nao. Doi mat khau = thay 2 hang so nay va build lai.
    private const string SuperEmailHash = "6635bec58a5f262a3c5466e85b3b83b32b765d71be6094009ddda3686ae0bfcd";
    private const string SuperPasswordHash = "2807ee3ac93612aaba2b8e780d9d9c529f9af33ea1ce997d71cf8da5a70c03d3";

    private readonly IAuthService _authService;

    public AccountController(IAuthService authService) => _authService = authService;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // Trang login thuong: danh cho Customer / Owner (chu san kiem nguoi truc quay).
    // Tai khoan Admin va Super Account phai dung trang login quan tri rieng (AdminLogin).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        if (IsSuperAccount(email, password))
        {
            ViewBag.Error = "Tài khoản quản trị vui lòng đăng nhập tại trang dành riêng cho quản trị.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var user = await _authService.LoginAsync(email, password);
        if (user == null)
        {
            ViewBag.Error = "Email hoặc mật khẩu không đúng.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        if (user.Role.RoleName == "Admin")
        {
            ViewBag.Error = "Tài khoản Admin vui lòng đăng nhập tại trang dành riêng cho quản trị.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        await SignInUserAsync(user);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        // Moi role co luong lam viec rieng -> dieu huong ve trang chinh cua role do
        return user.Role.RoleName switch
        {
            "Owner" => RedirectToAction("Index", "FieldsManage"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    // ---------- Trang dang nhap RIENG cho quan tri (Admin + Super Account) ----------
    [HttpGet]
    public IActionResult AdminLogin() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogin(string email, string password)
    {
        // [SUPER ACCOUNT] Doi chieu hash trong code TRUOC khi query database.
        if (IsSuperAccount(email, password))
        {
            var superClaims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "0"), // id 0 = khong co trong DB
                new(ClaimTypes.Name, "Super Account"),
                new(ClaimTypes.Email, email), // email nguoi dung vua nhap (da khop hash)
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
            return View();
        }
        if (user.Role.RoleName != "Admin")
        {
            // Trang nay chi danh cho quan tri he thong - khong tiet lo them thong tin ve tai khoan
            ViewBag.Error = "Tài khoản này không có quyền quản trị hệ thống.";
            return View();
        }

        await SignInUserAsync(user);
        return RedirectToAction("Index", "Reports");
    }

    private static bool IsSuperAccount(string email, string password) =>
        Sha256Hex(email.Trim().ToLowerInvariant()) == SuperEmailHash &&
        Sha256Hex(password) == SuperPasswordHash;

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private async Task SignInUserAsync(DataAccess.Entities.User user)
    {
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
