using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPaymentService
{
    /// <summary>Mo phong thanh toan Momo/Cash. Tra ve ket qua ngay (sandbox).</summary>
    Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    public PaymentService(IUnitOfWork uow) => _uow = uow;

    public async Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method)
    {
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status == "Cancelled") return (false, "Booking đã bị hủy.");
        if (booking.Status == "Completed") return (false, "Booking đã hoàn thành.");
        if (booking.Payments.Any(p => p.Status == "Paid")) return (false, "Booking đã được thanh toán.");
        if (booking.Payments.Any(p => p.Status == "Pending"))
            return (false, "Bạn đã gửi yêu cầu thanh toán, vui lòng chờ chủ sân xác nhận.");
        if (method is not ("Momo" or "Cash")) return (false, "Phương thức thanh toán không hợp lệ.");

        // Khach bao da chuyen khoan -> tao ban ghi thanh toan trang thai CHO XAC NHAN (Pending).
        // He thong KHONG tu danh dau da thanh toan. Chu san/Admin kiem tra tien thuc te
        // roi moi xac nhan -> Paid (xem BookingService.ConfirmAsync).
        await _uow.Payments.AddAsync(new Payment
        {
            BookingId = bookingId,
            Amount = booking.TotalAmount,
            Method = method,
            Status = "Pending",
            TransactionCode = $"{method.ToUpper()}-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}",
            PaidAt = null
        });
        await _uow.SaveChangesAsync();

        var msg = method == "Momo"
            ? "Đã ghi nhận yêu cầu thanh toán. Vui lòng chờ chủ sân kiểm tra chuyển khoản và xác nhận."
            : "Đã ghi nhận. Vui lòng thanh toán tiền mặt tại sân, chủ sân sẽ xác nhận.";
        return (true, msg);
    }
}
