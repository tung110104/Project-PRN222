using System.ComponentModel.DataAnnotations;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Web.Models;

public class ProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(
        100,
        ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [StringLength(
        20,
        ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Booking> Bookings { get; set; } = new();
}