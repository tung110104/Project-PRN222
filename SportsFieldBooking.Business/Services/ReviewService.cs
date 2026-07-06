using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public interface IReviewService
{
    Task<(bool Success, string Message)> AddReviewAsync(
        int bookingId,
        int userId,
        int rating,
        string? comment);

    Task<(bool Success, string Message)> UpdateReviewAsync(
        int bookingId,
        int userId,
        int rating,
        string? comment);

    Task<(bool Success, string Message)> DeleteReviewAsync(
        int bookingId,
        int userId);
}

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _uow;

    public ReviewService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<(bool Success, string Message)>
        AddReviewAsync(
            int bookingId,
            int userId,
            int rating,
            string? comment)
    {
        var validation = ValidateReview(rating, comment);

        if (!validation.Success)
            return validation;

        var booking = await _uow.Bookings.Query()
            .Include(b => b.BookingDetails)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b =>
                b.BookingId == bookingId &&
                b.UserId == userId);

        if (booking == null)
            return (false, "Không tìm thấy booking.");

        if (booking.Status != "Completed")
        {
            return (
                false,
                "Chỉ được đánh giá sau khi đã sử dụng sân."
            );
        }

        if (booking.Review != null)
        {
            return (
                false,
                "Bạn đã đánh giá booking này rồi."
            );
        }

        var detail = booking.BookingDetails.FirstOrDefault();

        if (detail == null)
            return (false, "Booking không có thông tin sân.");

        await _uow.Reviews.AddAsync(new Review
        {
            BookingId = bookingId,
            UserId = userId,
            FieldId = detail.FieldId,
            Rating = rating,
            Comment = comment?.Trim(),
            CreatedAt = DateTime.Now
        });

        await _uow.SaveChangesAsync();

        return (true, "Cảm ơn bạn đã đánh giá!");
    }

    public async Task<(bool Success, string Message)>
        UpdateReviewAsync(
            int bookingId,
            int userId,
            int rating,
            string? comment)
    {
        var validation = ValidateReview(rating, comment);

        if (!validation.Success)
            return validation;

        var review = await _uow.Reviews.Query()
            .FirstOrDefaultAsync(r =>
                r.BookingId == bookingId &&
                r.UserId == userId);

        if (review == null)
            return (false, "Không tìm thấy đánh giá.");

        review.Rating = rating;
        review.Comment = comment?.Trim();
        review.CreatedAt = DateTime.Now;

        _uow.Reviews.Update(review);
        await _uow.SaveChangesAsync();

        return (true, "Cập nhật đánh giá thành công.");
    }

    public async Task<(bool Success, string Message)>
        DeleteReviewAsync(
            int bookingId,
            int userId)
    {
        var review = await _uow.Reviews.Query()
            .FirstOrDefaultAsync(r =>
                r.BookingId == bookingId &&
                r.UserId == userId);

        if (review == null)
            return (false, "Không tìm thấy đánh giá.");

        _uow.Reviews.Remove(review);
        await _uow.SaveChangesAsync();

        return (true, "Đã xóa đánh giá.");
    }

    private static (bool Success, string Message)
        ValidateReview(
            int rating,
            string? comment)
    {
        if (rating is < 1 or > 5)
        {
            return (
                false,
                "Điểm đánh giá phải từ 1 đến 5 sao."
            );
        }

        if (comment?.Length > 1000)
        {
            return (
                false,
                "Bình luận không được vượt quá 1.000 ký tự."
            );
        }

        return (true, string.Empty);
    }
}