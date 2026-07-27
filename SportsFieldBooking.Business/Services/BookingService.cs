using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

/// <summary>Yêu cầu đặt sân (mục 9: 1 booking = 1 sân + 1 khung giờ + 1 ngày).</summary>
public record BookingRequest(
    int CustomerId,          // khách sử dụng sân
    int? CreatedById,        // Staff/Admin đặt hộ (mục 10); null = khách tự đặt
    string? GuestName,       // khách vãng lai (đặt hộ qua điện thoại)
    string? GuestPhone,
    int FieldId,
    int TimeSlotId,
    DateOnly Date,
    string? PromoCode,
    int PointsToUse,         // số điểm khách muốn quy đổi (mục 5.4)
    string? Note);

public interface IBookingService
{
    Task<List<int>> GetBookedSlotIdsAsync(int fieldId, DateOnly date);

    /// <summary>Giá từng slot của sân trong 1 ngày (theo rule + ngày vàng) để hiển thị.</summary>
    Task<Dictionary<int, decimal>> GetSlotPricesAsync(int fieldId, DateOnly date);

    Task<GoldenDay?> GetGoldenDayAsync(DateOnly date, int fieldId);

    Task<(bool Success, string Message, int BookingId)> CreateBookingAsync(BookingRequest request);
    Task<List<Booking>> GetByUserAsync(int userId);
    Task<Booking?> GetDetailAsync(int bookingId);
    Task<(bool Success, string Message)> CancelAsync(int bookingId, int userId, bool isStaff);
    Task<List<Booking>> GetForStaffAsync(int? ownerId, string? status);
    Task<(bool Success, string Message)> ConfirmAsync(int bookingId);
    Task CompletePastBookingsAsync();
}

public class BookingService : IBookingService
{
    private readonly IUnitOfWork _uow;
    private readonly ISettingsService _settings;
    private readonly IPointService _points;
    private readonly IWalletService _wallet;
    private readonly IEmailSender _emailSender;

    public BookingService(
        IUnitOfWork uow,
        ISettingsService settings,
        IPointService points,
        IWalletService wallet,
        IEmailSender emailSender)
    {
        _uow = uow;
        _settings = settings;
        _points = points;
        _wallet = wallet;
        _emailSender = emailSender;
    }

    public async Task<List<int>> GetBookedSlotIdsAsync(int fieldId, DateOnly date)
        => await _uow.Bookings.Query()
            .Where(b => b.FieldId == fieldId && b.BookingDate == date &&
                        b.Status != "Cancelled")
            .Select(b => b.TimeSlotId)
            .ToListAsync();

    public Task<GoldenDay?> GetGoldenDayAsync(DateOnly date, int fieldId) =>
        _uow.GoldenDays.Query().FirstOrDefaultAsync(g =>
            g.Date == date && g.IsActive &&
            (g.FieldId == null || g.FieldId == fieldId));

    public async Task<Dictionary<int, decimal>> GetSlotPricesAsync(int fieldId, DateOnly date)
    {
        var field = await _uow.Fields.Query()
            .Include(f => f.TimeSlots)
            .Include(f => f.PricingRules)
            .FirstOrDefaultAsync(f => f.FieldId == fieldId);
        if (field == null) return new Dictionary<int, decimal>();

        var golden = await GetGoldenDayAsync(date, fieldId);
        return field.TimeSlots
            .Where(t => t.IsActive)
            .ToDictionary(
                t => t.TimeSlotId,
                t => PricingEngine.GetPrice(field, field.PricingRules, t, date, golden));
    }

