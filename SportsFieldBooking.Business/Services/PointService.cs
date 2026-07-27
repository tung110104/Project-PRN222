using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPointService
{
    Task<List<PointTransaction>> GetHistoryAsync(int userId);
    /// <summary>Cong diem khi booking hoan thanh: 10.000d = 1 diem, nhan PointsMultiplier neu la ngay vang.</summary>
    Task EarnForBookingAsync(Booking booking);
    /// <summary>Diem thuong khi viet danh gia san.</summary>
    Task EarnForReviewAsync(int userId, int bookingId);
    /// <summary>So diem toi da dung duoc cho 1 booking (tran % gia tri + so du diem hien co).</summary>
    Task<int> GetMaxRedeemableAsync(int userId, decimal totalAmount);
    /// <summary>Tru diem doi lay giam gia. Tra ve so tien duoc giam.</summary>
    Task<(bool Success, string Message, decimal DiscountValue)> RedeemAsync(int userId, int points, int bookingId);
    /// <summary>Hoan lai diem da dung khi booking bi huy.</summary>
    Task ReturnUsedPointsAsync(Booking booking, string reason);
    /// <summary>Doi diem lay voucher giam gia: tru diem, tao ma Promotion dung 1 lan va gui ma qua email.</summary>
    Task<(bool Success, string Message)> RedeemVoucherAsync(int userId, int optionIndex);
    // ----- Admin -----
    Task<(bool Success, string Message)> AdjustAsync(int userId, int points, string reason);
}

