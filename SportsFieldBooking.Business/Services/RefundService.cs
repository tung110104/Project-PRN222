using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

/// <summary>Noi tien duoc hoan ve.</summary>
public enum RefundDestination
{
    None,           // booking chua thanh toan -> khong co gi de hoan
    Wallet,         // hoan ngay vao vi tien ao
    BankTransfer    // tao yeu cau chuyen khoan ve STK cua khach (xu ly thu cong)
}

public interface IRefundService
{
    /// <summary>
    /// Hoan tien cho mot booking bi huy. Quy tac:
    /// - Da tra bang VI            -> hoan ngay vao vi (du san co con bat nhan vi hay khong).
    /// - Tra qua cong / tien mat:
    ///     + San CO nhan vi        -> hoan ngay vao vi (nhanh, khach dung duoc luon).
    ///     + San KHONG nhan vi     -> tao RefundRequest chuyen khoan ve STK cua khach.
    /// Ham tu gui email thong bao ket qua hoan tien cho khach.
    /// </summary>
    Task<(RefundDestination Destination, decimal Amount, string Note)> RefundBookingAsync(Booking booking, string reason);

    // ----- Quan tri yeu cau hoan tien ve STK -----
    /// <summary>ownerId = null: Admin xem tat ca; co gia tri: chu san chi xem yeu cau cua san minh.</summary>
    Task<List<RefundRequest>> GetRefundRequestsAsync(int? ownerId, string? status);
    Task<(bool Success, string Message)> MarkTransferredAsync(int refundRequestId, int processedById, string? note);
    Task<(bool Success, string Message)> RejectRefundAsync(int refundRequestId, int processedById, string? note);
}

public class RefundService : IRefundService
{
    private readonly IUnitOfWork _uow;
    private readonly IWalletService _walletService;
    private readonly IEmailService _emailService;

    public RefundService(IUnitOfWork uow, IWalletService walletService, IEmailService emailService)
    {
        _uow = uow;
        _walletService = walletService;
        _emailService = emailService;
    }

    public async Task<(RefundDestination Destination, decimal Amount, string Note)> RefundBookingAsync(Booking booking, string reason)
    {
        var paid = booking.Payments.FirstOrDefault(p => p.Status == "Paid");
        if (paid == null) return (RefundDestination.None, 0, "");

        paid.Status = "Refunded";
        _uow.Payments.Update(paid);
        await _uow.SaveChangesAsync();

        var amount = paid.Amount;
        var user = await _uow.Users.GetByIdAsync(booking.UserId);
        var fieldName = booking.Field?.FieldName ?? $"sân #{booking.FieldId}";

        // Tra bang vi -> luon hoan ve vi (tien von tu vi ma ra)
        // Tra cach khac nhung san co nhan vi -> hoan vao vi cho nhanh
        var toWallet = paid.Method == "Wallet" || (booking.Field?.AcceptWalletPayment ?? false);

        if (toWallet)
        {
            await _walletService.RefundAsync(booking.UserId, amount, booking.BookingId,
                $"Hoàn tiền booking #{booking.BookingId} ({reason})");

            if (user != null)
                await _emailService.SendAsync(user.Email,
                    $"[SportBooking] Đã hoàn {amount:N0}đ vào ví - booking #{booking.BookingId}",
                    RefundToWalletEmail(user, booking, fieldName, amount, reason, paid.Method));

            return (RefundDestination.Wallet, amount, $" {amount:N0}đ đã được hoàn vào ví của bạn.");
        }

        // San khong nhan vi -> phai chuyen khoan ve STK cua khach
        var request = new RefundRequest
        {
            BookingId = booking.BookingId,
            UserId = booking.UserId,
            Amount = amount,
            Reason = reason,
            Status = "Pending",
            BankAccountNumber = user?.BankAccountNumber,
            BankName = user?.BankName,
            BankAccountHolder = user?.BankAccountHolder,
            CreatedAt = DateTime.Now
        };
        await _uow.RefundRequests.AddAsync(request);
        await _uow.SaveChangesAsync();

        var hasBank = !string.IsNullOrWhiteSpace(user?.BankAccountNumber);
        if (user != null)
            await _emailService.SendAsync(user.Email,
                $"[SportBooking] Yêu cầu hoàn {amount:N0}đ - booking #{booking.BookingId}",
                RefundToBankEmail(user, booking, fieldName, amount, reason, hasBank));

        return (RefundDestination.BankTransfer, amount, hasBank
            ? $" {amount:N0}đ sẽ được chuyển khoản về tài khoản ngân hàng của bạn trong 1-3 ngày làm việc."
            : $" {amount:N0}đ đang chờ hoàn - vui lòng cập nhật số tài khoản ngân hàng trong trang Hồ sơ cá nhân để nhận tiền.");
    }

