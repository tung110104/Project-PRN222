using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IFieldService
{
    Task<List<Field>> SearchAsync(string? keyword, int? fieldTypeId, string? province, decimal? maxPrice, int? minRating);
    Task<Field?> GetDetailAsync(int fieldId);
    Task<List<FieldType>> GetFieldTypesAsync();
    Task<List<string>> GetProvincesAsync();
    Task CreateAsync(Field field);
    Task UpdateAsync(Field field);
    Task<bool> DeleteAsync(int fieldId);
    Task<List<Field>> GetByOwnerAsync(int ownerId);
    Task<List<Field>> GetAllForAdminAsync();
    Task<double> GetAverageRatingAsync(int fieldId);
}

public class FieldService : IFieldService
{
    private readonly IUnitOfWork _uow;
    public FieldService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<Field>> SearchAsync(string? keyword, int? fieldTypeId, string? province, decimal? maxPrice, int? minRating)
    {
        var query = _uow.Fields.Query()
            .Include(f => f.FieldType)
            .Include(f => f.FieldImages)
            .Include(f => f.Reviews)
            .Where(f => f.Status == "Active");

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(f => f.FieldName.Contains(keyword) || f.Address.Contains(keyword) || f.Ward.Contains(keyword));
        if (fieldTypeId.HasValue)
            query = query.Where(f => f.FieldTypeId == fieldTypeId.Value);
        if (!string.IsNullOrWhiteSpace(province))
            query = query.Where(f => f.Province == province);
        if (maxPrice.HasValue)
            query = query.Where(f => f.PricePerHour <= maxPrice.Value);

        var fields = await query.OrderBy(f => f.PricePerHour).ToListAsync();

        if (minRating.HasValue)
            fields = fields.Where(f => f.Reviews.Any() && f.Reviews.Average(r => r.Rating) >= minRating.Value).ToList();

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

    public Task<List<string>> GetProvincesAsync() =>
        _uow.Fields.Query().Select(f => f.Province).Distinct().OrderBy(c => c).ToListAsync();

    public async Task CreateAsync(Field field)
    {
        field.CreatedAt = DateTime.Now;
        await _uow.Fields.AddAsync(field);
        await _uow.SaveChangesAsync();

        // Tu dong tao khung gio mac dinh 06:00 - 22:00 (chu san chinh sua lai trong trang "Giá & Khung giờ")
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
            field.Status = "Closed"; // co lich su dat -> chi dong san
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
}
