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

    Task<(bool Success, string Message)> UpdateProfileAsync(
        int userId,
        string fullName,
        string? phone);
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
            .FirstOrDefaultAsync(u =>
                u.UserId == userId &&
                u.IsActive);
    }

    public async Task ToggleActiveAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);

        if (user == null)
            return;

        user.IsActive = !user.IsActive;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }

    public Task<List<Role>> GetRolesAsync()
    {
        return _uow.Roles.GetAllAsync();
    }

    public async Task ChangeRoleAsync(
        int userId,
        int roleId)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return;

        // Khong duoc doi role cua Admin va khong duoc gan role Admin
        if (user.Role.RoleName == "Admin")
            return;

        var newRole = await _uow.Roles.GetByIdAsync(roleId);
        if (newRole == null || newRole.RoleName == "Admin")
            return;

        user.RoleId = roleId;

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)>
        UpdateProfileAsync(
            int userId,
            string fullName,
            string? phone)
    {
        var user = await _uow.Users.GetByIdAsync(userId);

        if (user == null || !user.IsActive)
        {
            return (
                false,
                "Không tìm thấy tài khoản."
            );
        }

        user.FullName = fullName.Trim();

        user.Phone = string.IsNullOrWhiteSpace(phone)
            ? null
            : phone.Trim();

        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        return (
            true,
            "Cập nhật hồ sơ thành công."
        );
    }
}