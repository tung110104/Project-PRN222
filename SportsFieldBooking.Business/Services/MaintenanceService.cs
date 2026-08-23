using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IMaintenanceService
{
    Task<List<MaintenanceRequest>> GetByOwnerAsync(int ownerId);
    Task<List<MaintenanceRequest>> GetAllForAdminAsync(string? status);
    Task<(bool Success, string Message)> CreateAsync(int ownerId, int fieldId, string reason, DateOnly start, DateOnly end);
    /// <summary>
    /// Admin duyet: san chuyen Maintenance (neu dang trong khoang bao tri), booking trung lich bi huy tu dong,
    /// hoan tien (ve vi neu tra bang vi), hoan diem va gui email thong bao cho khach - KHONG hoi y kien cho dong y.
    /// </summary>
    Task<(bool Success, string Message)> ApproveAsync(int requestId, string? adminNote);
    Task<(bool Success, string Message)> RejectAsync(int requestId, string? adminNote);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IUnitOfWork _uow;
    private readonly IWalletService _walletService;
    private readonly IPointService _pointService;
    private readonly IEmailService _emailService;
    private readonly IRefundService _refundService;
    private readonly INotificationService _notificationService;

    public MaintenanceService(IUnitOfWork uow, IWalletService walletService, IPointService pointService,
        IEmailService emailService, IRefundService refundService, INotificationService notificationService)
    {
        _uow = uow;
        _walletService = walletService;
        _pointService = pointService;
        _emailService = emailService;
        _refundService = refundService;
        _notificationService = notificationService;
    }

    /// <summary>Goi NotifyAsync nhung nuot loi - thong bao hong khong duoc lam hong nghiep vu chinh.</summary>
    private async Task TryNotifyAsync(int userId, string title, string message, string? url = null)
    {
        try { await _notificationService.NotifyAsync(userId, title, message, url); }
        catch { /* bo qua */ }
    }

    public Task<List<MaintenanceRequest>> GetByOwnerAsync(int ownerId) =>
        _uow.MaintenanceRequests.Query()
            .Include(m => m.Field)
            .Where(m => m.OwnerId == ownerId)
            .OrderByDescending(m => m.MaintenanceRequestId)
            .ToListAsync();

    public Task<List<MaintenanceRequest>> GetAllForAdminAsync(string? status)
    {
        var query = _uow.MaintenanceRequests.Query()
            .Include(m => m.Field)
            .Include(m => m.Owner)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);
        return query.OrderByDescending(m => m.MaintenanceRequestId).ToListAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(int ownerId, int fieldId, string reason, DateOnly start, DateOnly end)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Vui lòng nhập lý do bảo trì.");
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (start < today) return (false, "Ngày bắt đầu không được ở quá khứ.");
        if (end < start) return (false, "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");

        var field = await _uow.Fields.Query()
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.OwnerId == ownerId);
        if (field == null) return (false, "Sân không tồn tại hoặc không thuộc quyền quản lý của bạn.");

        var pendingDup = await _uow.MaintenanceRequests.Query()
            .AnyAsync(m => m.FieldId == fieldId && m.Status == "Pending");
        if (pendingDup) return (false, "Sân này đang có yêu cầu bảo trì chờ duyệt.");

        await _uow.MaintenanceRequests.AddAsync(new MaintenanceRequest
        {
            FieldId = fieldId,
            OwnerId = ownerId,
            Reason = reason,
            StartDate = start,
            EndDate = end,
            Status = "Pending",
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();

        // Bao real-time cho MOI Admin: co yeu cau bao tri moi cho duyet
        var owner = await _uow.Users.GetByIdAsync(ownerId);
        var adminIds = await _uow.Users.Query()
            .Where(u => u.IsActive && u.Role.RoleName == "Admin")
            .Select(u => u.UserId)
            .ToListAsync();
        foreach (var adminId in adminIds)
        {
            await TryNotifyAsync(adminId,
                "Yêu cầu bảo trì mới",
                $"{owner?.FullName ?? "Chủ sân"} gửi yêu cầu bảo trì sân {field.FieldName} " +
                $"từ {start:dd/MM/yyyy} đến {end:dd/MM/yyyy} — lý do: {reason}.",
                "/Maintenance/Index?status=Pending");
        }

        return (true, "Đã gửi yêu cầu bảo trì, chờ Admin duyệt.");
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int requestId, string? adminNote)
    {
        var request = await _uow.MaintenanceRequests.Query()
            .Include(m => m.Field)
            .Include(m => m.Owner)
            .FirstOrDefaultAsync(m => m.MaintenanceRequestId == requestId);
        if (request == null) return (false, "Không tìm thấy yêu cầu.");
        if (request.Status != "Pending") return (false, "Yêu cầu đã được xử lý trước đó.");

        request.Status = "Approved";
        request.AdminNote = adminNote;
        request.ProcessedAt = DateTime.Now;
        _uow.MaintenanceRequests.Update(request);

        // Dang trong khoang bao tri -> chuyen trang thai san ngay
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (request.StartDate <= today && request.EndDate >= today)
        {
            request.Field.Status = "Maintenance";
            _uow.Fields.Update(request.Field);
        }

        // Tim booking trung khoang bao tri de huy + hoan tien
        var affected = await _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.TimeSlot)
            .Include(b => b.Payments)
            .Include(b => b.Field)
            .Where(b => b.FieldId == request.FieldId &&
                        b.BookingDate >= request.StartDate && b.BookingDate <= request.EndDate &&
                        (b.Status == "Pending" || b.Status == "Confirmed"))
            .ToListAsync();

        foreach (var booking in affected)
        {
            booking.Status = "Cancelled";
            _uow.Bookings.Update(booking);

            if (booking.PromotionId.HasValue)
            {
                var promo = await _uow.Promotions.GetByIdAsync(booking.PromotionId.Value);
                if (promo != null) { promo.Quantity += 1; _uow.Promotions.Update(promo); }
            }
        }
        await _uow.SaveChangesAsync();

        foreach (var booking in affected)
        {
            await _pointService.ReturnUsedPointsAsync(booking, "sân bảo trì");

            // RefundService quyet dinh hoan vao vi hay chuyen khoan ve STK + gui email hoan tien chi tiet
            var (destination, amount, _) = await _refundService.RefundBookingAsync(booking, "sân bảo trì");
            var refundText = destination switch
            {
                RefundDestination.Wallet => $"Số tiền {amount:N0}đ đã được hoàn ngay vào ví của bạn.",
                RefundDestination.BankTransfer => $"Số tiền {amount:N0}đ sẽ được chuyển khoản về tài khoản ngân hàng của bạn " +
                                                  "(sân này không nhận thanh toán bằng ví) - xem email hoàn tiền kèm theo.",
                _ => "Booking của bạn chưa thanh toán nên không phát sinh hoàn tiền."
            };

            // Email thong bao huy (khong hoi y kien - san bao tri thi booking chac chan khong thuc hien duoc),
            // kem goi y dat lai lich khac de bu dap
            await _emailService.SendAsync(booking.User.Email,
                $"[SportBooking] Booking #{booking.BookingId} bị hủy do sân bảo trì",
                $"Xin lỗi bạn, sân <b>{request.Field.FieldName}</b> bảo trì từ {request.StartDate:dd/MM/yyyy} đến {request.EndDate:dd/MM/yyyy} " +
                $"(lý do: {request.Reason}) nên booking ngày {booking.BookingDate:dd/MM/yyyy} lúc {booking.TimeSlot.StartTime:HH\\:mm} của bạn đã được hủy tự động.<br/>" +
                $"{refundText}<br/>" +
                $"Bạn có thể đặt lại khung giờ khác sau ngày bảo trì - rất mong được phục vụ bạn lần sau!");

            // Bao real-time (chuong navbar) cho khach co booking bi huy do bao tri
            await TryNotifyAsync(booking.UserId,
                "Booking bị hủy do sân bảo trì",
                $"Sân {request.Field.FieldName} bảo trì {request.StartDate:dd/MM/yyyy}–{request.EndDate:dd/MM/yyyy} " +
                $"nên booking ngày {booking.BookingDate:dd/MM/yyyy} ({booking.TimeSlot.StartTime:HH\\:mm}) của bạn đã bị hủy. {refundText}",
                $"/Booking/Detail/{booking.BookingId}");
        }

        // Bao cho chu san: yeu cau da duoc duyet
        await TryNotifyAsync(request.OwnerId,
            "Yêu cầu bảo trì được duyệt",
            $"Admin đã duyệt bảo trì sân {request.Field.FieldName} " +
            $"({request.StartDate:dd/MM/yyyy}–{request.EndDate:dd/MM/yyyy}). " +
            $"{affected.Count} booking trùng lịch đã được hủy và hoàn tiền tự động.",
            "/Maintenance/Index");

        return (true, $"Đã duyệt bảo trì. {affected.Count} booking trùng lịch đã được hủy, hoàn tiền và gửi email thông báo.");
    }

    public async Task<(bool Success, string Message)> RejectAsync(int requestId, string? adminNote)
    {
        var request = await _uow.MaintenanceRequests.GetByIdAsync(requestId);
        if (request == null) return (false, "Không tìm thấy yêu cầu.");
        if (request.Status != "Pending") return (false, "Yêu cầu đã được xử lý trước đó.");

        request.Status = "Rejected";
        request.AdminNote = adminNote;
        request.ProcessedAt = DateTime.Now;
        _uow.MaintenanceRequests.Update(request);
        await _uow.SaveChangesAsync();

        // Bao cho chu san: yeu cau bi tu choi (kem ly do cua Admin neu co)
        var field = await _uow.Fields.GetByIdAsync(request.FieldId);
        var noteText = string.IsNullOrWhiteSpace(adminNote) ? "" : $" Lý do: {adminNote}";
        await TryNotifyAsync(request.OwnerId,
            "Yêu cầu bảo trì bị từ chối",
            $"Admin đã từ chối yêu cầu bảo trì sân {field?.FieldName} " +
            $"({request.StartDate:dd/MM/yyyy}–{request.EndDate:dd/MM/yyyy}).{noteText}",
            "/Maintenance/Index");

        return (true, "Đã từ chối yêu cầu bảo trì.");
    }
}
