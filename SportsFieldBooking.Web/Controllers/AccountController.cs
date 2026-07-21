using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.Web.Models;

namespace SportsFieldBooking.Web.Controllers;

public class AccountController : Controller
{
    private const string ResetEmailKey = "PasswordReset.Email";
    private const string OtpHashKey = "PasswordReset.OtpHash";
    private const string OtpExpiresKey = "PasswordReset.OtpExpires";
    private const string OtpAttemptsKey = "PasswordReset.OtpAttempts";
    private const string OtpVerifiedKey = "PasswordReset.Verified";

    private readonly IAuthService _authService;
    private readonly IEmailSender _emailSender;

    public AccountController(
        IAuthService authService,
        IEmailSender emailSender)
    {
        _authService = authService;
        _emailSender = emailSender;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string email,
        string password,
        string? returnUrl = null)
    {
        var user = await _authService.LoginAsync(email, password);

        if (user == null)
        {
            ViewBag.Error = "Email hoặc mật khẩu không đúng.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        if (user.Role.RoleName == "Admin")
        {
            ViewBag.Error =
                "Tài khoản Admin vui lòng đăng nhập tại trang dành cho quản trị viên.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        await SignInUserAsync(user);

        if (!string.IsNullOrEmpty(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return user.Role.RoleName switch
        {
            "Staff" => RedirectToAction("Index", "StaffBookings"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    // =========================
    // ĐĂNG NHẬP ADMIN
    // =========================

    [HttpGet]
    public IActionResult AdminLogin(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogin(
        string email,
        string password,
        string? returnUrl = null)
    {
        var user = await _authService.LoginAsync(email, password);

        if (user == null || user.Role.RoleName != "Admin")
        {
            ViewBag.Error =
                "Thông tin đăng nhập không đúng hoặc tài khoản không có quyền quản trị.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        await SignInUserAsync(user);

        if (!string.IsNullOrEmpty(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Reports");
    }

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.RoleName)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        string fullName,
        string email,
        string password,
        string confirmPassword,
        string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
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

        var (success, message) =
            await _authService.RegisterAsync(
                fullName,
                email,
                password,
                phone);

        if (!success)
        {
            ViewBag.Error = message;
            return View();
        }

        TempData["Success"] =
            message + " Vui lòng đăng nhập.";

        return RedirectToAction(nameof(Login));
    }

    // =========================
    // QUÊN MẬT KHẨU
    // =========================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ClearPasswordResetSession();

        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user =
            await _authService.FindActiveUserByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email chưa được đăng ký.");

            return View(model);
        }

        var sendResult =
            await SendOtpAndSaveSessionAsync(user);

        if (!sendResult.Success)
        {
            ModelState.AddModelError(
                string.Empty,
                sendResult.Message);

            return View(model);
        }

        TempData["Info"] = sendResult.Message;

        return RedirectToAction(nameof(VerifyOtp));
    }

    // =========================
    // XÁC THỰC OTP
    // =========================

    [HttpGet]
    public IActionResult VerifyOtp()
    {
        var email =
            HttpContext.Session.GetString(ResetEmailKey);

        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new VerifyOtpViewModel
        {
            Email = email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyOtp(
        VerifyOtpViewModel model)
    {
        var email =
            HttpContext.Session.GetString(ResetEmailKey);

        var savedOtpHash =
            HttpContext.Session.GetString(OtpHashKey);

        var expiresValue =
            HttpContext.Session.GetString(OtpExpiresKey);

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(savedOtpHash) ||
            !long.TryParse(expiresValue, out var expiresAt))
        {
            TempData["Error"] =
                "Phiên OTP không tồn tại. Vui lòng gửi lại mã.";

            return RedirectToAction(nameof(ForgotPassword));
        }

        model.Email = email;
        ModelState.Remove(nameof(model.Email));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var currentTime =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (currentTime > expiresAt)
        {
            ClearOtpSession();

            ModelState.AddModelError(
                nameof(model.Otp),
                "Mã OTP đã hết hạn. Vui lòng gửi lại mã.");

            return View(model);
        }

        var attempts =
            HttpContext.Session.GetInt32(OtpAttemptsKey) ?? 0;

        var enteredOtpHash =
            HashValue(model.Otp.Trim());

        if (savedOtpHash != enteredOtpHash)
        {
            attempts++;

            HttpContext.Session.SetInt32(
                OtpAttemptsKey,
                attempts);

            if (attempts >= 5)
            {
                ClearOtpSession();

                ModelState.AddModelError(
                    nameof(model.Otp),
                    "Bạn đã nhập sai 5 lần. Vui lòng yêu cầu OTP mới.");
            }
            else
            {
                ModelState.AddModelError(
                    nameof(model.Otp),
                    $"Mã OTP không chính xác. Còn {5 - attempts} lần thử.");
            }

            return View(model);
        }

        ClearOtpSession();

        HttpContext.Session.SetString(
            OtpVerifiedKey,
            "true");

        return RedirectToAction(nameof(ResetPassword));
    }

    // =========================
    // GỬI LẠI OTP
    // =========================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        var email =
            HttpContext.Session.GetString(ResetEmailKey);

        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user =
            await _authService.FindActiveUserByEmailAsync(email);

        if (user == null)
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        var sendResult =
            await SendOtpAndSaveSessionAsync(user);

        TempData[
            sendResult.Success ? "Info" : "Error"
        ] = sendResult.Message;

        return RedirectToAction(nameof(VerifyOtp));
    }

    // =========================
    // ĐẶT LẠI MẬT KHẨU
    // =========================

    [HttpGet]
    public IActionResult ResetPassword()
    {
        var email =
            HttpContext.Session.GetString(ResetEmailKey);

        var verified =
            HttpContext.Session.GetString(OtpVerifiedKey);

        if (string.IsNullOrWhiteSpace(email) ||
            verified != "true")
        {
            TempData["Error"] =
                "Vui lòng xác thực OTP trước khi đặt lại mật khẩu.";

            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model)
    {
        var email =
            HttpContext.Session.GetString(ResetEmailKey);

        var verified =
            HttpContext.Session.GetString(OtpVerifiedKey);

        if (string.IsNullOrWhiteSpace(email) ||
            verified != "true")
        {
            TempData["Error"] =
                "Phiên đặt lại mật khẩu đã hết hạn.";

            return RedirectToAction(nameof(ForgotPassword));
        }

        model.Email = email;
        ModelState.Remove(nameof(model.Email));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result =
            await _authService.ResetPasswordAsync(
                email,
                model.NewPassword);

        if (!result.Success)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Message);

            return View(model);
        }

        ClearPasswordResetSession();

        TempData["Success"] = result.Message;

        return RedirectToAction(nameof(Login));
    }

    // =========================
    // ĐĂNG XUẤT
    // =========================

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    // =========================
    // HÀM HỖ TRỢ OTP
    // =========================

    private async Task<(bool Success, string Message)>
        SendOtpAndSaveSessionAsync(User user)
    {
        var otp = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();

        try
        {
            await _emailSender.SendPasswordResetOtpAsync(
                user.Email,
                user.FullName,
                otp);
        }
        catch
        {
            return (
                false,
                "Không thể gửi OTP. Vui lòng kiểm tra cấu hình Gmail."
            );
        }

        HttpContext.Session.SetString(
            ResetEmailKey,
            user.Email.Trim().ToLowerInvariant());

        HttpContext.Session.SetString(
            OtpHashKey,
            HashValue(otp));

        var expiresAt = DateTimeOffset.UtcNow
            .AddMinutes(5)
            .ToUnixTimeSeconds();

        HttpContext.Session.SetString(
            OtpExpiresKey,
            expiresAt.ToString());

        HttpContext.Session.SetInt32(
            OtpAttemptsKey,
            0);

        HttpContext.Session.Remove(OtpVerifiedKey);

        return (
            true,
            "Mã OTP đã được gửi đến Gmail của bạn."
        );
    }

    private void ClearOtpSession()
    {
        HttpContext.Session.Remove(OtpHashKey);
        HttpContext.Session.Remove(OtpExpiresKey);
        HttpContext.Session.Remove(OtpAttemptsKey);
    }

    private void ClearPasswordResetSession()
    {
        ClearOtpSession();

        HttpContext.Session.Remove(ResetEmailKey);
        HttpContext.Session.Remove(OtpVerifiedKey);
    }

    private static string HashValue(string value)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return Convert
            .ToHexString(bytes)
            .ToLowerInvariant();
    }
}