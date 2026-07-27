using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPaymentService
{
    /// <summary>
    /// Momo/Cash: tạo Payment Pending chờ chủ sân xác nhận.
    /// Wallet (mục 4.2): kiểm tra sân có nhận ví + số dư → trừ NGAY → Paid + Confirmed + cashback.
    /// </summary>
    Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IWalletService _wallet;

    public PaymentService(IUnitOfWork uow, IWalletService wallet)
    {
        _uow = uow;
        _wallet = wallet;
    }

    public async Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method)
    {
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .Include(b => b.Field)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status == "Cancelled") return (false, "Booking đã bị hủy.");
        if (booking.Status == "Completed") return (false, "Booking đã hoàn thành.");
        if (booking.Payments.Any(p => p.Status == "Paid")) return (false, "Booking đã được thanh toán.");
        if (booking.Payments.Any(p => p.Status == "Pending"))
            return (false, "Bạn đã gửi yêu cầu thanh toán, vui lòng chờ chủ sân xác nhận.");
        if (method is not ("Momo" or "Cash" or "Wallet"))
            return (false, "Phương thức thanh toán không hợp lệ.");

        var code = $"{method.ToUpper()}-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}";

        // ---------- Thanh toán bằng VÍ (mục 4.2): trừ ngay, không cần duyệt ----------
        if (method == "Wallet")
        {
            if (!booking.Field.AcceptWalletPayment)
                return (false, "Sân này không chấp nhận thanh toán bằng ví.");

            if (booking.TotalAmount > 0)
            {
                var (ok, msg) = await _wallet.PayFromWalletAsync(userId, booking);
                if (!ok) return (false, msg);
            }

            await _uow.Payments.AddAsync(new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = "Wallet",
                Status = "Paid",              // ví trừ ngay = tiền đã nhận
                TransactionCode = code,
                PaidAt = DateTime.Now
            });

            booking.Status = "Confirmed";     // tiền đã về → xác nhận luôn
            _uow.Bookings.Update(booking);
            await _uow.SaveChangesAsync();

            // Cashback theo cấu hình của sân (mục 4.4)
            if (booking.Field.CashbackPercent > 0 && booking.TotalAmount > 0)
                await _wallet.CashbackAsync(userId, booking, booking.Field.CashbackPercent);

            return (true, booking.Field.CashbackPercent > 0
                ? $"Đã trừ ví {booking.TotalAmount:N0}đ, booking được xác nhận. Bạn nhận cashback {booking.Field.CashbackPercent}%."
                : $"Đã trừ ví {booking.TotalAmount:N0}đ, booking được xác nhận.");
        }

        // ---------- Momo/Cash: khách khai đã trả → tạo Pending, chủ sân kiểm tra rồi mới Paid ----------
        await _uow.Payments.AddAsync(new Payment
        {
            BookingId = bookingId,
            Amount = booking.TotalAmount,
            Method = method,
            Status = "Pending",
            TransactionCode = code,
            PaidAt = null
        });
        await _uow.SaveChangesAsync();

        var pendingMsg = method == "Momo"
            ? "Đã ghi nhận yêu cầu thanh toán. Vui lòng chờ chủ sân kiểm tra chuyển khoản và xác nhận."
            : "Đã ghi nhận. Vui lòng thanh toán tiền mặt tại sân, chủ sân sẽ xác nhận.";
        return (true, pendingMsg);
    }
}
