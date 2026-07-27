using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IGoldenDayService
{
    Task<List<GoldenDay>> GetAllAsync();
    Task<List<GoldenDay>> GetByOwnerAsync(int ownerId);
    Task<(bool Success, string Message)> CreateAsync(GoldenDay day, int actorUserId, bool isAdmin);
    Task<(bool Success, string Message)> ToggleActiveAsync(int id, int actorUserId, bool isAdmin);
    Task<(bool Success, string Message)> DeleteAsync(int id, int actorUserId, bool isAdmin);
}

public class GoldenDayService : IGoldenDayService
{
    private readonly IUnitOfWork _uow;
    public GoldenDayService(IUnitOfWork uow) => _uow = uow;

    public Task<List<GoldenDay>> GetAllAsync() =>
        _uow.GoldenDays.Query()
            .Include(g => g.Field)
            .OrderByDescending(g => g.Date)
            .ToListAsync();

    public Task<List<GoldenDay>> GetByOwnerAsync(int ownerId) =>
        _uow.GoldenDays.Query()
            .Include(g => g.Field)
            .Where(g => g.FieldId != null && g.Field!.OwnerId == ownerId)
            .OrderByDescending(g => g.Date)
            .ToListAsync();

    public async Task<(bool Success, string Message)> CreateAsync(GoldenDay day, int actorUserId, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(day.Name))
            return (false, "Vui lòng đặt tên ngày vàng.");
        if (day.PointMultiplier < 1) day.PointMultiplier = 1;
        if (day.DiscountPercent is < 0 or > 100)
            return (false, "% ưu đãi phải từ 0 đến 100.");

        // Chủ sân chỉ tạo ngày vàng cho SÂN CỦA MÌNH; ngày vàng toàn hệ thống là quyền Admin (mục 1)
        if (!isAdmin)
        {
            if (!day.FieldId.HasValue)
                return (false, "Chủ sân phải chọn sân cụ thể (ngày vàng toàn hệ thống do Admin tạo).");
            var owns = await _uow.Fields.Query()
                .AnyAsync(f => f.FieldId == day.FieldId.Value && f.OwnerId == actorUserId);
            if (!owns) return (false, "Sân được chọn không thuộc quyền của bạn.");
        }

        var dup = await _uow.GoldenDays.Query()
            .AnyAsync(g => g.Date == day.Date && g.FieldId == day.FieldId);
        if (dup) return (false, "Ngày này đã được cấu hình ngày vàng.");

        await _uow.GoldenDays.AddAsync(day);
        await _uow.SaveChangesAsync();
        return (true, $"Đã thêm ngày vàng {day.Date:dd/MM/yyyy}.");
    }

    private async Task<(GoldenDay? Day, string Message)> GetWithPermissionAsync(
        int id, int actorUserId, bool isAdmin)
    {
        var day = await _uow.GoldenDays.Query()
            .Include(g => g.Field)
            .FirstOrDefaultAsync(g => g.GoldenDayId == id);
        if (day == null) return (null, "Không tìm thấy ngày vàng.");
        if (!isAdmin && (day.FieldId == null || day.Field!.OwnerId != actorUserId))
            return (null, "Bạn chỉ được thao tác trên ngày vàng của sân mình.");
        return (day, "");
    }

    public async Task<(bool Success, string Message)> ToggleActiveAsync(int id, int actorUserId, bool isAdmin)
    {
        var (day, msg) = await GetWithPermissionAsync(id, actorUserId, isAdmin);
        if (day == null) return (false, msg);

        day.IsActive = !day.IsActive;
        _uow.GoldenDays.Update(day);
        await _uow.SaveChangesAsync();
        return (true, day.IsActive ? "Đã bật ngày vàng." : "Đã tắt ngày vàng.");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(int id, int actorUserId, bool isAdmin)
    {
        var (day, msg) = await GetWithPermissionAsync(id, actorUserId, isAdmin);
        if (day == null) return (false, msg);

        _uow.GoldenDays.Remove(day);
        await _uow.SaveChangesAsync();
        return (true, "Đã xóa ngày vàng.");
    }
}