    private static string RefundToWalletEmail(User user, Booking booking, string fieldName, decimal amount,
        string reason, string paidMethod)
    {
        var extra = paidMethod == "Wallet"
            ? ""
            : "<p><i>Bạn đã thanh toán qua " + paidMethod + ". Do sân này chấp nhận ví tiền ảo, " +
              "tiền được hoàn ngay vào ví để bạn dùng luôn cho lần đặt sau.</i></p>";
        return $"""
            <h3>Xin chào {user.FullName},</h3>
            <p>Booking <b>#{booking.BookingId}</b> tại <b>{fieldName}</b> ({booking.BookingDate:dd/MM/yyyy}) đã được hủy — lý do: {reason}.</p>
            <div style="border:2px solid #198754;border-radius:8px;padding:16px;margin:16px 0;">
                <p style="margin:0;">Số tiền hoàn</p>
                <h2 style="color:#198754;margin:4px 0;">{amount:N0}đ</h2>
                <p style="margin:0;">đã được cộng vào <b>ví tiền ảo</b> của bạn.</p>
            </div>
            {extra}
            <p>Bạn có thể xem lại tại mục <b>Ví &amp; Điểm</b> trên website. Tiền trong ví dùng để đặt sân, không rút ra được.</p>
            <p>SportBooking - Hệ thống đặt sân thể thao</p>
            """;
    }

    private static string RefundToBankEmail(User user, Booking booking, string fieldName, decimal amount,
        string reason, bool hasBank)
    {
        var bankBlock = hasBank
            ? $"""
               <p>Chúng tôi sẽ chuyển khoản về tài khoản bạn đã đăng ký:</p>
               <ul>
                 <li>Ngân hàng: <b>{user.BankName}</b></li>
                 <li>Số tài khoản: <b>{user.BankAccountNumber}</b></li>
                 <li>Chủ tài khoản: <b>{user.BankAccountHolder}</b></li>
               </ul>
               <p>Thời gian xử lý: <b>1-3 ngày làm việc</b>.</p>
               """
            : """
              <p style="color:#b02a37;"><b>Bạn chưa cập nhật số tài khoản ngân hàng.</b>
              Vui lòng đăng nhập, vào trang <b>Hồ sơ cá nhân</b> và bổ sung thông tin tài khoản
              để chúng tôi chuyển tiền hoàn cho bạn.</p>
              """;
        return $"""
            <h3>Xin chào {user.FullName},</h3>
            <p>Booking <b>#{booking.BookingId}</b> tại <b>{fieldName}</b> ({booking.BookingDate:dd/MM/yyyy}) đã được hủy — lý do: {reason}.</p>
            <div style="border:2px solid #0d6efd;border-radius:8px;padding:16px;margin:16px 0;">
                <p style="margin:0;">Số tiền hoàn</p>
                <h2 style="color:#0d6efd;margin:4px 0;">{amount:N0}đ</h2>
                <p style="margin:0;">hoàn qua <b>chuyển khoản ngân hàng</b> (sân này không nhận thanh toán bằng ví).</p>
            </div>
            {bankBlock}
            <p>SportBooking - Hệ thống đặt sân thể thao</p>
            """;
    }

