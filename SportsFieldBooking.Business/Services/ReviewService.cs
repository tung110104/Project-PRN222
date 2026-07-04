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
    public ReviewService(IUnitOfWork uow) => _uow = uow;

    public async Task<(bool Success, string Message)> AddReviewAsync(int bookingId, int userId, int rating, string? comment)
    {
        if (rating is < 1 or > 5) return (false, "Điểm đánh giá phải từ 1 đến 5.");

        var booking = await _uow.Bookings.Query()
            .Include(b => b.BookingDetails)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

        if (booking == null) return (false, "Không tìm thấy booking.");
        if (booking.Status != "Completed") return (false, "Chỉ đánh giá được sau khi đã sử dụng sân.");
        if (booking.Review != null) return (false, "Bạn đã đánh giá booking này rồi.");

        var fieldId = booking.BookingDetails.First().FieldId;
        await _uow.Reviews.AddAsync(new Review
        {
            BookingId = bookingId,
            UserId = userId,
            FieldId = fieldId,
            Rating = rating,
            Comment = comment,
            CreatedAt = DateTime.Now
        });
        await _uow.SaveChangesAsync();
        return (true, "Cảm ơn bạn đã đánh giá!");
    }
}
