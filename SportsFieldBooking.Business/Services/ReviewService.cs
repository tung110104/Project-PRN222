using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IReviewService
{
    Task<(bool Success, string Message)> AddReviewAsync(int bookingId, int userId, int rating, string? comment);
}

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _uow;
    private readonly IPointService _pointService;
    private readonly INotificationService _notificationService;

    public ReviewService(IUnitOfWork uow, IPointService pointService, INotificationService notificationService)
    {
        _uow = uow;
        _pointService = pointService;
        _notificationService = notificationService;
    }

    // View da an form khi chua du dieu kien, nhung o day van kiem tra lai toan bo
    // (nguyen tac "khong tin client" - nguoi dung co the tu POST khong qua form).
    public async Task<(bool Success, string Message)> AddReviewAsync(int bookingId, int userId, int rating, string? comment)
    {
        if (rating is < 1 or > 5) return (false, "Điểm đánh giá phải từ 1 đến 5.");

        // UserId trong query -> khong danh gia ho booking cua nguoi khac duoc
        var booking = await _uow.Bookings.Query()
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status != "Completed") return (false, "Chỉ đánh giá được sau khi đã sử dụng sân.");
        if (booking.Review != null) return (false, "Bạn đã đánh giá booking này rồi.");

        await _uow.Reviews.AddAsync(new Review
        {
            BookingId = bookingId,
            UserId = userId,
            FieldId = booking.FieldId, // Booking gan truc tiep FieldId (da bo BookingDetail)
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();

        // Thuong diem khuyen khich khach review
        await _pointService.EarnForReviewAsync(userId, bookingId);

        // Bao chu san: san vua nhan duoc danh gia moi
        var field = await _uow.Fields.GetByIdAsync(booking.FieldId);
        if (field != null && field.OwnerId != userId)
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            var stars = string.Concat(Enumerable.Repeat("★", rating)) + string.Concat(Enumerable.Repeat("☆", 5 - rating));
            var commentText = string.IsNullOrWhiteSpace(comment) ? "" : $" — \"{comment}\"";
            try
            {
                await _notificationService.NotifyAsync(field.OwnerId,
                    "Đánh giá mới",
                    $"{user?.FullName ?? "Khách"} đánh giá sân {field.FieldName}: {stars} ({rating}/5){commentText}",
                    $"/Field/Detail/{field.FieldId}");
            }
            catch { /* bo qua */ }
        }

        return (true, "Cảm ơn bạn đã đánh giá! Bạn được cộng điểm thưởng.");
    }
}
