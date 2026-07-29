using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Patterns;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IPaymentService
{
    /// <summary>
    /// Thanh toan booking: VNPay/Momo (qua cong), Wallet (vi tien ao, chi khi san bat AcceptWalletPayment)
    /// hoac Cash (tien mat - CHI ghi nhan yeu cau, cho chu san xac nhan da thu tien tai quay).
    /// pointsToUse: so diem khach muon dung de tru tien truoc khi thanh toan (0 = khong dung).
    /// </summary>
    Task<(bool Success, string Message)> PayAsync(int bookingId, int userId, string method, int pointsToUse = 0);

    /// <summary>
    /// [CHU SAN] Xac nhan da thu tien mat tai quay -> chuyen Payment sang Paid va booking sang Confirmed.
    /// confirmedById: nguoi thao tac (chu san/admin) de truy vet.
    /// </summary>
    Task<(bool Success, string Message)> ConfirmCashPaymentAsync(int bookingId, int confirmedById);

    /// <summary>[CHU SAN] Tu choi yeu cau tra tien mat (khach khong den tra) -> huy ban ghi cho thanh toan.</summary>
    Task<(bool Success, string Message)> RejectCashPaymentAsync(int bookingId, int rejectedById);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IPointService _pointService;
    private readonly IWalletService _walletService;
    private readonly IEmailService _emailService;

    public PaymentService(IUnitOfWork uow, IPointService pointService, IWalletService walletService,
        IEmailService emailService)
    {
        _uow = uow;
        _pointService = pointService;
        _walletService = walletService;
        _emailService = emailService;
    }

    /// <summary>Email xac nhan thanh toan - goi sau khi da ghi nhan Paid thanh cong.</summary>
    private async Task SendPaymentEmailAsync(Booking booking, string method, decimal amount,
        int pointsUsed, decimal cashback)
    {
        var user = booking.User ?? await _uow.Users.GetByIdAsync(booking.UserId);
        if (user == null) return;

        var methodText = method switch
        {
            "Wallet" => "Ví tiền ảo",
            "Cash" => "Tiền mặt tại sân",
            _ => method
        };
        var extras = "";
        if (pointsUsed > 0) extras += $"<tr><td>Điểm đã dùng</td><td><b>{pointsUsed} điểm</b></td></tr>";
        if (cashback > 0) extras += $"<tr><td>Hoàn tiền cashback</td><td><b style=\"color:#198754\">+{cashback:N0}đ vào ví</b></td></tr>";

        try
        {
            await _emailService.SendAsync(user.Email,
                $"[SportBooking] Thanh toán thành công {amount:N0}đ - booking #{booking.BookingId}",
                $"""
                <h3>Xin chào {user.FullName},</h3>
                <p>Cảm ơn bạn! Booking <b>#{booking.BookingId}</b> đã được thanh toán thành công và xác nhận.</p>
                <div style="border:2px solid #198754;border-radius:8px;padding:16px;margin:16px 0;text-align:center;">
                    <p style="margin:0;">Số tiền đã thanh toán</p>
                    <h2 style="color:#198754;margin:4px 0;">{amount:N0}đ</h2>
                    <p style="margin:0;">qua <b>{methodText}</b></p>
                </div>
                <table cellpadding="6" style="border-collapse:collapse;">
                    <tr><td>Sân</td><td><b>{booking.Field?.FieldName}</b></td></tr>
                    <tr><td>Ngày đá</td><td><b>{booking.BookingDate:dd/MM/yyyy}</b></td></tr>
                    <tr><td>Khung giờ</td><td><b>{booking.TimeSlot?.StartTime:HH\:mm} - {booking.TimeSlot?.EndTime:HH\:mm}</b></td></tr>
                    {extras}
                </table>
                <p>Vui lòng có mặt trước giờ đá 10 phút. Nếu cần hủy, hãy hủy trước giờ đá ít nhất 2 tiếng để được hoàn tiền.</p>
                <p>SportBooking - Hệ thống đặt sân thể thao</p>
                """);
        }
        catch { /* loi gui mail khong duoc lam hong giao dich da chot */ }
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

        // TIEN MAT: khong tu dong chuyen Paid - chi ghi nhan yeu cau (Pending),
        // chu san phai bam "Xac nhan da thu tien" tai trang Quan ly dat lich.
        if (method == "Cash")
        {
            if (booking.Payments.Any(p => p.Status == "Pending" && p.Method == "Cash"))
                return (false, "Bạn đã đăng ký trả tiền mặt cho booking này, đang chờ chủ sân xác nhận.");
            if (pointsToUse > 0)
                return (false, "Thanh toán tiền mặt không dùng được điểm. Vui lòng chọn phương thức khác nếu muốn dùng điểm.");

            await _uow.Payments.AddAsync(new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = "Cash",
                Status = "Pending",
                TransactionCode = $"CASH-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}",
                PaidAt = null
            });
            await _uow.SaveChangesAsync();
            return (true, $"Đã ghi nhận yêu cầu thanh toán tiền mặt {booking.TotalAmount:N0}đ. " +
                          "Vui lòng đến sân thanh toán - booking được xác nhận sau khi chủ sân xác nhận đã thu tiền.");
        }

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

            // Email xac nhan thanh toan (gui sau khi commit de khong giu transaction cho SMTP)
            var cashbackAmount = method == "Wallet" && booking.Field.CashbackPercent > 0
                ? Math.Round(booking.TotalAmount * booking.Field.CashbackPercent / 100m) : 0m;
            await SendPaymentEmailAsync(booking, method, booking.TotalAmount, pointsToUse, cashbackAmount);

            var pointsNote = pointsToUse > 0 ? $" (đã dùng {pointsToUse} điểm)" : "";
            return (true, $"Thanh toán {method} thành công{pointsNote}! Booking đã được xác nhận.{cashbackNote}");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Có lỗi khi thanh toán, vui lòng thử lại.");
        }
    }

    public async Task<(bool Success, string Message)> ConfirmCashPaymentAsync(int bookingId, int confirmedById)
    {
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .Include(b => b.Field)
            .Include(b => b.User)
            .Include(b => b.TimeSlot)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status == "Cancelled") return (false, "Booking đã bị hủy, không thể thu tiền.");
        if (booking.Payments.Any(p => p.Status == "Paid")) return (false, "Booking này đã được thanh toán.");

        var cash = booking.Payments.FirstOrDefault(p => p.Status == "Pending" && p.Method == "Cash");
        if (cash == null)
        {
            // Khach tra thang tai quay ma chua dang ky truoc -> tao ban ghi moi da thu tien
            cash = new Payment
            {
                BookingId = bookingId,
                Amount = booking.TotalAmount,
                Method = "Cash",
                TransactionCode = $"CASH-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}"
            };
            await _uow.Payments.AddAsync(cash);
        }

        cash.Amount = booking.TotalAmount;   // chot lai theo so tien hien tai cua booking
        cash.Status = "Paid";
        cash.PaidAt = DateTime.Now;
        cash.TransactionCode = $"CASH-{DateTime.Now:yyyyMMddHHmmss}-{bookingId}-BY{confirmedById}";

        if (booking.Status == "Pending") booking.Status = "Confirmed";
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        // Email bao khach da thu tien mat thanh cong
        await SendPaymentEmailAsync(booking, "Cash", booking.TotalAmount, booking.PointsUsed, 0m);

        return (true, $"Đã xác nhận thu {booking.TotalAmount:N0}đ tiền mặt. Booking #{bookingId} được xác nhận.");
    }

    public async Task<(bool Success, string Message)> RejectCashPaymentAsync(int bookingId, int rejectedById)
    {
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        var cash = booking.Payments.FirstOrDefault(p => p.Status == "Pending" && p.Method == "Cash");
        if (cash == null) return (false, "Booking này không có yêu cầu thanh toán tiền mặt đang chờ.");

        cash.Status = "Failed";
        cash.TransactionCode = $"CASH-REJECTED-{DateTime.Now:yyyyMMddHHmmss}-BY{rejectedById}";
        _uow.Payments.Update(cash);
        await _uow.SaveChangesAsync();

        return (true, $"Đã từ chối yêu cầu trả tiền mặt của booking #{bookingId}. Khách có thể chọn phương thức khác.");
    }
}
