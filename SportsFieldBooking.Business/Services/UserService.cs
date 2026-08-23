using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task<List<User>> GetCustomersAsync();
    Task ToggleActiveAsync(int userId);
    Task<List<Role>> GetRolesAsync();
    Task ChangeRoleAsync(int userId, int roleId);
    /// <summary>Super account cap / thu hoi quyen Admin (tai khoan cuu ho).</summary>
    Task<(bool Success, string Message)> SetAdminAsync(int userId, bool grant);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    public UserService(IUnitOfWork uow) => _uow = uow;

    public Task<List<User>> GetAllAsync() =>
        _uow.Users.Query().Include(u => u.Role).Include(u => u.Wallet).OrderBy(u => u.UserId).ToListAsync();

    public Task<List<User>> GetCustomersAsync() =>
        _uow.Users.Query()
            .Where(u => u.IsActive && u.Role.RoleName == "Customer")
            .OrderBy(u => u.FullName)
            .ToListAsync();

    public async Task ToggleActiveAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return;
        user.IsActive = !user.IsActive;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }

    public Task<List<Role>> GetRolesAsync() => _uow.Roles.GetAllAsync();

    public async Task ChangeRoleAsync(int userId, int roleId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return;
        user.RoleId = roleId;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> SetAdminAsync(int userId, bool grant)
    {
        var user = await _uow.Users.Query().Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");

        var targetRole = grant ? "Admin" : "Customer";
        var role = await _uow.Roles.Query().FirstAsync(r => r.RoleName == targetRole);
        user.RoleId = role.RoleId;
        if (grant) user.IsActive = true; // cap quyen admin thi mo khoa luon (truong hop cuu ho)
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return (true, grant
            ? $"Đã cấp quyền Admin cho {user.Email}."
            : $"Đã thu hồi quyền Admin của {user.Email} (chuyển về Customer).");
    }
}
