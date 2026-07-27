using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPointService
{
    Task<List<PointTransaction>> GetHistoryAsync(int userId);
    Task<(string TierName, int DiscountPercent)> GetUserTierAsync(int userId);

    /// <summary>Biến động điểm CỐT LÕI — mọi cộng/trừ đều đi qua đây (ghi log PointTransaction).</summary>
    Task ApplyChangeAsync(int userId, string type, int points, int? bookingId, string note);

    /// <summary>Cộng điểm khi booking hoàn thành (mục 5.1, 5.2): theo tiền × hệ số ngày vàng + thưởng lần đầu.</summary>
    Task<int> EarnOnCompleteAsync(Booking booking);

    /// <summary>Thu hồi điểm đã cộng nếu booking bị hủy sau khi hoàn thành (Revoke).</summary>
    Task RevokeAsync(Booking booking);

    /// <summary>Thưởng điểm khi review sân (mục 5.3).</summary>
    Task AddReviewBonusAsync(int userId, int bookingId);

    /// <summary>Đổi điểm lấy voucher gửi qua email (mục 5.5 + 11).</summary>
    Task<(bool Success, string Message)> RedeemVoucherAsync(int userId);
}

public class PointService : IPointService
{
    private readonly IUnitOfWork _uow;
    private readonly ISettingsService _settings;
    private readonly IEmailSender _emailSender;

    public PointService(IUnitOfWork uow, ISettingsService settings, IEmailSender emailSender)
    {
        _uow = uow;
        _settings = settings;
        _emailSender = emailSender;
    }

    public Task<List<PointTransaction>> GetHistoryAsync(int userId) =>
        _uow.PointTransactions.Query()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

    public async Task<(string TierName, int DiscountPercent)> GetUserTierAsync(int userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        return await _settings.GetTierAsync(user?.LifetimePoints ?? 0);
    }

