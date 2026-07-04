using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task ToggleActiveAsync(int userId);
    Task<List<Role>> GetRolesAsync();
    Task ChangeRoleAsync(int userId, int roleId);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    public UserService(IUnitOfWork uow) => _uow = uow;

    public Task<List<User>> GetAllAsync() =>
        _uow.Users.Query().Include(u => u.Role).OrderBy(u => u.UserId).ToListAsync();

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
}
