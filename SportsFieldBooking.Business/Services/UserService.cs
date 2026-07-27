using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int userId);
    Task ToggleActiveAsync(int userId);
    Task<List<Role>> GetRolesAsync();
    Task ChangeRoleAsync(int userId, int roleId);
    Task<(bool Success, string Message)> UpdateProfileAsync(int userId, string fullName, string? phone);

    /// <summary>Danh sách khách hàng đang hoạt động — cho chức năng đặt hộ (mục 10).</summary>
    Task<List<User>> GetCustomersAsync();

    /// <summary>Super Account cấp / thu hồi quyền Admin (mục 3).</summary>
    Task<(bool Success, string Message)> GrantAdminAsync(int userId);
    Task<(bool Success, string Message)> RevokeAdminAsync(int userId);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public Task<List<User>> GetAllAsync()
    {
        return _uow.Users.Query()
            .Include(u => u.Role)
            .OrderBy(u => u.UserId)
            .ToListAsync();
    }

    public Task<User?> GetByIdAsync(int userId)
    {
        return _uow.Users.Query()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
    }

    public Task<List<User>> GetCustomersAsync()
    {
        return _uow.Users.Query()
            .Where(u => u.Role.RoleName == "Customer" && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();
    }

    public async Task ToggleActiveAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return;

        user.IsActive = !user.IsActive;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }

    public Task<List<Role>> GetRolesAsync()
    {
        return _uow.Roles.GetAllAsync();
    }

    public async Task ChangeRoleAsync(int userId, int roleId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return;

        user.RoleId = roleId;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(
        int userId, string fullName, string? phone)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null || !user.IsActive)
            return (false, "Không tìm thấy tài khoản.");

        user.FullName = fullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return (true, "Cập nhật hồ sơ thành công.");
    }

    // ---------------- Super Account (mục 3) ----------------

    public async Task<(bool Success, string Message)> GrantAdminAsync(int userId)
    {
        var user = await _uow.Users.Query().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");

        var adminRole = await _uow.Roles.Query().FirstOrDefaultAsync(r => r.RoleName == "Admin");
        if (adminRole == null) return (false, "Chưa có role Admin trong hệ thống.");
        if (user.RoleId == adminRole.RoleId) return (false, "Tài khoản này đã là Admin.");

        user.RoleId = adminRole.RoleId;
        user.IsActive = true;   // cứu hộ: mở khóa luôn nếu đang bị khóa
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return (true, $"Đã cấp quyền Admin cho {user.Email}.");
    }

    public async Task<(bool Success, string Message)> RevokeAdminAsync(int userId)
    {
        var user = await _uow.Users.Query().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        if (user.Role.RoleName != "Admin") return (false, "Tài khoản này không phải Admin.");

        var customerRole = await _uow.Roles.Query().FirstAsync(r => r.RoleName == "Customer");
        user.RoleId = customerRole.RoleId;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return (true, $"Đã thu hồi quyền Admin của {user.Email} (chuyển về Customer).");
    }
}
