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
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    public AuthService(IUnitOfWork uow) => _uow = uow;

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
}
