using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public record ProvinceOption(string Code, string Name);

public interface IFieldService
{
    Task<List<Field>> SearchAsync(string? keyword, int? fieldTypeId, string? provinceCode,
        string? wardCode, decimal? maxPrice, int? minRating);
    Task<Field?> GetDetailAsync(int fieldId);
    Task<List<FieldType>> GetFieldTypesAsync();

    /// <summary>Danh sách tỉnh/phường đang có sân — cho bộ lọc tìm kiếm (mục 7).</summary>
    Task<List<ProvinceOption>> GetProvincesAsync();
    Task<List<ProvinceOption>> GetWardsAsync(string provinceCode);

    Task CreateAsync(Field field);
    Task UpdateAsync(Field field);
    Task<bool> DeleteAsync(int fieldId);
    Task<List<Field>> GetByOwnerAsync(int ownerId);
    Task<List<Field>> GetAllForAdminAsync();
    Task<double> GetAverageRatingAsync(int fieldId);

    // Bảng giá theo sân (mục 2)
    Task<List<FieldPricingRule>> GetRulesAsync(int fieldId);
    Task<(bool Success, string Message)> AddRuleAsync(FieldPricingRule rule, int actorUserId, bool isAdmin);
    Task<(bool Success, string Message)> DeleteRuleAsync(int ruleId, int actorUserId, bool isAdmin);
}

public class FieldService : IFieldService
{
    private readonly IUnitOfWork _uow;
    public FieldService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<Field>> SearchAsync(string? keyword, int? fieldTypeId, string? provinceCode,
        string? wardCode, decimal? maxPrice, int? minRating)
    {
        var query = _uow.Fields.Query()
            .Include(f => f.FieldType)
            .Include(f => f.FieldImages)
            .Include(f => f.Reviews)
            .Where(f => f.Status == "Active");

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(f => f.FieldName.Contains(keyword) ||
                                     f.Street.Contains(keyword) ||
                                     f.WardName.Contains(keyword));
        if (fieldTypeId.HasValue)
            query = query.Where(f => f.FieldTypeId == fieldTypeId.Value);
        if (!string.IsNullOrWhiteSpace(provinceCode))
            query = query.Where(f => f.ProvinceCode == provinceCode);
        if (!string.IsNullOrWhiteSpace(wardCode))
            query = query.Where(f => f.WardCode == wardCode);
        if (maxPrice.HasValue)
            query = query.Where(f => f.PricePerHour <= maxPrice.Value);

        var fields = await query.OrderBy(f => f.PricePerHour).ToListAsync();

        if (minRating.HasValue)
            fields = fields.Where(f => f.Reviews.Any() &&
                                       f.Reviews.Average(r => r.Rating) >= minRating.Value).ToList();

        return fields;
    }

    public Task<Field?> GetDetailAsync(int fieldId) =>
        _uow.Fields.Query()
            .Include(f => f.FieldType)
            .Include(f => f.FieldImages)
            .Include(f => f.TimeSlots.Where(t => t.IsActive))
            .Include(f => f.PricingRules)
            .Include(f => f.Reviews).ThenInclude(r => r.User)
            .Include(f => f.Owner)
            .FirstOrDefaultAsync(f => f.FieldId == fieldId);

    public Task<List<FieldType>> GetFieldTypesAsync() => _uow.FieldTypes.GetAllAsync();

    public async Task<List<ProvinceOption>> GetProvincesAsync() =>
        (await _uow.Fields.Query()
            .Select(f => new { f.ProvinceCode, f.ProvinceName })
            .Distinct()
            .OrderBy(p => p.ProvinceName)
            .ToListAsync())
        .Select(p => new ProvinceOption(p.ProvinceCode, p.ProvinceName))
        .ToList();

    public async Task<List<ProvinceOption>> GetWardsAsync(string provinceCode) =>
        (await _uow.Fields.Query()
            .Where(f => f.ProvinceCode == provinceCode)
            .Select(f => new { f.WardCode, f.WardName })
            .Distinct()
            .OrderBy(w => w.WardName)
            .ToListAsync())
        .Select(w => new ProvinceOption(w.WardCode, w.WardName))
        .ToList();

