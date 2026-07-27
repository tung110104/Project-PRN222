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

    public ReviewService(IUnitOfWork uow, IPointService pointService)
    {
        _uow = uow;
        _pointService = pointService;
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

        return (true, "Cảm ơn bạn đã đánh giá! Bạn được cộng điểm thưởng.");
    }
}
