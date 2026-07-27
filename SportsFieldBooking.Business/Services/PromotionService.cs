using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPromotionService
{
    /// <summary>ownerId = null: Admin xem tat ca; co gia tri: chu san chi xem ma cua minh.</summary>
    Task<List<Promotion>> GetAllAsync(int? ownerId);
    Task<Promotion?> GetByIdAsync(int id);
    Task<(bool Success, string Message)> CreateAsync(Promotion promo);
    Task<(bool Success, string Message)> UpdateAsync(Promotion promo);
    Task ToggleActiveAsync(int id);
    /// <summary>
    /// Gui ma khuyen mai qua email (khong phat offline): ma cua chu san -> gui cho khach da tung dat san cua ho;
    /// ma he thong -> gui cho tat ca khach hang dang hoat dong. Tra ve so email da gui.
    /// </summary>
    Task<(bool Success, string Message, int SentCount)> SendPromotionEmailAsync(int promotionId, int? requestOwnerId);
}

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;

    public PromotionService(IUnitOfWork uow, IEmailService emailService)
    {
        _uow = uow;
        _emailService = emailService;
    }

    public Task<List<Promotion>> GetAllAsync(int? ownerId)
    {
        var query = _uow.Promotions.Query().Include(p => p.Owner).AsQueryable();
        if (ownerId.HasValue)
            query = query.Where(p => p.OwnerId == ownerId.Value);
        return query.OrderByDescending(p => p.PromotionId).ToListAsync();
    }

    public Task<Promotion?> GetByIdAsync(int id) => _uow.Promotions.GetByIdAsync(id);

    public async Task<(bool Success, string Message)> CreateAsync(Promotion promo)
    {
        if (await _uow.Promotions.Query().AnyAsync(p => p.Code == promo.Code))
            return (false, "Mã khuyến mãi đã tồn tại.");
        if (promo.EndDate < promo.StartDate)
            return (false, "Ngày kết thúc phải sau ngày bắt đầu.");

        await _uow.Promotions.AddAsync(promo);
        await _uow.SaveChangesAsync();
        return (true, "Tạo khuyến mãi thành công. Hãy dùng nút \"Gửi email\" để gửi mã cho khách hàng.");
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

    public async Task<(bool Success, string Message, int SentCount)> SendPromotionEmailAsync(int promotionId, int? requestOwnerId)
    {
        var promo = await _uow.Promotions.GetByIdAsync(promotionId);
        if (promo == null) return (false, "Không tìm thấy khuyến mãi.", 0);
        // Chu san chi duoc gui ma cua chinh minh (Admin: requestOwnerId = null gui duoc moi ma)
        if (requestOwnerId.HasValue && promo.OwnerId != requestOwnerId.Value)
            return (false, "Bạn chỉ được gửi mã khuyến mãi của mình.", 0);
        if (!promo.IsActive || promo.EndDate < DateOnly.FromDateTime(DateTime.Now))
            return (false, "Khuyến mãi đã hết hạn hoặc đang tạm dừng.", 0);

        // Ma cua chu san: gui cho khach da tung dat san cua chu do; ma he thong: gui moi khach hang active
        List<string> emails;
        if (promo.OwnerId.HasValue)
        {
            emails = await _uow.Bookings.Query()
                .Where(b => b.Field.OwnerId == promo.OwnerId.Value && b.Status != "Cancelled")
                .Select(b => b.User.Email)
                .Distinct()
                .ToListAsync();
            if (emails.Count == 0)
                return (false, "Chưa có khách hàng nào từng đặt sân của bạn để gửi mã.", 0);
        }
        else
        {
            emails = await _uow.Users.Query()
                .Where(u => u.IsActive && u.Role.RoleName == "Customer")
                .Select(u => u.Email)
                .ToListAsync();
        }

        var body =
            $"Tặng bạn mã khuyến mãi <b style=\"font-size:18px\">{promo.Code}</b> - giảm {promo.DiscountPercent}%" +
            (promo.MaxDiscount > 0 ? $" (tối đa {promo.MaxDiscount:N0}đ)" : "") + ".<br/>" +
            $"{promo.Description}<br/>" +
            $"Hiệu lực: {promo.StartDate:dd/MM/yyyy} - {promo.EndDate:dd/MM/yyyy}. " +
            $"Nhập mã khi đặt sân trên SportBooking để được giảm giá ngay!";

        foreach (var email in emails)
            await _emailService.SendAsync(email, $"[SportBooking] Mã giảm giá {promo.Code} dành cho bạn", body);

        return (true, $"Đã gửi mã {promo.Code} tới {emails.Count} khách hàng qua email.", emails.Count);
    }
}