    public async Task CreateAsync(Field field)
    {
        field.CreatedAt = DateTime.Now;
        await _uow.Fields.AddAsync(field);
        await _uow.SaveChangesAsync();

        // Tự động tạo khung giờ mặc định 06:00 - 22:00
        for (var h = 6; h < 22; h++)
        {
            await _uow.TimeSlots.AddAsync(new TimeSlot
            {
                FieldId = field.FieldId,
                StartTime = new TimeOnly(h, 0),
                EndTime = new TimeOnly(h + 1, 0)
            });
        }
        await _uow.SaveChangesAsync();
    }

    public async Task UpdateAsync(Field field)
    {
        _uow.Fields.Update(field);
        await _uow.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int fieldId)
    {
        var field = await _uow.Fields.GetByIdAsync(fieldId);
        if (field == null) return false;

        var hasBooking = await _uow.Bookings.Query().AnyAsync(b => b.FieldId == fieldId);
        if (hasBooking)
        {
            field.Status = "Closed"; // có lịch sử đặt -> chỉ đóng sân (FK Restrict)
            _uow.Fields.Update(field);
        }
        else
        {
            _uow.Fields.Remove(field);
        }
        await _uow.SaveChangesAsync();
        return true;
    }

    public Task<List<Field>> GetAllForAdminAsync() =>
        _uow.Fields.Query()
            .Include(f => f.FieldType)
            .Include(f => f.Owner)
            .OrderBy(f => f.FieldId)
            .ToListAsync();

    public Task<List<Field>> GetByOwnerAsync(int ownerId) =>
        _uow.Fields.Query()
            .Include(f => f.FieldType)
            .Where(f => f.OwnerId == ownerId)
            .ToListAsync();

    public async Task<double> GetAverageRatingAsync(int fieldId)
    {
        var ratings = await _uow.Reviews.Query()
            .Where(r => r.FieldId == fieldId)
            .Select(r => r.Rating)
            .ToListAsync();
        return ratings.Count == 0 ? 0 : ratings.Average();
    }

    // ---------------- Bảng giá theo sân (mục 2) ----------------

    public Task<List<FieldPricingRule>> GetRulesAsync(int fieldId) =>
        _uow.FieldPricingRules.Query()
            .Where(r => r.FieldId == fieldId)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

    public async Task<(bool Success, string Message)> AddRuleAsync(
        FieldPricingRule rule, int actorUserId, bool isAdmin)
    {
        var field = await _uow.Fields.GetByIdAsync(rule.FieldId);
        if (field == null) return (false, "Không tìm thấy sân.");
        if (!isAdmin && field.OwnerId != actorUserId)
            return (false, "Bạn chỉ được thêm giá cho sân của mình.");
        if (rule.Price <= 0) return (false, "Giá phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(rule.RuleName))
            return (false, "Vui lòng đặt tên luật giá.");
        if (rule.StartDate.HasValue && rule.EndDate.HasValue && rule.EndDate < rule.StartDate)
            return (false, "Khoảng ngày không hợp lệ.");
        if (rule.StartTime.HasValue && rule.EndTime.HasValue && rule.EndTime <= rule.StartTime)
            return (false, "Khung giờ không hợp lệ.");

        await _uow.FieldPricingRules.AddAsync(rule);
        await _uow.SaveChangesAsync();
        return (true, "Đã thêm luật giá.");
    }

    public async Task<(bool Success, string Message)> DeleteRuleAsync(
        int ruleId, int actorUserId, bool isAdmin)
    {
        var rule = await _uow.FieldPricingRules.Query()
            .Include(r => r.Field)
            .FirstOrDefaultAsync(r => r.PricingRuleId == ruleId);
        if (rule == null) return (false, "Không tìm thấy luật giá.");
        if (!isAdmin && rule.Field.OwnerId != actorUserId)
            return (false, "Bạn chỉ được xóa giá của sân mình.");

        _uow.FieldPricingRules.Remove(rule);
        await _uow.SaveChangesAsync();
        return (true, "Đã xóa luật giá.");
    }
}
