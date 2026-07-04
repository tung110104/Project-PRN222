using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SportsFieldBooking.DataAccess.Entities;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!;
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Phone { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Field> OwnedFields { get; set; } = new List<Field>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public class FieldType
{
    public int FieldTypeId { get; set; }
    public string TypeName { get; set; } = null!;
    public virtual ICollection<Field> Fields { get; set; } = new List<Field>();
}

public class Field
{
    public int FieldId { get; set; }
    public string FieldName { get; set; } = null!;
    public int FieldTypeId { get; set; }
    public int OwnerId { get; set; }
    public string Address { get; set; } = null!;
    public string District { get; set; } = null!;
    public string City { get; set; } = null!;
    public decimal PricePerHour { get; set; }
    public decimal PeakPricePerHour { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }

    public virtual FieldType FieldType { get; set; } = null!;
    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<FieldImage> FieldImages { get; set; } = new List<FieldImage>();
    public virtual ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}

public class FieldImage
{
    public int ImageId { get; set; }
    public int FieldId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public virtual Field Field { get; set; } = null!;
}

public class TimeSlot
{
    public int TimeSlotId { get; set; }
    public int FieldId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual Field Field { get; set; } = null!;
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
}

public class Promotion
{
    public int PromotionId { get; set; }
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public int DiscountPercent { get; set; }
    public decimal MaxDiscount { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Quantity { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

public class Booking
{
    public int BookingId { get; set; }
    public int UserId { get; set; }
    public int? PromotionId { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Promotion? Promotion { get; set; }
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual Review? Review { get; set; }
}

public class BookingDetail
{
    public int BookingDetailId { get; set; }
    public int BookingId { get; set; }
    public int FieldId { get; set; }
    public int TimeSlotId { get; set; }
    public DateOnly BookingDate { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";

    public virtual Booking Booking { get; set; } = null!;
    public virtual Field Field { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
}

public class Payment
{
    public int PaymentId { get; set; }
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string? TransactionCode { get; set; }
    public DateTime? PaidAt { get; set; }
    public virtual Booking Booking { get; set; } = null!;
}

public class Review
{
    public int ReviewId { get; set; }
    public int FieldId { get; set; }
    public int UserId { get; set; }
    public int BookingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Field Field { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Booking Booking { get; set; } = null!;
}