    // ================= QUAN TRI =================
    public Task<List<RefundRequest>> GetRefundRequestsAsync(int? ownerId, string? status)
    {
        var query = _uow.RefundRequests.Query()
            .Include(r => r.User)
            .Include(r => r.Booking).ThenInclude(b => b.Field)
            .AsQueryable();

        if (ownerId.HasValue)
            query = query.Where(r => r.Booking.Field.OwnerId == ownerId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        return query.OrderBy(r => r.Status == "Pending" ? 0 : 1)
                    .ThenByDescending(r => r.RefundRequestId)
                    .ToListAsync();
    }

    public async Task<(bool Success, string Message)> MarkTransferredAsync(int refundRequestId, int processedById, string? note)
    {
        var request = await _uow.RefundRequests.Query()
            .Include(r => r.User)
            .Include(r => r.Booking).ThenInclude(b => b.Field)
            .FirstOrDefaultAsync(r => r.RefundRequestId == refundRequestId);

        if (request == null) return (false, "Không tìm thấy yêu cầu hoàn tiền.");
        if (request.Status != "Pending") return (false, "Yêu cầu này đã được xử lý.");
        if (string.IsNullOrWhiteSpace(request.BankAccountNumber))
            return (false, "Khách hàng chưa cập nhật số tài khoản ngân hàng - chưa thể chuyển khoản.");

        request.Status = "Completed";
        request.ProcessedById = processedById;
        request.ProcessedAt = DateTime.Now;
        request.ProcessedNote = note;
        _uow.RefundRequests.Update(request);
        await _uow.SaveChangesAsync();

        await _emailService.SendAsync(request.User.Email,
            $"[SportBooking] Đã chuyển khoản hoàn {request.Amount:N0}đ - booking #{request.BookingId}",
            $"""
            <h3>Xin chào {request.User.FullName},</h3>
            <p>Chúng tôi đã <b>chuyển khoản hoàn tiền</b> cho booking #{request.BookingId}
               tại {request.Booking.Field.FieldName}.</p>
            <div style="border:2px solid #198754;border-radius:8px;padding:16px;margin:16px 0;">
                <h2 style="color:#198754;margin:0;">{request.Amount:N0}đ</h2>
                <p style="margin:8px 0 0;">→ {request.BankName} · {request.BankAccountNumber}</p>
            </div>
            {(string.IsNullOrWhiteSpace(note) ? "" : $"<p>Nội dung chuyển khoản: <b>{note}</b></p>")}
            <p>Thời gian: {DateTime.Now:HH:mm dd/MM/yyyy}. Tiền có thể mất vài phút đến vài giờ để về tài khoản
               tùy ngân hàng. Nếu sau 24 giờ chưa nhận được, vui lòng liên hệ với chúng tôi.</p>
            <p>SportBooking - Hệ thống đặt sân thể thao</p>
            """);

        return (true, $"Đã ghi nhận chuyển khoản {request.Amount:N0}đ cho {request.User.FullName} và gửi email xác nhận.");
    }

    public async Task<(bool Success, string Message)> RejectRefundAsync(int refundRequestId, int processedById, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return (false, "Vui lòng ghi lý do từ chối hoàn tiền.");

        var request = await _uow.RefundRequests.Query()
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.RefundRequestId == refundRequestId);

        if (request == null) return (false, "Không tìm thấy yêu cầu hoàn tiền.");
        if (request.Status != "Pending") return (false, "Yêu cầu này đã được xử lý.");

        request.Status = "Rejected";
        request.ProcessedById = processedById;
        request.ProcessedAt = DateTime.Now;
        request.ProcessedNote = note;
        _uow.RefundRequests.Update(request);
        await _uow.SaveChangesAsync();

        await _emailService.SendAsync(request.User.Email,
            $"[SportBooking] Về yêu cầu hoàn tiền booking #{request.BookingId}",
            $"""
            <h3>Xin chào {request.User.FullName},</h3>
            <p>Yêu cầu hoàn {request.Amount:N0}đ cho booking #{request.BookingId} chưa được xử lý chuyển khoản.</p>
            <p>Lý do: <b>{note}</b></p>
            <p>Vui lòng liên hệ với chúng tôi nếu bạn cần hỗ trợ thêm.</p>
            <p>SportBooking - Hệ thống đặt sân thể thao</p>
            """);

        return (true, "Đã từ chối yêu cầu hoàn tiền và gửi email thông báo cho khách.");
    }
}
