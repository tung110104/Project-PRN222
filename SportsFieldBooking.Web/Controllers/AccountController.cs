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
    // (Tai khoan ADMIN thi nguoc lai: cau hinh trong appsettings.json - xem AdminLogin ben duoi.)
    private const string SuperEmailHash = "27cbfffc12a8cf6ebfeb875859ef17e04feb0965cd208affff7d2de2b43a9292";
    private const string SuperPasswordHash = "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92";

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
        if (!user.IsActive)
        {
            ViewBag.Error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.";
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

        // [ADMIN TU APPSETTINGS] Tai khoan Admin cau hinh trong appsettings.json (AdminAccount:Email/Password).
        // Khop cau hinh -> gan voi user Admin tuong ung trong DB (tu tao neu chua co) de moi nghiep vu
        // can UserId (dat ho, xu ly hoan tien, nhan thong bao...) van hoat dong day du.
        var cfgEmail = _config["AdminAccount:Email"];
        var cfgPassword = _config["AdminAccount:Password"];
        if (!string.IsNullOrWhiteSpace(cfgEmail) && !string.IsNullOrWhiteSpace(cfgPassword)
            && email.Trim().Equals(cfgEmail.Trim(), StringComparison.OrdinalIgnoreCase)
            && password == cfgPassword)
        {
            var admin = await _authService.GetOrCreateConfiguredAdminAsync(cfgEmail.Trim(), cfgPassword);
            if (admin == null)
            {
                ViewBag.Error = "Email trong cấu hình AdminAccount đang thuộc về một tài khoản không phải Admin.";
                return View();
            }
            if (!admin.IsActive)
            {
                ViewBag.Error = "Tài khoản Admin đã bị khóa. Dùng Super Account để mở khóa.";
                return View();
            }
            await SignInUserAsync(admin);
            return RedirectToAction("Index", "Reports");
        }

        var user = await _authService.LoginAsync(email, password);
        if (user == null)
        {
            ViewBag.Error = "Email hoặc mật khẩu không đúng.";
            return View();
        }
        if (!user.IsActive)
        {
            ViewBag.Error = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.";
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

    // ---------- QUEN MAT KHAU (Customer / Owner) ----------
    // Admin va Super Account khong dung luong nay: mat tai khoan Admin thi dung Super Account de cap lai quyen.
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var (_, message) = await _authService.RequestPasswordResetAsync(email);
        TempData["Success"] = message;
        return RedirectToAction(nameof(VerifyOtp), new { email });
    }

    [HttpGet]
    public IActionResult VerifyOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(ForgotPassword));
        ViewBag.Email = email;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(string email, string otp)
    {
        var (ok, message) = await _authService.VerifyOtpAsync(email, otp);
        if (!ok)
        {
            ViewBag.Email = email;
            ViewBag.Error = message;
            return View();
        }
        return RedirectToAction(nameof(ResetPassword), new { email, otp });
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string otp)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
            return RedirectToAction(nameof(ForgotPassword));
        ViewBag.Email = email;
        ViewBag.Otp = otp;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string email, string otp, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            ViewBag.Email = email; ViewBag.Otp = otp;
            ViewBag.Error = "Xác nhận mật khẩu không khớp.";
            return View();
        }

        var (ok, message) = await _authService.ResetPasswordAsync(email, otp, newPassword);
        if (!ok)
        {
            ViewBag.Email = email; ViewBag.Otp = otp;
            ViewBag.Error = message;
            return View();
        }
        TempData["Success"] = message;
        return RedirectToAction(nameof(Login));
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();
}