    public async Task<(bool Success, string Message, int BookingId)> CreateBookingAsync(BookingRequest request)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.Date < today)
            return (false, "Không thể đặt sân cho ngày trong quá khứ.", 0);
        if (request.Date > today.AddDays(AppConfigSingleton.Instance.MaxAdvanceBookingDays))
            return (false, $"Chỉ được đặt trước tối đa {AppConfigSingleton.Instance.MaxAdvanceBookingDays} ngày.", 0);

        var field = await _uow.Fields.Query()
            .Include(f => f.TimeSlots)
            .Include(f => f.PricingRules)
            .FirstOrDefaultAsync(f => f.FieldId == request.FieldId && f.Status == "Active");
        if (field == null)
            return (false, "Sân không tồn tại hoặc đang ngừng hoạt động.", 0);

        var slot = field.TimeSlots.FirstOrDefault(t => t.TimeSlotId == request.TimeSlotId && t.IsActive);
        if (slot == null)
            return (false, "Khung giờ không hợp lệ.", 0);

        if (request.Date == today && slot.StartTime <= TimeOnly.FromDateTime(DateTime.Now))
            return (false, "Khung giờ đã qua, vui lòng chọn khung giờ khác.", 0);

        // ===== PIPELINE GIÁ (thứ tự chốt ở mục 5): rule → mã KM → hạng → điểm =====

        // (1) Giá theo rule (giờ/thứ/mùa) + ưu đãi ngày vàng — Strategy Pattern nâng cấp
        var golden = await GetGoldenDayAsync(request.Date, request.FieldId);
        var unitPrice = PricingEngine.GetPrice(field, field.PricingRules, slot, request.Date, golden);

        // (2) Mã khuyến mãi — kiểm tra hạn/lượt/phạm vi (toàn hệ thống, theo chủ sân, theo sân — mục 11)
        Promotion? promo = null;
        decimal promoDiscount = 0;
        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            promo = await _uow.Promotions.Query().FirstOrDefaultAsync(p =>
                p.Code == request.PromoCode && p.IsActive && p.Quantity > 0 &&
                p.StartDate <= request.Date && p.EndDate >= request.Date &&
                (p.FieldId == null || p.FieldId == request.FieldId) &&
                (p.OwnerId == null || p.OwnerId == field.OwnerId));
            if (promo == null)
                return (false, "Mã giảm giá không hợp lệ, hết hạn hoặc không áp dụng cho sân này.", 0);

            promoDiscount = unitPrice * promo.DiscountPercent / 100m;
            if (promo.MaxDiscount > 0 && promoDiscount > promo.MaxDiscount)
                promoDiscount = promo.MaxDiscount;
        }

        // (3) Giảm giá theo hạng thành viên của KHÁCH (mục 5)
        var customer = await _uow.Users.GetByIdAsync(request.CustomerId);
        if (customer == null)
            return (false, "Không tìm thấy tài khoản khách hàng.", 0);
        var (tierName, tierPercent) = await _settings.GetTierAsync(customer.LifetimePoints);
        var tierDiscount = (unitPrice - promoDiscount) * tierPercent / 100m;

        // (4) Quy đổi điểm (mục 5.4) — có trần % giá trị booking
        int pointsUsed = 0;
        decimal pointsDiscount = 0;
        if (request.PointsToUse > 0)
        {
            var unitPoints = await _settings.GetIntAsync("RedeemUnitPoints", 100);
            var unitValue = await _settings.GetDecimalAsync("RedeemUnitValue", 10000);
            var maxPercent = await _settings.GetIntAsync("RedeemMaxPercent", 50);

            var remaining = unitPrice - promoDiscount - tierDiscount;
            var capByPercent = unitPrice * maxPercent / 100m;
            var maxValue = Math.Min(remaining, capByPercent);

            var requestedUnits = request.PointsToUse / unitPoints;                 // làm tròn xuống bội số
            var affordableUnits = customer.PointBalance / unitPoints;              // theo số điểm đang có
            var capUnits = (int)(maxValue / unitValue);                            // theo trần giá trị
            var units = Math.Min(requestedUnits, Math.Min(affordableUnits, capUnits));

            if (units > 0)
            {
                pointsUsed = units * unitPoints;
                pointsDiscount = units * unitValue;
            }
        }

        var total = unitPrice - promoDiscount - tierDiscount - pointsDiscount;
        if (total < 0) total = 0;

        // ===== TRANSACTION + CHỐNG TRÙNG (unique index UX_Bookings_NoOverlap là lớp cuối) =====
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            var conflict = await _uow.Bookings.Query().AnyAsync(b =>
                b.FieldId == request.FieldId && b.TimeSlotId == request.TimeSlotId &&
                b.BookingDate == request.Date && b.Status != "Cancelled");
            if (conflict)
            {
                await tx.RollbackAsync();
                return (false, "Khung giờ vừa được người khác đặt. Vui lòng chọn khung khác.", 0);
            }

            var booking = new Booking
            {
                UserId = request.CustomerId,
                CreatedById = request.CreatedById,
                GuestName = request.GuestName?.Trim(),
                GuestPhone = request.GuestPhone?.Trim(),
                FieldId = request.FieldId,
                TimeSlotId = request.TimeSlotId,
                BookingDate = request.Date,
                UnitPrice = unitPrice,
                PromotionId = promo?.PromotionId,
                PromoDiscount = promoDiscount,
                TierDiscount = tierDiscount,
                PointsUsed = pointsUsed,
                PointsDiscount = pointsDiscount,
                TotalAmount = total,
                Status = "Pending",
                Note = request.Note,
                CreatedAt = DateTime.Now
            };
            await _uow.Bookings.AddAsync(booking);
            await _uow.SaveChangesAsync();

            if (promo != null)
            {
                promo.Quantity -= 1;
                _uow.Promotions.Update(promo);
                await _uow.SaveChangesAsync();
            }

            // Trừ điểm quy đổi ngay trong transaction (hủy sẽ hoàn lại)
            if (pointsUsed > 0)
            {
                await _points.ApplyChangeAsync(request.CustomerId, "Redeem", -pointsUsed,
                    booking.BookingId, $"Quy đổi {pointsUsed} điểm (−{pointsDiscount:N0}đ) cho booking #{booking.BookingId}");
            }

            await tx.CommitAsync();

            // Email xác nhận (ngoài transaction — lỗi email không phá booking)
            try
            {
                await _emailSender.SendEmailAsync(customer.Email, customer.FullName,
                    "Xác nhận đặt sân",
                    $"""
                    <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto">
                        <h2 style="color:#198754">Sports Field Booking</h2>
                        <p>Bạn đã đặt <strong>{field.FieldName}</strong>,
                           khung {slot.StartTime:HH\:mm}–{slot.EndTime:HH\:mm}
                           ngày <strong>{request.Date:dd/MM/yyyy}</strong>.</p>
                        <p>Tổng tiền: <strong>{total:N0}đ</strong>
                           (giá gốc {unitPrice:N0}đ{(golden != null ? $", ngày vàng {golden.Name}" : "")}).</p>
                        <p>Vui lòng thanh toán để chủ sân xác nhận.</p>
                    </div>
                    """);
            }
            catch { /* demo: bỏ qua lỗi gửi mail */ }

            return (true, "Đặt sân thành công! Vui lòng thanh toán để xác nhận.", booking.BookingId);
        }
        catch (DbUpdateException)
        {
            // Vi phạm unique index — 2 người đặt cùng lúc (race condition)
            await tx.RollbackAsync();
            return (false, "Khung giờ vừa được người khác đặt trước. Vui lòng chọn lại.", 0);
        }
    }

    public Task<List<Booking>> GetByUserAsync(int userId) =>
        _uow.Bookings.Query()
            .Include(b => b.Field)
            .Include(b => b.TimeSlot)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

    public Task<Booking?> GetDetailAsync(int bookingId) =>
        _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.CreatedBy)
            .Include(b => b.Promotion)
            .Include(b => b.Field)
            .Include(b => b.TimeSlot)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

    public async Task<(bool Success, string Message)> CancelAsync(int bookingId, int userId, bool isStaff)
    {
        var booking = await GetDetailAsync(bookingId);
        if (booking == null) return (false, "Không tìm thấy booking.");
        if (!isStaff && booking.UserId != userId) return (false, "Bạn không có quyền hủy booking này.");
        if (booking.Status is "Cancelled" or "Completed") return (false, "Booking không thể hủy.");

        // Khách chỉ được hủy trước giờ đá tối thiểu X giờ (Singleton)
        if (!isStaff)
        {
            var limit = AppConfigSingleton.Instance.CancelBeforeHours;
            var startAt = booking.BookingDate.ToDateTime(booking.TimeSlot.StartTime);
            if (startAt < DateTime.Now.AddHours(limit))
                return (false, $"Chỉ được hủy trước giờ đá ít nhất {limit} giờ.");
        }

        booking.Status = "Cancelled";

        // Hoàn lượt khuyến mãi
        if (booking.PromotionId.HasValue)
        {
            var promo = await _uow.Promotions.GetByIdAsync(booking.PromotionId.Value);
            if (promo != null)
            {
                promo.Quantity += 1;
                _uow.Promotions.Update(promo);
            }
        }

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        // Hoàn điểm đã quy đổi
        if (booking.PointsUsed > 0)
        {
            await _points.ApplyChangeAsync(booking.UserId, "RedeemRefund", booking.PointsUsed,
                booking.BookingId, $"Hoàn {booking.PointsUsed} điểm do hủy booking #{booking.BookingId}");
        }

        // Thu hồi điểm đã tích (nếu booking từng Completed rồi bị staff hủy — hiếm)
        if (booking.PointsEarned > 0)
            await _points.RevokeAsync(booking);

        // Hoàn tiền (mục 4.3 + 8): trả bằng ví → hoàn ví NGAY; Momo/Cash → chờ hoàn thủ công
        var paid = booking.Payments.FirstOrDefault(p => p.Status == "Paid");
        var refundMsg = "";
        if (paid != null)
        {
            if (paid.Method == "Wallet")
            {
                paid.Status = "Refunded";
                _uow.Payments.Update(paid);
                await _uow.SaveChangesAsync();
                await _wallet.RefundToWalletAsync(booking.UserId, paid.Amount,
                    booking.BookingId, $"Hoàn tiền hủy booking #{booking.BookingId}");
                refundMsg = $" {paid.Amount:N0}đ đã được hoàn vào ví.";
            }
            else
            {
                paid.Status = "RefundPending";
                _uow.Payments.Update(paid);
                await _uow.SaveChangesAsync();
                refundMsg = " Tiền sẽ được chủ sân hoàn thủ công trong 3-5 ngày làm việc.";
            }
        }

        // Payment còn Pending (chưa duyệt) → hủy luôn yêu cầu thanh toán
        var pending = booking.Payments.FirstOrDefault(p => p.Status == "Pending");
        if (pending != null)
        {
            pending.Status = "Refunded";
            _uow.Payments.Update(pending);
            await _uow.SaveChangesAsync();
        }

        return (true, "Đã hủy booking." + refundMsg);
    }

    public Task<List<Booking>> GetForStaffAsync(int? ownerId, string? status)
    {
        var query = _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.CreatedBy)
            .Include(b => b.Field)
            .Include(b => b.TimeSlot)
            .Include(b => b.Payments)
            .AsQueryable();

        // Owner chỉ thấy booking của sân mình; Staff/Admin (ownerId = null) thấy tất cả
        if (ownerId.HasValue)
            query = query.Where(b => b.Field.OwnerId == ownerId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);

        return query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task<(bool Success, string Message)> ConfirmAsync(int bookingId)
    {
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);
        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status != "Pending") return (false, "Chỉ xác nhận được booking đang chờ.");

        // Xác nhận đã nhận tiền: payment Pending (Momo/Cash) → Paid
        var pendingPayment = booking.Payments.FirstOrDefault(p => p.Status == "Pending");
        if (pendingPayment != null)
        {
            pendingPayment.Status = "Paid";
            pendingPayment.PaidAt = DateTime.Now;
            _uow.Payments.Update(pendingPayment);
        }

        booking.Status = "Confirmed";
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();
        return (true, pendingPayment != null
            ? "Đã xác nhận booking và ghi nhận đã nhận thanh toán."
            : "Đã xác nhận booking.");
    }

    /// <summary>Đánh dấu Completed cho booking đã qua ngày đá + tích điểm (mục 5.1).</summary>
    public async Task CompletePastBookingsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var toComplete = await _uow.Bookings.Query()
            .Where(b => b.Status == "Confirmed" && b.BookingDate < today)
            .ToListAsync();
        if (toComplete.Count == 0) return;

        foreach (var b in toComplete)
        {
            b.Status = "Completed";
            _uow.Bookings.Update(b);
        }
        await _uow.SaveChangesAsync();

        // Tích điểm sau khi chốt Completed
        foreach (var b in toComplete)
        {
            var earned = await _points.EarnOnCompleteAsync(b);
            if (earned > 0)
            {
                b.PointsEarned = earned;
                _uow.Bookings.Update(b);
            }
        }
        await _uow.SaveChangesAsync();
    }
}
