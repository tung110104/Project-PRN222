using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPromotionService
{
    Task<List<Promotion>> GetAllAsync();
    Task<Promotion?> GetByIdAsync(int id);
    Task<(bool Success, string Message)> CreateAsync(Promotion promo);
    Task<(bool Success, string Message)> UpdateAsync(Promotion promo);
    Task ToggleActiveAsync(int id);
}

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _uow;
    public PromotionService(IUnitOfWork uow) => _uow = uow;

    public Task<List<Promotion>> GetAllAsync() =>
        _uow.Promotions.Query().OrderByDescending(p => p.PromotionId).ToListAsync();

    public Task<Promotion?> GetByIdAsync(int id) => _uow.Promotions.GetByIdAsync(id);

    public async Task<(bool Success, string Message)> CreateAsync(Promotion promo)
    {
        if (await _uow.Promotions.Query().AnyAsync(p => p.Code == promo.Code))
            return (false, "Mã khuyến mãi đã tồn tại.");
        if (promo.EndDate < promo.StartDate)
            return (false, "Ngày kết thúc phải sau ngày bắt đầu.");

        await _uow.Promotions.AddAsync(promo);
        await _uow.SaveChangesAsync();
        return (true, "Tạo khuyến mãi thành công.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(Promotion promo)
    {
        if (await _uow.Promotions.Query().AnyAsync(p => p.Code == promo.Code && p.PromotionId != promo.PromotionId))
            return (false, "Mã khuyến mãi đã tồn tại.");
        _uow.Promotions.Update(promo);
        await _uow.SaveChangesAsync();
        return (true, "Cập nhật khuyến mãi thành công.");
    }

    public async Task ToggleActiveAsync(int id)
    {
        var promo = await _uow.Promotions.GetByIdAsync(id);
        if (promo == null) return;
        promo.IsActive = !promo.IsActive;
        _uow.Promotions.Update(promo);
        await _uow.SaveChangesAsync();
    }
}