public class PointService : IPointService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;

    public PointService(IUnitOfWork uow, IEmailService emailService)
    {
        _uow = uow;
        _emailService = emailService;
    }

    public Task<List<PointTransaction>> GetHistoryAsync(int userId) =>
        _uow.PointTransactions.Query()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.PointTransactionId)
            .ToListAsync();

    // Moi bien dong diem deu qua PointTransactions; LifetimePoints chi tang khi cong diem
    // (tieu diem khong tut hang thanh vien)
    private async Task AddTransactionAsync(User user, string type, int points, string? description, int? bookingId)
    {
        user.Points += points;
        if (points > 0) user.LifetimePoints += points;
        _uow.Users.Update(user);
        await _uow.PointTransactions.AddAsync(new PointTransaction
        {
            UserId = user.UserId,
            Type = type,
            Points = points,
            Description = description,
            BookingId = bookingId,
            CreatedAt = DateTime.Now
        });
    }

    public async Task EarnForBookingAsync(Booking booking)
    {
        var config = AppConfigSingleton.Instance;
        var basePoints = (int)(booking.TotalAmount / config.VndPerPoint);
        if (basePoints <= 0) return;

        // Ngay vang -> nhan he so diem (uu tien ngay vang rieng cua san, sau do ngay vang toan he thong)
        var golden = await _uow.GoldenDays.Query()
            .Where(g => g.Date == booking.BookingDate && g.IsActive &&
                        (g.FieldId == booking.FieldId || g.FieldId == null))
            .OrderByDescending(g => g.FieldId)
            .FirstOrDefaultAsync();
        var points = golden != null ? basePoints * golden.PointsMultiplier : basePoints;

        var user = await _uow.Users.GetByIdAsync(booking.UserId);
        if (user == null) return;

        var note = $"Tích điểm booking #{booking.BookingId}" + (golden != null ? $" (x{golden.PointsMultiplier} {golden.Name})" : "");
        await AddTransactionAsync(user, "Earn", points, note, booking.BookingId);
        await _uow.SaveChangesAsync();
    }

    public async Task EarnForReviewAsync(int userId, int bookingId)
    {
        var bonus = AppConfigSingleton.Instance.ReviewBonusPoints;
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return;
        await AddTransactionAsync(user, "ReviewBonus", bonus, $"Thưởng đánh giá booking #{bookingId}", bookingId);
        await _uow.SaveChangesAsync();
    }

    public async Task<int> GetMaxRedeemableAsync(int userId, decimal totalAmount)
    {
        var config = AppConfigSingleton.Instance;
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null || user.Points <= 0) return 0;

        var capValue = totalAmount * config.MaxRedeemPercent / 100m;
        var capPoints = (int)(capValue / config.PointValueVnd);
        return Math.Min(user.Points, capPoints);
    }

    public async Task<(bool Success, string Message, decimal DiscountValue)> RedeemAsync(int userId, int points, int bookingId)
    {
        if (points <= 0) return (true, "", 0);
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy người dùng.", 0);
        if (user.Points < points)
            return (false, $"Không đủ điểm (hiện có {user.Points} điểm).", 0);

        var value = points * AppConfigSingleton.Instance.PointValueVnd;
        await AddTransactionAsync(user, "Redeem", -points, $"Dùng {points} điểm trừ {value:N0}đ cho booking #{bookingId}", bookingId);
        await _uow.SaveChangesAsync();
        return (true, $"Đã dùng {points} điểm (giảm {value:N0}đ).", value);
    }

    public async Task ReturnUsedPointsAsync(Booking booking, string reason)
    {
        if (booking.PointsUsed <= 0) return;
        var user = await _uow.Users.GetByIdAsync(booking.UserId);
        if (user == null) return;
        // Hoan diem: cong lai so du nhung khong tinh vao LifetimePoints (khong phai diem moi)
        user.Points += booking.PointsUsed;
        _uow.Users.Update(user);
        await _uow.PointTransactions.AddAsync(new PointTransaction
        {
            UserId = user.UserId,
            Type = "Revoke",
            Points = booking.PointsUsed,
            Description = $"Hoàn {booking.PointsUsed} điểm ({reason}) - booking #{booking.BookingId}",
            BookingId = booking.BookingId,
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();
    }

    public async Task<(bool Success, string Message)> RedeemVoucherAsync(int userId, int optionIndex)
    {
        var options = AppConfigSingleton.Instance.VoucherOptions;
        if (optionIndex < 0 || optionIndex >= options.Count)
            return (false, "Gói voucher không hợp lệ.");
        var opt = options[optionIndex];

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy người dùng.");
        if (user.Points < opt.PointCost)
            return (false, $"Không đủ điểm để đổi voucher này (cần {opt.PointCost}, hiện có {user.Points}).");

        // Sinh ma voucher duy nhat (Promotion.Code co unique index)
        string code;
        do
        {
            code = $"VC{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        } while (await _uow.Promotions.Query().AnyAsync(p => p.Code == code));

        var today = DateOnly.FromDateTime(DateTime.Now);
        var endDate = today.AddDays(opt.ValidityDays);
        await _uow.Promotions.AddAsync(new Promotion
        {
            Code = code,
            Description = $"Voucher đổi {opt.PointCost} điểm - {user.FullName}",
            DiscountPercent = opt.DiscountPercent,
            MaxDiscount = opt.MaxDiscount,
            StartDate = today,
            EndDate = endDate,
            Quantity = 1,          // voucher dung 1 lan
            IsActive = true,
            OwnerId = null         // ap dung moi san
        });

        await AddTransactionAsync(user, "Redeem", -opt.PointCost,
            $"Đổi {opt.PointCost} điểm lấy voucher {code} (giảm {opt.DiscountPercent}%, tối đa {opt.MaxDiscount:N0}đ)", null);
        await _uow.SaveChangesAsync();

        // Voucher phat hanh qua email (muc 11): khong hien thi offline
        await _emailService.SendAsync(user.Email, $"[SportBooking] Voucher {code} của bạn",
            $"""
            <h3>Xin chào {user.FullName},</h3>
            <p>Bạn vừa đổi <strong>{opt.PointCost} điểm</strong> lấy voucher giảm giá đặt sân:</p>
            <div style="border:2px dashed #198754;border-radius:8px;padding:16px;text-align:center;margin:16px 0;">
                <h2 style="letter-spacing:3px;color:#198754;margin:0;">{code}</h2>
                <p style="margin:8px 0 0;">Giảm <strong>{opt.DiscountPercent}%</strong> (tối đa {opt.MaxDiscount:N0}đ) cho 1 lần đặt sân</p>
            </div>
            <p>Hạn sử dụng: <strong>{endDate:dd/MM/yyyy}</strong>. Nhập mã này vào ô "Mã khuyến mãi" khi đặt sân.</p>
            <p>SportBooking - Hệ thống đặt sân thể thao</p>
            """);

        return (true, $"Đổi voucher thành công! Mã {code} đã được gửi tới email {user.Email}.");
    }

    public async Task<(bool Success, string Message)> AdjustAsync(int userId, int points, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Cộng/trừ điểm thủ công bắt buộc phải ghi lý do.");
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy người dùng.");
        if (user.Points + points < 0) return (false, "Không thể trừ quá số điểm hiện có.");

        await AddTransactionAsync(user, "Adjust", points, $"Admin điều chỉnh: {reason}", null);
        await _uow.SaveChangesAsync();
        return (true, "Đã điều chỉnh điểm.");
    }
}
