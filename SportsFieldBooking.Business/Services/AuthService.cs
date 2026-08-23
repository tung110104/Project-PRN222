using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);
    Task<(bool Success, string Message)> RegisterAsync(string fullName, string email, string password, string? phone);
    string HashPassword(string password);

    /// <summary>
    /// [ADMIN TU APPSETTINGS] Lay user Admin ung voi email cau hinh trong AdminAccount cua appsettings.json;
    /// chua co thi tu tao (role Admin). Tra ve null neu email da thuoc mot tai khoan KHONG phai Admin.
    /// Mat khau da duoc doi chieu voi appsettings truoc khi goi ham nay.
    /// </summary>
    Task<User?> GetOrCreateConfiguredAdminAsync(string email, string configuredPassword);

    // ----- Quen mat khau (chi Customer/Owner - Admin dung Super Account de cuu ho) -----
    /// <summary>Tao ma OTP 6 so, luu DB va gui qua email. Luon tra ve thong bao chung de khong lo email nao ton tai.</summary>
    Task<(bool Success, string Message)> RequestPasswordResetAsync(string email);
    /// <summary>Kiem tra OTP con hieu luc (chua dung, chua het han, chua qua 5 lan sai).</summary>
    Task<(bool Success, string Message)> VerifyOtpAsync(string email, string otp);
    /// <summary>Doi mat khau moi sau khi OTP hop le.</summary>
    Task<(bool Success, string Message)> ResetPasswordAsync(string email, string otp, string newPassword);

    // ----- Ho so ca nhan -----
    Task<User?> GetProfileAsync(int userId);
    Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, string? phone);
    /// <summary>Cap nhat tai khoan ngan hang de nhan tien hoan khi san khong nhan vi tien ao.</summary>
    Task<(bool Success, string Message)> UpdateBankAccountAsync(int userId, string? bankName,
        string? accountNumber, string? accountHolder);
    Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;

    public AuthService(IUnitOfWork uow, IEmailService emailService)
    {
        _uow = uow;
        _emailService = emailService;
    }

    public string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // Tra ve ca tai khoan bi khoa (IsActive = false) de noi goi hien thong bao
    // "tai khoan bi khoa" ro rang thay vi "sai mat khau"
    public async Task<User?> LoginAsync(string email, string password)
    {
        var hash = HashPassword(password);
        return await _uow.Users.Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == hash);
    }

    public async Task<User?> GetOrCreateConfiguredAdminAsync(string email, string configuredPassword)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
            return user.Role.RoleName == "Admin" ? user : null; // email dang thuoc tai khoan thuong -> tu choi

        var adminRole = await _uow.Roles.Query().FirstAsync(r => r.RoleName == "Admin");
        user = new User
        {
            FullName = "Quản trị viên",
            Email = email,
            PasswordHash = HashPassword(configuredPassword), // dong bo DB voi mat khau cau hinh
            RoleId = adminRole.RoleId,
            IsActive = true,
            CreatedAt = DateTime.Now,
            Role = adminRole
        };
        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();
        return user;
    }

    public async Task<(bool Success, string Message)> RegisterAsync(string fullName, string email, string password, string? phone)
    {
        if (await _uow.Users.Query().AnyAsync(u => u.Email == email))
            return (false, "Email đã được sử dụng.");

        var customerRole = await _uow.Roles.Query().FirstAsync(r => r.RoleName == "Customer");
        await _uow.Users.AddAsync(new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = HashPassword(password),
            Phone = phone,
            RoleId = customerRole.RoleId,
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();
        return (true, "Đăng ký thành công.");
    }

    // ================= QUEN MAT KHAU =================
    private const int OtpValidMinutes = 10;
    private const int MaxOtpAttempts = 5;
    private const string GenericResetMessage =
        "Nếu email tồn tại trong hệ thống, mã xác thực đã được gửi. Vui lòng kiểm tra hộp thư.";

    /// <summary>Tai khoan duoc phep dung luong quen mat khau (khong cho Admin - dung Super Account de cuu ho).</summary>
    private static bool CanSelfReset(User user) => user.Role.RoleName is "Customer" or "Owner";

    public async Task<(bool Success, string Message)> RequestPasswordResetAsync(string email)
    {
        email = (email ?? "").Trim();
        var user = await _uow.Users.Query().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        // Khong tiet lo email co ton tai hay khong (chong do email)
        if (user == null || !user.IsActive || !CanSelfReset(user))
            return (true, GenericResetMessage);

        // Vo hieu cac OTP cu chua dung cua user nay
        var oldOtps = await _uow.PasswordResetOtps.Query()
            .Where(o => o.UserId == user.UserId && !o.IsUsed).ToListAsync();
        foreach (var o in oldOtps) o.IsUsed = true;

        var code = Random.Shared.Next(100000, 1000000).ToString();
        await _uow.PasswordResetOtps.AddAsync(new PasswordResetOtp
        {
            UserId = user.UserId,
            OtpCode = code,
            ExpiresAt = DateTime.Now.AddMinutes(OtpValidMinutes),
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();

        await _emailService.SendAsync(user.Email, "[SportBooking] Mã xác thực đặt lại mật khẩu",
            $"""
            <h3>Xin chào {user.FullName},</h3>
            <p>Bạn vừa yêu cầu đặt lại mật khẩu tài khoản SportBooking. Mã xác thực của bạn là:</p>
            <div style="border:2px dashed #198754;border-radius:8px;padding:16px;text-align:center;margin:16px 0;">
                <h2 style="letter-spacing:6px;color:#198754;margin:0;">{code}</h2>
            </div>
            <p>Mã có hiệu lực trong <strong>{OtpValidMinutes} phút</strong> và chỉ dùng được một lần.</p>
            <p>Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này - tài khoản của bạn vẫn an toàn.</p>
            <p>SportBooking - Hệ thống đặt sân thể thao</p>
            """);

        return (true, GenericResetMessage);
    }

    /// <summary>Lay OTP moi nhat con hieu luc; dong thoi tra ve user de cac buoc sau dung lai.</summary>
    private async Task<(PasswordResetOtp? Otp, User? User, string? Error)> FindValidOtpAsync(string email, string otp)
    {
        email = (email ?? "").Trim();
        var user = await _uow.Users.Query().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !user.IsActive || !CanSelfReset(user))
            return (null, null, "Mã xác thực không đúng hoặc đã hết hạn.");

        var record = await _uow.PasswordResetOtps.Query()
            .Where(o => o.UserId == user.UserId && !o.IsUsed)
            .OrderByDescending(o => o.PasswordResetOtpId)
            .FirstOrDefaultAsync();

        if (record == null) return (null, user, "Mã xác thực không đúng hoặc đã hết hạn.");
        if (record.ExpiresAt < DateTime.Now)
        {
            record.IsUsed = true;
            await _uow.SaveChangesAsync();
            return (null, user, "Mã xác thực đã hết hạn. Vui lòng yêu cầu mã mới.");
        }
        if (record.AttemptCount >= MaxOtpAttempts)
        {
            record.IsUsed = true;
            await _uow.SaveChangesAsync();
            return (null, user, "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu mã mới.");
        }
        if (record.OtpCode != (otp ?? "").Trim())
        {
            record.AttemptCount++;
            await _uow.SaveChangesAsync();
            var left = MaxOtpAttempts - record.AttemptCount;
            return (null, user, $"Mã xác thực không đúng. Bạn còn {left} lần thử.");
        }
        return (record, user, null);
    }

    public async Task<(bool Success, string Message)> VerifyOtpAsync(string email, string otp)
    {
        var (record, _, error) = await FindValidOtpAsync(email, otp);
        return record == null ? (false, error!) : (true, "Mã xác thực hợp lệ.");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(string email, string otp, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "Mật khẩu mới phải có ít nhất 6 ký tự.");

        var (record, user, error) = await FindValidOtpAsync(email, otp);
        if (record == null || user == null) return (false, error!);

        user.PasswordHash = HashPassword(newPassword);
        _uow.Users.Update(user);
        record.IsUsed = true;
        await _uow.SaveChangesAsync();

        await _emailService.SendAsync(user.Email, "[SportBooking] Mật khẩu đã được thay đổi",
            $"<p>Xin chào {user.FullName},</p><p>Mật khẩu tài khoản SportBooking của bạn vừa được đặt lại thành công " +
            $"lúc {DateTime.Now:HH:mm dd/MM/yyyy}. Nếu không phải bạn thực hiện, vui lòng liên hệ quản trị viên ngay.</p>");

        return (true, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }

    // ================= HO SO CA NHAN =================
    public Task<User?> GetProfileAsync(int userId) =>
        _uow.Users.Query().Include(u => u.Role).Include(u => u.Wallet)
            .FirstOrDefaultAsync(u => u.UserId == userId);

    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (false, "Họ tên không được để trống.");
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (phone != null && (phone.Length < 9 || phone.Length > 15 || !phone.All(char.IsDigit)))
            return (false, "Số điện thoại phải gồm 9-15 chữ số.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");

        user.FullName = fullName.Trim();
        user.Phone = phone;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return (true, "Cập nhật thông tin cá nhân thành công.");
    }

    public async Task<(bool Success, string Message)> UpdateBankAccountAsync(int userId, string? bankName,
        string? accountNumber, string? accountHolder)
    {
        bankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
        accountNumber = string.IsNullOrWhiteSpace(accountNumber) ? null : accountNumber.Trim().Replace(" ", "");
        accountHolder = string.IsNullOrWhiteSpace(accountHolder) ? null : accountHolder.Trim().ToUpperInvariant();

        var filled = new[] { bankName, accountNumber, accountHolder }.Count(x => x != null);
        if (filled is > 0 and < 3)
            return (false, "Vui lòng nhập đủ cả ngân hàng, số tài khoản và tên chủ tài khoản.");
        if (accountNumber != null && (accountNumber.Length < 6 || accountNumber.Length > 20 || !accountNumber.All(char.IsDigit)))
            return (false, "Số tài khoản phải gồm 6-20 chữ số.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");

        user.BankName = bankName;
        user.BankAccountNumber = accountNumber;
        user.BankAccountHolder = accountHolder;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return (true, filled == 0
            ? "Đã xóa thông tin tài khoản ngân hàng."
            : "Cập nhật tài khoản ngân hàng thành công. Tiền hoàn của sân không nhận ví sẽ được chuyển về tài khoản này.");
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (false, "Mật khẩu mới phải có ít nhất 6 ký tự.");

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        if (user.PasswordHash != HashPassword(currentPassword ?? ""))
            return (false, "Mật khẩu hiện tại không đúng.");
        if (currentPassword == newPassword)
            return (false, "Mật khẩu mới phải khác mật khẩu hiện tại.");

        user.PasswordHash = HashPassword(newPassword);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return (true, "Đổi mật khẩu thành công.");
    }
}
