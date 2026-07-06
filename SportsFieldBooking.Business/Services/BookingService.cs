using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IBookingService
{
    Task<List<int>> GetBookedSlotIdsAsync(int fieldId, DateOnly date);
    Task<(bool Success, string Message, int BookingId)> CreateBookingAsync(
        int userId, int fieldId, DateOnly date, List<int> timeSlotIds, string? promoCode, string? note);
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
    public BookingService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<int>> GetBookedSlotIdsAsync(int fieldId, DateOnly date)
        => await _uow.BookingDetails.Query()
            .Where(d => d.FieldId == fieldId && d.BookingDate == date && d.Status == "Active")
            .Select(d => d.TimeSlotId)
            .ToListAsync();

    public async Task<(bool Success, string Message, int BookingId)> CreateBookingAsync(
        int userId, int fieldId, DateOnly date, List<int> timeSlotIds, string? promoCode, string? note)
    {
        if (timeSlotIds.Count == 0)
            return (false, "Vui lòng chọn ít nhất một khung giờ.", 0);

        var today = DateOnly.FromDateTime(DateTime.Now);
        if (date < today)
            return (false, "Không thể đặt sân cho ngày trong quá khứ.", 0);
        if (date > today.AddDays(AppConfigSingleton.Instance.MaxAdvanceBookingDays))
            return (false, $"Chỉ được đặt trước tối đa {AppConfigSingleton.Instance.MaxAdvanceBookingDays} ngày.", 0);

        var field = await _uow.Fields.Query()
            .Include(f => f.TimeSlots)
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.Status == "Active");
        if (field == null)
            return (false, "Sân không tồn tại hoặc đang ngừng hoạt động.", 0);

        var slots = field.TimeSlots.Where(t => timeSlotIds.Contains(t.TimeSlotId) && t.IsActive).ToList();
        if (slots.Count != timeSlotIds.Count)
            return (false, "Khung giờ không hợp lệ.", 0);

        // Neu dat trong ngay hom nay, khong cho dat khung gio da qua
        if (date == today)
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            if (slots.Any(s => s.StartTime <= now))
                return (false, "Khung giờ đã qua, vui lòng chọn khung giờ khác.", 0);
        }

        // Tinh tien theo Strategy Pattern (gio thuong / cao diem)
        decimal total = slots.Sum(s => PricingContext.GetPrice(field, s));

        // Ap dung khuyen mai
        Promotion? promo = null;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            promo = await _uow.Promotions.Query().FirstOrDefaultAsync(p =>
                p.Code == promoCode && p.IsActive && p.Quantity > 0 &&
                p.StartDate <= date && p.EndDate >= date);
            if (promo == null)
                return (false, "Mã giảm giá không hợp lệ hoặc đã hết hạn.", 0);

            var discount = total * promo.DiscountPercent / 100m;
            if (promo.MaxDiscount > 0 && discount > promo.MaxDiscount)
                discount = promo.MaxDiscount;
            total -= discount;
        }

        // Transaction + kiem tra trung lich. Unique filtered index UX_BookingDetails_NoOverlap
        // la lop bao ve cuoi cung neu 2 nguoi dat dong thoi.
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            var conflict = await _uow.BookingDetails.Query()
                .Where(d => d.FieldId == fieldId && d.BookingDate == date &&
                            d.Status == "Active" && timeSlotIds.Contains(d.TimeSlotId))
                .AnyAsync();
            if (conflict)
            {
                await tx.RollbackAsync();
                return (false, "Một hoặc nhiều khung giờ vừa được người khác đặt. Vui lòng chọn lại.", 0);
            }

            var booking = new Booking
            {
                UserId = userId,
                PromotionId = promo?.PromotionId,
                Status = "Pending",
                TotalAmount = total,
                Note = note,
                CreatedAt = DateTime.Now
            };
            await _uow.Bookings.AddAsync(booking);
            await _uow.SaveChangesAsync();

            foreach (var slot in slots)
            {
                await _uow.BookingDetails.AddAsync(new BookingDetail
                {
                    BookingId = booking.BookingId,
                    FieldId = fieldId,
                    TimeSlotId = slot.TimeSlotId,
                    BookingDate = date,
                    Price = PricingContext.GetPrice(field, slot),
                    Status = "Active"
                });
            }

            if (promo != null)
            {
                promo.Quantity -= 1;
                _uow.Promotions.Update(promo);
            }

            await _uow.SaveChangesAsync();
            await tx.CommitAsync();

            // Factory Pattern: gui email xac nhan (demo)
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user != null)
            {
                var sender = NotificationFactory.Create(NotificationType.Email);
                await sender.SendAsync(user.Email, "Xác nhận đặt sân",
                    $"Bạn đã đặt {slots.Count} khung giờ tại {field.FieldName} ngày {date:dd/MM/yyyy}. Tổng tiền: {total:N0}đ.");
            }

            return (true, "Đặt sân thành công! Vui lòng thanh toán để xác nhận.", booking.BookingId);
        }
        catch (DbUpdateException)
        {
            // Vi pham unique index -> nguoi khac vua dat cung slot (race condition)
            await tx.RollbackAsync();
            return (false, "Khung giờ vừa được người khác đặt trước. Vui lòng chọn lại.", 0);
        }
    }

    public Task<List<Booking>> GetByUserAsync(int userId) =>
        _uow.Bookings.Query()
            .Include(b => b.BookingDetails).ThenInclude(d => d.Field)
            .Include(b => b.BookingDetails).ThenInclude(d => d.TimeSlot)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

    public Task<Booking?> GetDetailAsync(int bookingId) =>
        _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.Promotion)
            .Include(b => b.BookingDetails).ThenInclude(d => d.Field)
            .Include(b => b.BookingDetails).ThenInclude(d => d.TimeSlot)
            .Include(b => b.Payments)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

    public async Task<(bool Success, string Message)> CancelAsync(int bookingId, int userId, bool isStaff)
    {
        var booking = await GetDetailAsync(bookingId);
        if (booking == null) return (false, "Không tìm thấy booking.");
        if (!isStaff && booking.UserId != userId) return (false, "Bạn không có quyền hủy booking này.");
        if (booking.Status is "Cancelled" or "Completed") return (false, "Booking không thể hủy.");

        // Khach hang chi duoc huy truoc gio da toi thieu X gio (Singleton config)
        if (!isStaff)
        {
            var limit = AppConfigSingleton.Instance.CancelBeforeHours;
            var earliest = booking.BookingDetails
                .Where(d => d.Status == "Active")
                .Select(d => d.BookingDate.ToDateTime(d.TimeSlot.StartTime))
                .DefaultIfEmpty(DateTime.MaxValue)
                .Min();
            if (earliest < DateTime.Now.AddHours(limit))
                return (false, $"Chỉ được hủy trước giờ đá ít nhất {limit} giờ.");
        }

        booking.Status = "Cancelled";
        foreach (var d in booking.BookingDetails) d.Status = "Cancelled";

        // Hoan lai luot khuyen mai neu co
        if (booking.PromotionId.HasValue)
        {
            var promo = await _uow.Promotions.GetByIdAsync(booking.PromotionId.Value);
            if (promo != null)
            {
                promo.Quantity += 1;
                _uow.Promotions.Update(promo);
            }
        }

        // Hoan tien neu da thanh toan (mo phong)
        var paid = booking.Payments.FirstOrDefault(p => p.Status == "Paid");
        if (paid != null) paid.Status = "Refunded";

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();
        return (true, "Đã hủy booking." + (paid != null ? " Tiền sẽ được hoàn trong 3-5 ngày làm việc." : ""));
    }

    public Task<List<Booking>> GetForStaffAsync(int? ownerId, string? status)
    {
        var query = _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.BookingDetails).ThenInclude(d => d.Field)
            .Include(b => b.BookingDetails).ThenInclude(d => d.TimeSlot)
            .Include(b => b.Payments)
            .AsQueryable();

        // Staff chi thay booking cua san minh so huu; Admin (ownerId = null) thay tat ca
        if (ownerId.HasValue)
            query = query.Where(b => b.BookingDetails.Any(d => d.Field.OwnerId == ownerId.Value));

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

        // Chu san/Admin xac nhan da nhan duoc tien -> danh dau thanh toan Paid.
        var pendingPayment = booking.Payments.FirstOrDefault(p => p.Status == "Pending");
        if (pendingPayment != null)
        {
            pendingPayment.Status = "Paid";
            pendingPayment.PaidAt = DateTime.Now;
        }

        booking.Status = "Confirmed";
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();
        return (true, pendingPayment != null
            ? "Đã xác nhận booking và ghi nhận đã nhận thanh toán."
            : "Đã xác nhận booking.");
    }

    /// <summary>Danh dau Completed cho cac booking da qua ngay da (goi khi xem lich su).</summary>
    public async Task CompletePastBookingsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var toComplete = await _uow.Bookings.Query()
            .Include(b => b.BookingDetails)
            .Where(b => b.Status == "Confirmed" &&
                        b.BookingDetails.All(d => d.BookingDate < today))
            .ToListAsync();
        if (toComplete.Count == 0) return;
        foreach (var b in toComplete) b.Status = "Completed";
        await _uow.SaveChangesAsync();
    }
}
