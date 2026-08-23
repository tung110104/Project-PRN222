using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPricingService
{
    /// <summary>Ngay vang ap dung cho san vao 1 ngay (uu tien ngay vang rieng cua san truoc ngay vang he thong).</summary>
    Task<GoldenDay?> GetGoldenDayAsync(int fieldId, DateOnly date);
    /// <summary>Bang gia cua tat ca slot cua san trong 1 ngay: TimeSlotId -> gia cuoi cung.</summary>
    Task<Dictionary<int, decimal>> GetSlotPricesAsync(Field field, DateOnly date);
    // ----- CRUD bang gia (chu san) -----
    Task<List<FieldPricingRule>> GetRulesAsync(int fieldId);
    Task<(bool Success, string Message)> AddRuleAsync(FieldPricingRule rule);
    Task DeleteRuleAsync(int ruleId, int fieldId);
    // ----- CRUD khung gio (chu san) -----
    /// <summary>Tat ca khung gio cua san, ke ca dang tat (trang quan ly can thay de bat lai).</summary>
    Task<List<TimeSlot>> GetAllTimeSlotsAsync(int fieldId);
    Task<(bool Success, string Message)> AddTimeSlotAsync(int fieldId, TimeOnly start, TimeOnly end);
    Task<(bool Success, string Message)> ToggleTimeSlotAsync(int timeSlotId, int fieldId);
    // ----- CRUD ngay vang -----
    Task<List<GoldenDay>> GetGoldenDaysAsync(int? fieldId);
    Task<(bool Success, string Message)> AddGoldenDayAsync(GoldenDay day);
    Task DeleteGoldenDayAsync(int goldenDayId);
}

public class PricingService : IPricingService
{
    private readonly IUnitOfWork _uow;
    public PricingService(IUnitOfWork uow) => _uow = uow;

    public Task<GoldenDay?> GetGoldenDayAsync(int fieldId, DateOnly date) =>
        _uow.GoldenDays.Query()
            .Where(g => g.Date == date && g.IsActive && (g.FieldId == fieldId || g.FieldId == null))
            .OrderByDescending(g => g.FieldId) // ngay vang rieng cua san uu tien hon ngay vang he thong
            .FirstOrDefaultAsync();

    public async Task<Dictionary<int, decimal>> GetSlotPricesAsync(Field field, DateOnly date)
    {
        // field can duoc Include TimeSlots + PricingRules truoc khi goi
        var golden = await GetGoldenDayAsync(field.FieldId, date);
        return field.TimeSlots.ToDictionary(
            s => s.TimeSlotId,
            s => PricingEngine.GetPrice(field, s, date, golden));
    }

    public Task<List<FieldPricingRule>> GetRulesAsync(int fieldId) =>
        _uow.FieldPricingRules.Query()
            .Where(r => r.FieldId == fieldId)
            .OrderByDescending(r => r.Priority).ThenBy(r => r.StartTime)
            .ToListAsync();

    public async Task<(bool Success, string Message)> AddRuleAsync(FieldPricingRule rule)
    {
        if (rule.StartTime >= rule.EndTime)
            return (false, "Giờ kết thúc phải sau giờ bắt đầu.");
        if (rule.Price <= 0)
            return (false, "Giá phải lớn hơn 0.");
        if (rule.StartMonth.HasValue != rule.EndMonth.HasValue)
            return (false, "Phải nhập đủ cả tháng bắt đầu và tháng kết thúc (hoặc bỏ trống cả hai).");

        rule.IsActive = true;
        await _uow.FieldPricingRules.AddAsync(rule);
        await _uow.SaveChangesAsync();
        return (true, "Đã thêm quy tắc giá.");
    }

    public async Task DeleteRuleAsync(int ruleId, int fieldId)
    {
        var rule = await _uow.FieldPricingRules.GetByIdAsync(ruleId);
        if (rule == null || rule.FieldId != fieldId) return;
        _uow.FieldPricingRules.Remove(rule);
        await _uow.SaveChangesAsync();
    }

    public Task<List<TimeSlot>> GetAllTimeSlotsAsync(int fieldId) =>
        _uow.TimeSlots.Query()
            .Where(t => t.FieldId == fieldId)
            .OrderBy(t => t.StartTime)
            .ToListAsync();

    public async Task<(bool Success, string Message)> AddTimeSlotAsync(int fieldId, TimeOnly start, TimeOnly end)
    {
        if (start >= end) return (false, "Giờ kết thúc phải sau giờ bắt đầu.");
        var overlap = await _uow.TimeSlots.Query()
            .AnyAsync(t => t.FieldId == fieldId && t.StartTime < end && t.EndTime > start);
        if (overlap) return (false, "Khung giờ bị chồng lấn với khung giờ đã có.");

        await _uow.TimeSlots.AddAsync(new TimeSlot { FieldId = fieldId, StartTime = start, EndTime = end });
        await _uow.SaveChangesAsync();
        return (true, "Đã thêm khung giờ.");
    }

    public async Task<(bool Success, string Message)> ToggleTimeSlotAsync(int timeSlotId, int fieldId)
    {
        var slot = await _uow.TimeSlots.GetByIdAsync(timeSlotId);
        if (slot == null || slot.FieldId != fieldId) return (false, "Không tìm thấy khung giờ.");
        slot.IsActive = !slot.IsActive;
        _uow.TimeSlots.Update(slot);
        await _uow.SaveChangesAsync();
        return (true, slot.IsActive ? "Đã mở lại khung giờ." : "Đã tắt khung giờ (không xóa để giữ lịch sử booking).");
    }

    public Task<List<GoldenDay>> GetGoldenDaysAsync(int? fieldId)
    {
        var query = _uow.GoldenDays.Query().Include(g => g.Field).AsQueryable();
        // fieldId = null: admin xem tat ca; co gia tri: ngay vang cua san do + ngay vang he thong
        if (fieldId.HasValue)
            query = query.Where(g => g.FieldId == fieldId.Value || g.FieldId == null);
        return query.OrderBy(g => g.Date).ToListAsync();
    }

    public async Task<(bool Success, string Message)> AddGoldenDayAsync(GoldenDay day)
    {
        if (day.PriceMultiplier < 1m || day.PriceMultiplier > 5m)
            return (false, "Hệ số giá phải từ 1 đến 5.");
        if (day.PointsMultiplier < 1 || day.PointsMultiplier > 10)
            return (false, "Hệ số điểm phải từ 1 đến 10.");
        var dup = await _uow.GoldenDays.Query()
            .AnyAsync(g => g.Date == day.Date && g.FieldId == day.FieldId);
        if (dup) return (false, "Ngày này đã được đánh dấu ngày vàng.");

        day.IsActive = true;
        await _uow.GoldenDays.AddAsync(day);
        await _uow.SaveChangesAsync();
        return (true, "Đã thêm ngày vàng.");
    }

    public async Task DeleteGoldenDayAsync(int goldenDayId)
    {
        var day = await _uow.GoldenDays.GetByIdAsync(goldenDayId);
        if (day == null) return;
        _uow.GoldenDays.Remove(day);
        await _uow.SaveChangesAsync();
    }
}
