using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IMaintenanceService
{
    Task<List<MaintenanceRequest>> GetByOwnerAsync(int ownerId);
    Task<List<MaintenanceRequest>> GetAllAsync(string? status);
    Task<(bool Success, string Message)> CreateAsync(
        int fieldId, int ownerId, DateOnly fromDate, DateOnly toDate, string reason);

    /// <summary>
    /// Admin duyệt (mục 8): sân → Maintenance, TỰ ĐỘNG hủy + hoàn tiền booking trùng khoảng
    /// (không chờ khách đồng ý), gửi email thông báo kèm đền bù điểm.
    /// </summary>
    Task<(bool Success, string Message)> ApproveAsync(int requestId, string? adminNote);
    Task<(bool Success, string Message)> RejectAsync(int requestId, string? adminNote);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IUnitOfWork _uow;
    private readonly IBookingService _bookingService;
    private readonly IPointService _points;
    private readonly ISettingsService _settings;
    private readonly IEmailSender _emailSender;

    public MaintenanceService(
        IUnitOfWork uow,
        IBookingService bookingService,
        IPointService points,
        ISettingsService settings,
        IEmailSender emailSender)
    {
        _uow = uow;
        _bookingService = bookingService;
        _points = points;
        _settings = settings;
        _emailSender = emailSender;
    }

    public Task<List<MaintenanceRequest>> GetByOwnerAsync(int ownerId) =>
        _uow.MaintenanceRequests.Query()
            .Include(r => r.Field)
            .Where(r => r.Field.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public Task<List<MaintenanceRequest>> GetAllAsync(string? status)
    {
        var query = _uow.MaintenanceRequests.Query()
            .Include(r => r.Field)
            .Include(r => r.RequestedBy)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);
        return query.OrderByDescending(r => r.CreatedAt).ToListAsync();
    }

    public async Task<(bool Success, string Message)> CreateAsync(
        int fieldId, int ownerId, DateOnly fromDate, DateOnly toDate, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "Vui lòng nhập lý do bảo trì.");
        if (toDate < fromDate)
            return (false, "Ngày kết thúc phải sau ngày bắt đầu.");
        if (fromDate < DateOnly.FromDateTime(DateTime.Now))
            return (false, "Không thể bảo trì trong quá khứ.");

        var field = await _uow.Fields.Query()
            .FirstOrDefaultAsync(f => f.FieldId == fieldId && f.OwnerId == ownerId);
        if (field == null)
            return (false, "Sân không tồn tại hoặc không thuộc quyền của bạn.");

        var hasPending = await _uow.MaintenanceRequests.Query().AnyAsync(r =>
            r.FieldId == fieldId && r.Status == "Pending");
        if (hasPending)
            return (false, "Sân này đang có yêu cầu bảo trì chờ duyệt.");

        await _uow.MaintenanceRequests.AddAsync(new MaintenanceRequest
        {
            FieldId = fieldId,
            RequestedById = ownerId,
            FromDate = fromDate,
            ToDate = toDate,
            Reason = reason.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();
        return (true, "Đã gửi yêu cầu bảo trì, chờ Admin duyệt.");
    }

    public async Task<(bool Success, string Message)> ApproveAsync(int requestId, string? adminNote)
    {
        var request = await _uow.MaintenanceRequests.Query()
            .Include(r => r.Field)
            .FirstOrDefaultAsync(r => r.MaintenanceRequestId == requestId);
        if (request == null) return (false, "Không tìm thấy yêu cầu.");
        if (request.Status != "Pending") return (false, "Yêu cầu đã được xử lý.");

        request.Status = "Approved";
        request.AdminNote = adminNote;
        request.DecidedAt = DateTime.Now;
        _uow.MaintenanceRequests.Update(request);

        // Sân chuyển trạng thái bảo trì → khóa đặt mới
        request.Field.Status = "Maintenance";
        _uow.Fields.Update(request.Field);
        await _uow.SaveChangesAsync();

        // Booking dính khoảng bảo trì → TỰ ĐỘNG hủy + hoàn tiền (không hỏi ý kiến khách)
        var affected = await _uow.Bookings.Query()
            .Include(b => b.User)
            .Include(b => b.TimeSlot)
            .Where(b => b.FieldId == request.FieldId &&
                        b.BookingDate >= request.FromDate &&
                        b.BookingDate <= request.ToDate &&
                        (b.Status == "Pending" || b.Status == "Confirmed"))
            .ToListAsync();

        var compensation = await _settings.GetIntAsync("MaintenanceCompensationPoints", 20);

        foreach (var booking in affected)
        {
            // Dùng lại toàn bộ logic hủy chuẩn: hoàn lượt mã, hoàn điểm redeem,
            // hoàn ví ngay nếu trả bằng ví, đánh dấu chờ hoàn nếu Momo/Cash
            await _bookingService.CancelAsync(booking.BookingId, booking.UserId, isStaff: true);

            // Cộng điểm đền bù
            if (compensation > 0)
            {
                await _points.ApplyChangeAsync(booking.UserId, "Adjust", compensation,
                    booking.BookingId, $"Đền bù do sân bảo trì (booking #{booking.BookingId})");
            }

            // Email thông báo ngay khi hủy: lý do + hoàn tiền + lựa chọn bù đắp
            try
            {
                await _emailSender.SendEmailAsync(booking.User.Email, booking.User.FullName,
                    $"Booking #{booking.BookingId} bị hủy do sân bảo trì",
                    $"""
                    <div style="font-family:Arial,sans-serif;max-width:560px;margin:auto">
                        <h2 style="color:#dc3545">Sân bảo trì — booking của bạn đã được hủy</h2>
                        <p>Xin chào {booking.User.FullName},</p>
                        <p>Sân <strong>{request.Field.FieldName}</strong> bảo trì từ
                           <strong>{request.FromDate:dd/MM/yyyy}</strong> đến
                           <strong>{request.ToDate:dd/MM/yyyy}</strong>
                           (lý do: {request.Reason}).</p>
                        <p>Booking #{booking.BookingId} ngày {booking.BookingDate:dd/MM/yyyy}
                           ({booking.TimeSlot.StartTime:HH\:mm}) đã được <strong>hủy và hoàn tiền</strong>:
                           trả bằng ví thì tiền đã về ví ngay, trả Momo/tiền mặt sẽ được chủ sân hoàn thủ công.</p>
                        <p>Chúng tôi đã cộng <strong>{compensation} điểm đền bù</strong> vào tài khoản của bạn.</p>
                        <p>Bạn có thể đặt lại khung giờ khác tại trang tìm sân của hệ thống.</p>
                    </div>
                    """);
            }
            catch { /* demo: bỏ qua lỗi gửi mail */ }
        }

        return (true, $"Đã duyệt bảo trì. {affected.Count} booking bị ảnh hưởng đã được hủy, hoàn tiền và gửi email.");
    }

    public async Task<(bool Success, string Message)> RejectAsync(int requestId, string? adminNote)
    {
        var request = await _uow.MaintenanceRequests.GetByIdAsync(requestId);
        if (request == null) return (false, "Không tìm thấy yêu cầu.");
        if (request.Status != "Pending") return (false, "Yêu cầu đã được xử lý.");

        request.Status = "Rejected";
        request.AdminNote = adminNote;
        request.DecidedAt = DateTime.Now;
        _uow.MaintenanceRequests.Update(request);
        await _uow.SaveChangesAsync();
        return (true, "Đã từ chối yêu cầu bảo trì.");
    }
}
