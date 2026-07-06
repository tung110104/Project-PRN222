using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(string email, string password);
    Task<(bool Success, string Message)> RegisterAsync(
        string fullName, string email, string password, string? phone);

    Task<User?> FindActiveUserByEmailAsync(string email);

    Task<(bool Success, string Message)> ResetPasswordAsync(
        string email, string newPassword);

    string HashPassword(string password);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
   

    public AuthService(IUnitOfWork uow)
    {
        _uow = uow;
        
    }

    public string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        var hash = HashPassword(password);
        return await _uow.Users.Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u =>
                u.Email.ToLower() == normalizedEmail &&
                u.PasswordHash == hash &&
                u.IsActive);
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(
    string email, string newPassword)
    {
        var user = await FindActiveUserByEmailAsync(email);

        if (user == null)
            return (false, "Không tìm thấy tài khoản.");

        user.PasswordHash = HashPassword(newPassword);
        await _uow.SaveChangesAsync();

        return (true, "Đổi mật khẩu thành công.");
    }
    public Task<User?> FindActiveUserByEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        return _uow.Users.Query().FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalizedEmail && u.IsActive);
    }

    public async Task<(bool Success, string Message)> RegisterAsync(
        string fullName, string email, string password, string? phone)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await _uow.Users.Query().AnyAsync(u => u.Email.ToLower() == normalizedEmail))
            return (false, "Email đã được sử dụng.");

        var customerRole = await _uow.Roles.Query()
            .FirstAsync(r => r.RoleName == "Customer");

        await _uow.Users.AddAsync(new User
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = HashPassword(password),
            Phone = phone?.Trim(),
            RoleId = customerRole.RoleId,
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();
        return (true, "Đăng ký thành công.");
    }
    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();
}
