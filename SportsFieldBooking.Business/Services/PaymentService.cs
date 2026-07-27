using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPaymentService
{
    /// <summary>
    /// Thanh toan booking: VNPay/Momo/Cash (mo phong) hoac Wallet (vi tien ao, chi khi san bat AcceptWalletPayment).
    /// pointsToUse: so diem khach muon dung de tru tien truoc khi thanh toan (0 = khong dung).
    /// </summary>
    Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method, int pointsToUse = 0);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IPointService _pointService;
    private readonly IWalletService _walletService;

    public PaymentService(IUnitOfWork uow, IPointService pointService, IWalletService walletService)
    {
        _uow = uow;
        _pointService = pointService;
        _walletService = walletService;
    }

    public async Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method, int pointsToUse = 0)
    {
        // Dieu kien so huu (UserId) nam ngay trong query -> khong thanh toan ho booking nguoi khac
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .Include(b => b.Field)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status == "Cancelled") return (false, "Booking đã bị hủy.");
        if (booking.Status == "Completed") return (false, "Booking đã hoàn thành.");
        if (booking.Payments.Any(p => p.Status == "Paid")) return (false, "Booking đã được thanh toán.");
        if (method is not ("VNPay" or "Momo" or "Cash" or "Wallet"))
            return (false, "Phương thức thanh toán không hợp lệ.");
        if (method == "Wallet" && !booking.Field.AcceptWalletPayment)
            return (false, "Sân này không chấp nhận thanh toán bằng ví tiền ảo.");

        // Transaction bao ca: tru diem -> tru vi -> ghi payment (hong buoc nao rollback het)
        await using var tx = await _uow.BeginTransactionAsync();
        try
        {
            // Buoc 1: dung diem tru tien (neu co). Tran: MaxRedeemPercent % gia tri booking
            if (pointsToUse > 0)
            {
                var maxPoints = await _pointService.GetMaxRedeemableAsync(userId, booking.TotalAmount);
                if (pointsToUse > maxPoints)
                {
                    await tx.RollbackAsync();
                    return (false, $"Chỉ được dùng tối đa {maxPoints} điểm cho booking này " +
                                   $"(tối đa {AppConfigSingleton.Instance.MaxRedeemPercent}% giá trị và không quá số điểm đang có).");
                }
                var (ok, msg, discountValue) = await _pointService.RedeemAsync(userId, pointsToUse, bookingId);
                if (!ok)
                {
                    await tx.RollbackAsync();
                    return (false, msg);
                }
                booking.PointsUsed = pointsToUse;
                booking.DiscountAmount += discountValue;
                booking.TotalAmount -= discountValue;
            }

            // Buoc 2: thanh toan phan con lai
            if (method == "Wallet")
            {
                var (ok, msg) = await _walletService.ChargeAsync(userId, booking.TotalAmount, bookingId,
                    $"Thanh toán booking #{bookingId} - {booking.Field.FieldName}");
                if (!ok)
                {
                    await tx.RollbackAsync();
                    return (false, msg);
                }
            }

            // Buoc 3: ghi Payment - Amount lay tu server, khong nhan tu client nen khong the sua gia
            await _uow.Payments.AddAsync(new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = method,
                Status = "Paid",
                TransactionCode = $"{method.ToUpper()}-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}",
                PaidAt = DateTime.Now
            });

            if (booking.Status == "Pending") booking.Status = "Confirmed";
            _uow.Bookings.Update(booking);
            await _uow.SaveChangesAsync();

            // Buoc 4: cashback khi tra bang vi (neu chu san cau hinh)
            var cashbackNote = "";
            if (method == "Wallet" && booking.Field.CashbackPercent > 0 && booking.TotalAmount > 0)
            {
                var cashback = Math.Round(booking.TotalAmount * booking.Field.CashbackPercent / 100m);
                await _walletService.CashbackAsync(userId, cashback, bookingId,
                    $"Cashback {booking.Field.CashbackPercent}% booking #{bookingId}");
                cashbackNote = $" Bạn được hoàn {cashback:N0}đ vào ví (cashback {booking.Field.CashbackPercent}%).";
            }

            await tx.CommitAsync();

            var pointsNote = pointsToUse > 0 ? $" (đã dùng {pointsToUse} điểm)" : "";
            return (true, $"Thanh toán {method} thành công{pointsNote}! Booking đã được xác nhận.{cashbackNote}");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Có lỗi khi thanh toán, vui lòng thử lại.");
        }
    }
}
