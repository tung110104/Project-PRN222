using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPromotionService
{
    /// <summary>Admin: mọi mã. Owner: chỉ mã của mình.</summary>
    Task<List<Promotion>> GetAllAsync();
    Task<List<Promotion>> GetByOwnerAsync(int ownerId);
    Task<Promotion?> GetByIdAsync(int id);

    /// <summary>isAdmin=false → mã gắn với chủ sân (OwnerId = actor), FieldId phải là sân của actor (mục 11).</summary>
    Task<(bool Success, string Message)> CreateAsync(Promotion promo, int actorUserId, bool isAdmin);
    Task<(bool Success, string Message)> UpdateAsync(Promotion promo, int actorUserId, bool isAdmin);
    Task<(bool Success, string Message)> ToggleActiveAsync(int id, int actorUserId, bool isAdmin);

    /// <summary>Gửi mã qua email cho khách từng đặt sân liên quan (mục 11 — không phát offline).</summary>
    Task<(bool Success, string Message)> SendToCustomersAsync(int promotionId, int actorUserId, bool isAdmin);
}

public class PromotionService : IPromotionService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailSender _emailSender;

    public PromotionService(IUnitOfWork uow, IEmailSender emailSender)
    {
        _uow = uow;
        _emailSender = emailSender;
    }

    public Task<List<Promotion>> GetAllAsync() =>
        _uow.Promotions.Query()
            .Include(p => p.Owner)
            .Include(p => p.Field)
            .OrderByDescending(p => p.PromotionId)
            .ToListAsync();

    public Task<List<Promotion>> GetByOwnerAsync(int ownerId) =>
        _uow.Promotions.Query()
            .Include(p => p.Field)
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.PromotionId)
            .ToListAsync();

    public Task<Promotion?> GetByIdAsync(int id) => _uow.Promotions.GetByIdAsync(id);

    private static (bool Ok, string Message) ValidateDates(Promotion promo)
        => promo.EndDate < promo.StartDate
            ? (false, "Ngày kết thúc phải sau ngày bắt đầu.")
            : (true, "");

    public async Task<(bool Success, string Message)> CreateAsync(Promotion promo, int actorUserId, bool isAdmin)
    {
        if (await _uow.Promotions.Query().AnyAsync(p => p.Code == promo.Code))
            return (false, "Mã khuyến mãi đã tồn tại.");
        var dates = ValidateDates(promo);
        if (!dates.Ok) return (false, dates.Message);

        if (!isAdmin)
        {
            // Chủ sân chỉ tạo được mã cho chính mình và sân của mình
            promo.OwnerId = actorUserId;
            if (promo.FieldId.HasValue)
            {
                var ownsField = await _uow.Fields.Query()
                    .AnyAsync(f => f.FieldId == promo.FieldId.Value && f.OwnerId == actorUserId);
                if (!ownsField) return (false, "Sân được chọn không thuộc quyền của bạn.");
            }
        }

        await _uow.Promotions.AddAsync(promo);
        await _uow.SaveChangesAsync();
        return (true, "Tạo khuyến mãi thành công.");
    }

    public async Task<(bool Success, string Message)> UpdateAsync(Promotion promo, int actorUserId, bool isAdmin)
    {
        var existing = await _uow.Promotions.GetByIdAsync(promo.PromotionId);
        if (existing == null) return (false, "Không tìm thấy khuyến mãi.");
        if (!isAdmin && existing.OwnerId != actorUserId)
            return (false, "Bạn chỉ được sửa mã của chính mình.");

        if (await _uow.Promotions.Query()
                .AnyAsync(p => p.Code == promo.Code && p.PromotionId != promo.PromotionId))
            return (false, "Mã khuyến mãi đã tồn tại.");
        var dates = ValidateDates(promo);
        if (!dates.Ok) return (false, dates.Message);

        existing.Code = promo.Code;
        existing.Description = promo.Description;
        existing.DiscountPercent = promo.DiscountPercent;
        existing.MaxDiscount = promo.MaxDiscount;
        existing.StartDate = promo.StartDate;
        existing.EndDate = promo.EndDate;
        existing.Quantity = promo.Quantity;
        if (isAdmin) existing.FieldId = promo.FieldId;

        _uow.Promotions.Update(existing);
        await _uow.SaveChangesAsync();
        return (true, "Cập nhật khuyến mãi thành công.");
    }

    public async Task<(bool Success, string Message)> ToggleActiveAsync(int id, int actorUserId, bool isAdmin)
    {
        var promo = await _uow.Promotions.GetByIdAsync(id);
        if (promo == null) return (false, "Không tìm thấy khuyến mãi.");
        if (!isAdmin && promo.OwnerId != actorUserId)
            return (false, "Bạn chỉ được thao tác trên mã của chính mình.");

        promo.IsActive = !promo.IsActive;
        _uow.Promotions.Update(promo);
        await _uow.SaveChangesAsync();
        return (true, promo.IsActive ? "Đã kích hoạt mã." : "Đã tạm dừng mã.");
    }

    public async Task<(bool Success, string Message)> SendToCustomersAsync(
        int promotionId, int actorUserId, bool isAdmin)
    {
        var promo = await _uow.Promotions.Query()
            .Include(p => p.Field)
            .FirstOrDefaultAsync(p => p.PromotionId == promotionId);
        if (promo == null) return (false, "Không tìm thấy khuyến mãi.");
        if (!isAdmin && promo.OwnerId != actorUserId)
            return (false, "Bạn chỉ được gửi mã của chính mình.");
        if (!promo.IsActive) return (false, "Mã đang tạm dừng — kích hoạt trước khi gửi.");

        // Chọn người nhận: khách từng đặt sân liên quan (mã theo sân / theo chủ sân / toàn hệ thống)
        var bookingsQuery = _uow.Bookings.Query().AsQueryable();
        if (promo.FieldId.HasValue)
            bookingsQuery = bookingsQuery.Where(b => b.FieldId == promo.FieldId.Value);
        else if (promo.OwnerId.HasValue)
            bookingsQuery = bookingsQuery.Where(b => b.Field.OwnerId == promo.OwnerId.Value);

        var recipients = await bookingsQuery
            .Select(b => new { b.User.Email, b.User.FullName })
            .Distinct()
            .ToListAsync();

        if (recipients.Count == 0)
            return (false, "Chưa có khách nào từng đặt sân trong phạm vi mã này.");

        var scope = promo.Field != null ? $"sân {promo.Field.FieldName}"
                  : promo.OwnerId.HasValue ? "các sân của chủ sân"
                  : "mọi sân trong hệ thống";
        var sent = 0;
        foreach (var r in recipients)
        {
            try
            {
                await _emailSender.SendEmailAsync(r.Email, r.FullName,
                    $"Mã ưu đãi {promo.Code} dành cho bạn",
                    $"""
                    <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto">
                        <h2 style="color:#198754">Sports Field Booking</h2>
                        <p>Xin chào {r.FullName},</p>
                        <p>Cảm ơn bạn đã từng đặt sân. Tặng bạn mã ưu đãi áp dụng cho {scope}:</p>
                        <div style="font-size:28px;font-weight:bold;letter-spacing:4px;
                                    color:#198754;margin:20px 0">{promo.Code}</div>
                        <p>Giảm <strong>{promo.DiscountPercent}%</strong>{(promo.MaxDiscount > 0 ? $" (tối đa {promo.MaxDiscount:N0}đ)" : "")},
                           hiệu lực {promo.StartDate:dd/MM/yyyy} – {promo.EndDate:dd/MM/yyyy}.</p>
                        <p>{promo.Description}</p>
                    </div>
                    """);
                sent++;
            }
            catch { /* bỏ qua địa chỉ lỗi, gửi tiếp người sau */ }
        }

        return (true, $"Đã gửi mã {promo.Code} tới {sent}/{recipients.Count} khách hàng.");
    }
}