    public async Task ApplyChangeAsync(int userId, string type, int points, int? bookingId, string note)
    {
        if (points == 0) return;
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new InvalidOperationException($"Không tìm thấy user #{userId}.");

        user.PointBalance += points;
        if (user.PointBalance < 0) user.PointBalance = 0;

        // Chỉ điểm KIẾM ĐƯỢC từ hoạt động mới tăng tích lũy trọn đời (tính hạng);
        // hoàn điểm đã quy đổi (RedeemRefund) không tính; Revoke trừ lại điểm không hợp lệ.
        var earnTypes = new[] { "Earn", "ReviewBonus", "FirstBookingBonus", "Bonus" };
        if (points > 0 && earnTypes.Contains(type))
            user.LifetimePoints += points;
        else if (type == "Revoke")
            user.LifetimePoints = Math.Max(0, user.LifetimePoints + points);

        _uow.Users.Update(user);
        await _uow.PointTransactions.AddAsync(new PointTransaction
        {
            UserId = userId,
            Type = type,
            Points = points,
            BalanceAfter = user.PointBalance,
            BookingId = bookingId,
            Note = note,
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();
    }

    public async Task<int> EarnOnCompleteAsync(Booking booking)
    {
        var rate = await _settings.GetDecimalAsync("PointsEarnRate", 10000);
        if (rate <= 0) return 0;

        var basePoints = (int)(booking.TotalAmount / rate);

        // Nhân điểm ngày vàng (mục 1 + 5.2)
        var golden = await _uow.GoldenDays.Query().FirstOrDefaultAsync(g =>
            g.Date == booking.BookingDate && g.IsActive &&
            (g.FieldId == null || g.FieldId == booking.FieldId));
        var multiplier = golden?.PointMultiplier ?? 1;
        var points = basePoints * Math.Max(1, multiplier);

        if (points > 0)
        {
            var note = multiplier > 1
                ? $"Tích điểm booking #{booking.BookingId} (×{multiplier} ngày vàng {golden!.Name})"
                : $"Tích điểm booking #{booking.BookingId}";
            await ApplyChangeAsync(booking.UserId, "Earn", points, booking.BookingId, note);
        }

        // Thưởng lần đặt hoàn thành đầu tiên (mục 5.2)
        var completedCount = await _uow.Bookings.Query()
            .CountAsync(b => b.UserId == booking.UserId && b.Status == "Completed");
        if (completedCount <= 1)
        {
            var firstBonus = await _settings.GetIntAsync("FirstBookingBonus", 20);
            if (firstBonus > 0)
            {
                await ApplyChangeAsync(booking.UserId, "FirstBookingBonus", firstBonus,
                    booking.BookingId, "Thưởng lần đặt sân hoàn thành đầu tiên");
                points += firstBonus;
            }
        }

        return points;
    }

    public async Task RevokeAsync(Booking booking)
    {
        if (booking.PointsEarned <= 0) return;
        await ApplyChangeAsync(booking.UserId, "Revoke", -booking.PointsEarned,
            booking.BookingId, $"Thu hồi điểm do hủy booking #{booking.BookingId}");
    }

    public async Task AddReviewBonusAsync(int userId, int bookingId)
    {
        var bonus = await _settings.GetIntAsync("ReviewBonus", 5);
        if (bonus <= 0) return;
        await ApplyChangeAsync(userId, "ReviewBonus", bonus, bookingId,
            $"Thưởng đánh giá sân (booking #{bookingId})");
    }

    public async Task<(bool Success, string Message)> RedeemVoucherAsync(int userId)
    {
        var cost = await _settings.GetIntAsync("VoucherCostPoints", 200);
        var percent = await _settings.GetIntAsync("VoucherPercent", 10);
        var maxDiscount = await _settings.GetDecimalAsync("VoucherMaxDiscount", 50000);

        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        if (user.PointBalance < cost)
            return (false, $"Cần {cost} điểm để đổi voucher (bạn đang có {user.PointBalance}).");

        // Tạo mã voucher cá nhân: giảm percent%, dùng 1 lần, hạn 3 tháng
        var code = $"VC{userId}{DateTime.Now:MMddHHmmss}";
        var today = DateOnly.FromDateTime(DateTime.Now);
        await _uow.Promotions.AddAsync(new Promotion
        {
            Code = code,
            Description = $"Voucher đổi {cost} điểm của {user.FullName}",
            DiscountPercent = percent,
            MaxDiscount = maxDiscount,
            StartDate = today,
            EndDate = today.AddMonths(3),
            Quantity = 1,
            IsActive = true
        });
        await _uow.SaveChangesAsync();

        await ApplyChangeAsync(userId, "VoucherRedeem", -cost, null,
            $"Đổi {cost} điểm lấy voucher {code}");

        // Gửi voucher qua email (mục 11) — lỗi gửi mail không làm hỏng giao dịch điểm
        try
        {
            await _emailSender.SendEmailAsync(user.Email, user.FullName,
                "Voucher đổi điểm của bạn",
                $"""
                <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto">
                    <h2 style="color:#198754">Sports Field Booking</h2>
                    <p>Xin chào {user.FullName},</p>
                    <p>Bạn vừa đổi <strong>{cost} điểm</strong> lấy voucher giảm giá:</p>
                    <div style="font-size:28px;font-weight:bold;letter-spacing:4px;
                                color:#198754;margin:20px 0">{code}</div>
                    <p>Giảm <strong>{percent}%</strong> (tối đa {maxDiscount:N0}đ), dùng 1 lần,
                       hạn đến <strong>{today.AddMonths(3):dd/MM/yyyy}</strong>.</p>
                    <p>Nhập mã này khi đặt sân để được giảm giá.</p>
                </div>
                """);
        }
        catch
        {
            return (true, $"Đã đổi voucher {code} (gửi email thất bại — hãy lưu lại mã này).");
        }

        return (true, $"Đã đổi voucher {code} — mã đã được gửi tới email của bạn.");
    }
}
