using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPaymentService
{
    /// <summary>Mo phong thanh toan VNPay/Momo/Cash. Tra ve ket qua ngay (sandbox).</summary>
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
        if (method is not ("VNPay" or "Momo" or "Cash")) return (false, "Phương thức thanh toán không hợp lệ.");

        await _uow.Payments.AddAsync(new Payment
        {
            BookingId = bookingId,
            Amount = booking.TotalAmount,
            Method = method,
            Status = "Paid",
            TransactionCode = $"{method.ToUpper()}-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}",
            PaidAt = DateTime.Now
        });

        // Thanh toan xong -> tu dong xac nhan booking (neu dang cho)
        if (booking.Status == "Pending") booking.Status = "Confirmed";
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        return (true, $"Thanh toán {method} thành công! Booking đã được xác nhận.");
    }
}
