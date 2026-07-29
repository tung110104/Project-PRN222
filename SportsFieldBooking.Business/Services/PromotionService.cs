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
    /// Danh sach khach hang co the nhan ma: ma cua chu san -> khach tung dat san cua ho;
    /// ma he thong -> moi khach hang dang hoat dong. Kem so lan da dat de nguoi gui de chon.
    /// </summary>
    Task<List<PromotionRecipient>> GetRecipientCandidatesAsync(int promotionId, int? requestOwnerId);

    /// <summary>
    /// Gui ma khuyen mai qua email (khong phat offline).
    /// userIds = null: gui cho toan bo danh sach de xuat; co gia tri: chi gui cho nhung nguoi duoc chon.
    /// </summary>
    Task<(bool Success, string Message, int SentCount)> SendPromotionEmailAsync(int promotionId, int? requestOwnerId,
        List<int>? userIds = null);
}

/// <summary>Mot khach hang co the nhan ma khuyen mai (dung cho man hinh chon nguoi nhan).</summary>
public class PromotionRecipient
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int BookingCount { get; set; }
    public DateOnly? LastBookingDate { get; set; }
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

    public async Task<List<PromotionRecipient>> GetRecipientCandidatesAsync(int promotionId, int? requestOwnerId)
    {
        var promo = await _uow.Promotions.GetByIdAsync(promotionId);
        if (promo == null) return new List<PromotionRecipient>();
        if (requestOwnerId.HasValue && promo.OwnerId != requestOwnerId.Value) return new List<PromotionRecipient>();

        // Ma cua chu san: khach da tung dat san cua chu do (kem so lan dat de uu tien khach quen)
        if (promo.OwnerId.HasValue)
        {
            return await _uow.Bookings.Query()
                .Where(b => b.Field.OwnerId == promo.OwnerId.Value && b.Status != "Cancelled" && b.User.IsActive)
                .GroupBy(b => new { b.UserId, b.User.FullName, b.User.Email })
                .Select(g => new PromotionRecipient
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName,
                    Email = g.Key.Email,
                    BookingCount = g.Count(),
                    LastBookingDate = g.Max(b => b.BookingDate)
                })
                .OrderByDescending(r => r.BookingCount)
                .ToListAsync();
        }

        // Ma he thong: moi khach hang dang hoat dong
        return await _uow.Users.Query()
            .Where(u => u.IsActive && u.Role.RoleName == "Customer")
            .Select(u => new PromotionRecipient
            {
                UserId = u.UserId,
                FullName = u.FullName,
                Email = u.Email,
                BookingCount = u.Bookings.Count(b => b.Status != "Cancelled"),
                LastBookingDate = u.Bookings.Where(b => b.Status != "Cancelled")
                                            .Select(b => (DateOnly?)b.BookingDate).Max()
            })
            .OrderByDescending(r => r.BookingCount)
            .ToListAsync();
    }

    public async Task<(bool Success, string Message, int SentCount)> SendPromotionEmailAsync(int promotionId,
        int? requestOwnerId, List<int>? userIds = null)
    {
        var promo = await _uow.Promotions.GetByIdAsync(promotionId);
        if (promo == null) return (false, "Không tìm thấy khuyến mãi.", 0);
        // Chu san chi duoc gui ma cua chinh minh (Admin: requestOwnerId = null gui duoc moi ma)
        if (requestOwnerId.HasValue && promo.OwnerId != requestOwnerId.Value)
            return (false, "Bạn chỉ được gửi mã khuyến mãi của mình.", 0);
        if (!promo.IsActive || promo.EndDate < DateOnly.FromDateTime(DateTime.Now))
            return (false, "Khuyến mãi đã hết hạn hoặc đang tạm dừng.", 0);

        var candidates = await GetRecipientCandidatesAsync(promotionId, requestOwnerId);
        if (candidates.Count == 0)
            return (false, promo.OwnerId.HasValue
                ? "Chưa có khách hàng nào từng đặt sân của bạn để gửi mã."
                : "Chưa có khách hàng nào trong hệ thống để gửi mã.", 0);

        // Chi gui cho nhung nguoi duoc chon (neu co) - van loc theo danh sach hop le de tranh gui bay ba
        var recipients = userIds is { Count: > 0 }
            ? candidates.Where(c => userIds.Contains(c.UserId)).ToList()
            : candidates;
        if (recipients.Count == 0)
            return (false, "Bạn chưa chọn khách hàng nào để gửi mã.", 0);

        foreach (var r in recipients)
        {
            var body =
                $"<p>Xin chào <b>{r.FullName}</b>,</p>" +
                $"<p>SportBooking tặng bạn mã khuyến mãi:</p>" +
                $"<div style=\"border:2px dashed #198754;border-radius:8px;padding:16px;text-align:center;margin:16px 0;\">" +
                $"<h2 style=\"letter-spacing:3px;color:#198754;margin:0;\">{promo.Code}</h2>" +
                $"<p style=\"margin:8px 0 0;\">Giảm <b>{promo.DiscountPercent}%</b>" +
                (promo.MaxDiscount > 0 ? $" (tối đa {promo.MaxDiscount:N0}đ)" : "") + "</p></div>" +
                (string.IsNullOrWhiteSpace(promo.Description) ? "" : $"<p>{promo.Description}</p>") +
                $"<p>Hiệu lực: <b>{promo.StartDate:dd/MM/yyyy} - {promo.EndDate:dd/MM/yyyy}</b>. " +
                $"Nhập mã khi đặt sân trên SportBooking để được giảm giá ngay!</p>";

            await _emailService.SendAsync(r.Email, $"[SportBooking] Mã giảm giá {promo.Code} dành cho bạn", body);
        }

        return (true, $"Đã gửi mã {promo.Code} tới {recipients.Count} khách hàng qua email.", recipients.Count);
    }
}
