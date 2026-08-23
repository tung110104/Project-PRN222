using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IBookingService
{
    Task<List<int>> GetBookedSlotIdsAsync(int fieldId, DateOnly date);
    /// <summary>
    /// Tao booking: moi khung gio duoc chon = 1 booking (da bo BookingDetail).
    /// createdById != null nghia la Owner/Admin dat ho khach (userId la khach duoc dat ho).
    /// </summary>
    Task<(bool Success, string Message, List<int> BookingIds)> CreateBookingAsync(
        int userId, int fieldId, DateOnly date, List<int> timeSlotIds, string? promoCode, string? note,
        int? createdById = null);
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
    private readonly IPricingService _pricingService;
    private readonly IPointService _pointService;
    private readonly IWalletService _walletService;
    private readonly IEmailService _emailService;
    private readonly IRefundService _refundService;

    public BookingService(IUnitOfWork uow, IPricingService pricingService, IPointService pointService,
        IWalletService walletService, IEmailService emailService, IRefundService refundService,
        INotificationService notificationService)
    {
        _uow = uow;
        _pricingService = pricingService;
        _pointService = pointService;
        _walletService = walletService;
        _emailService = emailService;
        _refundService = refundService;
    }

    public async Task<List<int>> GetBookedSlotIdsAsync(int fieldId, DateOnly date)
        => await _uow.Bookings.Query()
            .Where(b => b.FieldId == fieldId && b.BookingDate == date && b.Status != "Cancelled")
            .Select(b => b.TimeSlotId)
            .ToListAsync();

    public async Task<(bool Success, string Message, List<int> BookingIds)> CreateBookingAsync(
        int userId, int fieldId, DateOnly date, List<int> timeSlotIds, string? promoCode, string? note,
        int? createdById = null)
    {
        var none = new List<int>();
        if (timeSlotIds.Count == 0)
            return (false, "Vui lòng chọn ít nhất một khung giờ.", none);

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (date < today)
            return (false, "Không thể đặt sân cho ngày trong quá khứ.", none);
        if (date > today.AddDays(AppConfigSingleton.Instance.MaxAdvanceBookingDays))
            return (false, $"Chỉ được đặt trước tối đa {AppConfigSingleton.Instance.MaxAdvanceBookingDays} ngày.", none);

        var field = await _uow.Fields.Query()
            .Include(f => f.TimeSlots)
            .Include(f => f.PricingRules)
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.Status == "Active");
        if (field == null)
            return (false, "Sân không tồn tại hoặc đang ngừng hoạt động.", none);

        // San dang trong khoang bao tri da duoc duyet -> khong cho dat ngay do
        var inMaintenance = await _uow.MaintenanceRequests.Query()
            .AnyAsync(m => m.FieldId == fieldId && m.Status == "Approved" &&
                           m.StartDate <= date && m.EndDate >= date);
        if (inMaintenance)
            return (false, "Sân bảo trì trong ngày này, vui lòng chọn ngày khác.", none);

        var slots = field.TimeSlots.Where(t => timeSlotIds.Contains(t.TimeSlotId) && t.IsActive).ToList();
        if (slots.Count != timeSlotIds.Count)
            return (false, "Khung giờ không hợp lệ.", none);

        if (date == today)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            if (slots.Any(s => s.StartTime <= now))
                return (false, "Khung giờ đã qua, vui lòng chọn khung giờ khác.", none);
        }

        // Gia tung slot theo bang gia da cap (gio / loai ngay / thang-mua) + he so ngay vang
        var golden = await _pricingService.GetGoldenDayAsync(fieldId, date);

        // Khuyen mai: ma he thong (OwnerId null) dung cho moi san; ma cua chu san chi dung cho san cua ho
        Promotion? promo = null;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            promo = await _uow.Promotions.Query().FirstOrDefaultAsync(p =>
                p.Code == promoCode && p.IsActive && p.Quantity >= timeSlotIds.Count &&
                p.StartDate <= date && p.EndDate >= date);
            if (promo == null)
                return (false, "Mã giảm giá không hợp lệ, hết lượt hoặc đã hết hạn.", none);
            if (promo.OwnerId.HasValue && promo.OwnerId.Value != field.OwnerId)
                return (false, "Mã giảm giá này không áp dụng cho sân bạn chọn.", none);
        }

        // Giam gia theo hang thanh vien cua khach dat
        var customer = await _uow.Users.GetByIdAsync(userId);
        if (customer == null)
            return (false, "Không tìm thấy khách hàng.", none);
        var tier = MembershipTiers.GetTier(customer.LifetimePoints);

        // Transaction + kiem tra trung lich; unique filtered index tren Bookings la lop chan cuoi
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            var conflict = await _uow.Bookings.Query()
                .Where(b => b.FieldId == fieldId && b.BookingDate == date &&
                            b.Status != "Cancelled" && timeSlotIds.Contains(b.TimeSlotId))
                .AnyAsync();
            if (conflict)
            {
                await tx.RollbackAsync();
                return (false, "Một hoặc nhiều khung giờ vừa được người khác đặt. Vui lòng chọn lại.", none);
            }

            var bookingIds = new List<int>();
            decimal grandTotal = 0;
            foreach (var slot in slots)
            {
                var unitPrice = PricingEngine.GetPrice(field, slot, date, golden);
                var discount = 0m;

                if (promo != null)
                {
                    var promoDiscount = unitPrice * promo.DiscountPercent / 100m;
                    if (promo.MaxDiscount > 0 && promoDiscount > promo.MaxDiscount)
                        promoDiscount = promo.MaxDiscount;
                    discount += promoDiscount;
                }
                if (tier.DiscountPercent > 0)
                    discount += (unitPrice - discount) * tier.DiscountPercent / 100m;

                discount = Math.Round(discount);
                var booking = new Booking
                {
                    UserId = userId,
                    FieldId = fieldId,
                    TimeSlotId = slot.TimeSlotId,
                    BookingDate = date,
                    PromotionId = promo?.PromotionId,
                    Status = "Pending",
                    UnitPrice = unitPrice,
                    DiscountAmount = discount,
                    TotalAmount = unitPrice - discount,
                    Note = note,
                    CreatedById = createdById,
                    CreatedAt = DateTime.Now
                };
                await _uow.Bookings.AddAsync(booking);
                await _uow.SaveChangesAsync();
                bookingIds.Add(booking.BookingId);
                grandTotal += booking.TotalAmount;

                if (promo != null)
                {
                    promo.Quantity -= 1;
                    _uow.Promotions.Update(promo);
                }
            }

            await _uow.SaveChangesAsync();
            await tx.CommitAsync();

            var goldenNote = golden != null ? $" (Ngày vàng: {golden.Name})" : "";
            var tierNote = tier.DiscountPercent > 0 ? $" Hạng {tier.Name} được giảm {tier.DiscountPercent}%." : "";
            await _emailService.SendAsync(customer.Email, "Xác nhận đặt sân",
                $"Bạn đã đặt {slots.Count} khung giờ tại {field.FieldName} ngày {date:dd/MM/yyyy}{goldenNote}. " +
                $"Tổng tiền: {grandTotal:N0}đ.{tierNote} Vui lòng thanh toán để xác nhận.");

            return (true, $"Đặt sân thành công ({slots.Count} khung giờ, tổng {grandTotal:N0}đ){goldenNote}.{tierNote}", bookingIds);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            return (false, "Khung giờ vừa được người khác đặt trước. Vui lòng chọn lại.", none);
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
            .Include(b => b.Promotion)
            .Include(b => b.Field)
            .Include(b => b.TimeSlot)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .Include(b => b.CreatedBy)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

    public async Task<(bool Success, string Message)> CancelAsync(int bookingId, int userId, bool isStaff)
    {
        var booking = await GetDetailAsync(bookingId);
        if (booking == null) return (false, "Không tìm thấy booking.");
        if (!isStaff && booking.UserId != userId) return (false, "Bạn không có quyền hủy booking này.");
        if (booking.Status is "Cancelled" or "Completed") return (false, "Booking không thể hủy.");

        if (!isStaff)
        {
            var limit = AppConfigSingleton.Instance.CancelBeforeHours;
            var start = booking.BookingDate.ToDateTime(booking.TimeSlot.StartTime);
            if (start < DateTime.Now.AddHours(limit))
                return (false, $"Chỉ được hủy trước giờ đá ít nhất {limit} giờ.");
        }

        booking.Status = "Cancelled";

        // Hoan lai luot khuyen mai
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

        // Hoan diem da dung (neu co)
        await _pointService.ReturnUsedPointsAsync(booking, "hủy booking");

        // Hoan tien: RefundService tu quyet dinh hoan vao vi hay chuyen khoan ve STK, va gui email hoan tien
        var cancelReason = isStaff ? "chủ sân/quản trị viên hủy" : "khách hàng tự hủy";
        var (destination, refundAmount, refundNote) = await _refundService.RefundBookingAsync(booking, cancelReason);

        // Email thong bao huy booking (rieng email hoan tien do RefundService gui)
        await SendCancelEmailAsync(booking, cancelReason, destination, refundAmount);

        return (true, "Đã hủy booking." + refundNote);
    }

    /// <summary>Email bao huy booking cho khach (moi truong hop huy deu gui, khong chi rieng bao tri).</summary>
    private async Task SendCancelEmailAsync(Booking booking, string reason, RefundDestination destination, decimal amount)
    {
        if (booking.User == null) return;

        var refundBlock = destination switch
        {
            RefundDestination.Wallet =>
                $"<p>Số tiền <b>{amount:N0}đ</b> đã được <b>hoàn vào ví tiền ảo</b> của bạn (xem chi tiết trong email hoàn tiền kèm theo).</p>",
            RefundDestination.BankTransfer =>
                $"<p>Số tiền <b>{amount:N0}đ</b> sẽ được <b>chuyển khoản</b> về tài khoản ngân hàng của bạn (xem chi tiết trong email hoàn tiền kèm theo).</p>",
            _ => "<p>Booking chưa thanh toán nên không phát sinh hoàn tiền.</p>"
        };

        try
        {
            await _emailService.SendAsync(booking.User.Email,
                $"[SportBooking] Đã hủy booking #{booking.BookingId}",
                $"""
                <h3>Xin chào {booking.User.FullName},</h3>
                <p>Booking của bạn đã được hủy — lý do: <b>{reason}</b>.</p>
                <table cellpadding="6" style="border-collapse:collapse;">
                    <tr><td>Mã booking</td><td><b>#{booking.BookingId}</b></td></tr>
                    <tr><td>Sân</td><td><b>{booking.Field?.FieldName}</b></td></tr>
                    <tr><td>Ngày đá</td><td><b>{booking.BookingDate:dd/MM/yyyy}</b></td></tr>
                    <tr><td>Khung giờ</td><td><b>{booking.TimeSlot?.StartTime:HH\:mm} - {booking.TimeSlot?.EndTime:HH\:mm}</b></td></tr>
                    <tr><td>Tổng tiền</td><td><b>{booking.TotalAmount:N0}đ</b></td></tr>
                </table>
                {refundBlock}
                <p>Điểm tích lũy đã dùng (nếu có) và lượt mã khuyến mãi đã được hoàn lại cho bạn.</p>
                <p>Rất mong được phục vụ bạn lần sau!</p>
                <p>SportBooking - Hệ thống đặt sân thể thao</p>
                """);
        }
        catch { /* loi gui mail khong duoc lam hong nghiep vu huy booking */ }
    }

    public Task<List<Booking>> GetForStaffAsync(int? ownerId, string? status)
    {
        var query = _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.Field)
            .Include(b => b.TimeSlot)
            .Include(b => b.Payments)
            .AsQueryable();

        // Owner chi thay booking cua san minh; Admin/Staff (ownerId = null) thay tat ca
        if (ownerId.HasValue)
            query = query.Where(b => b.Field.OwnerId == ownerId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);

        return query.OrderByDescending(b => b.CreatedAt).ToListAsync();
    }

    public async Task<(bool Success, string Message)> ConfirmAsync(int bookingId)
    {
        var booking = await _uow.Bookings.GetByIdAsync(bookingId);
        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status != "Pending") return (false, "Chỉ xác nhận được booking đang chờ.");
        booking.Status = "Confirmed";
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();
        return (true, "Đã xác nhận booking.");
    }

    /// <summary>Danh dau Completed cho booking da qua ngay da + tich diem cho khach (goi khi xem danh sach).</summary>
    public async Task CompletePastBookingsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var toComplete = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .Where(b => b.Status == "Confirmed" && b.BookingDate < today)
            .ToListAsync();
        if (toComplete.Count == 0) return;

        foreach (var b in toComplete) b.Status = "Completed";
        await _uow.SaveChangesAsync();

        // Chi tich diem cho booking da thanh toan that (tranh cong diem cho booking chua thu tien)
        foreach (var b in toComplete.Where(x => x.Payments.Any(p => p.Status == "Paid")))
            await _pointService.EarnForBookingAsync(b);
    }
}
