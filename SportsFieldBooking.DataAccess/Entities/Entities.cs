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
    // So du diem hien tai (co the tieu); LifetimePoints chi tang - dung de xet hang thanh vien
    public int Points { get; set; }
    public int LifetimePoints { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual Wallet? Wallet { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Field> OwnedFields { get; set; } = new List<Field>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
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
    // Dia chi chi tiet: so nha/duong + Phuong/Xa + Tinh/Thanh pho (chon tu API hanh chinh VN)
    public string Address { get; set; } = null!;
    public string Ward { get; set; } = null!;
    public string Province { get; set; } = null!;
    // Gia co ban (fallback khi khong co rule nao khop trong FieldPricingRules)
    public decimal PricePerHour { get; set; }
    // Chu san bat/tat cho phep thanh toan bang vi tien ao cho san nay
    public bool AcceptWalletPayment { get; set; }
    // % hoan tien vao vi khi khach thanh toan bang vi (0 = khong cashback)
    public int CashbackPercent { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Active"; // Active / Maintenance / Closed
    public DateTime CreatedAt { get; set; }

    public virtual FieldType FieldType { get; set; } = null!;
    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<FieldImage> FieldImages { get; set; } = new List<FieldImage>();
    public virtual ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<FieldPricingRule> PricingRules { get; set; } = new List<FieldPricingRule>();
    public virtual ICollection<GoldenDay> GoldenDays { get; set; } = new List<GoldenDay>();
    public virtual ICollection<MaintenanceRequest> MaintenanceRequests { get; set; } = new List<MaintenanceRequest>();
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
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

/// <summary>
/// Bang gia chi tiet cua tung san: theo khung gio + loai ngay (thuong/cuoi tuan) + khoang thang (mua).
/// Rule co Priority cao nhat trong cac rule khop se duoc ap dung; khong rule nao khop -> PricePerHour cua san.
/// </summary>
public class FieldPricingRule
{
    public int PricingRuleId { get; set; }
    public int FieldId { get; set; }
    public string? RuleName { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string DayType { get; set; } = "All"; // All / Weekday / Weekend
    // Khoang thang ap dung (1-12), null = quanh nam. Vd mua he: StartMonth=5, EndMonth=8
    public int? StartMonth { get; set; }
    public int? EndMonth { get; set; }
    public decimal Price { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual Field Field { get; set; } = null!;
}

/// <summary>
/// Ngay vang: ngay dac biet (le, Tet, su kien). FieldId = null la ngay vang toan he thong (Admin tao),
/// co FieldId la ngay vang rieng cua san (chu san tao). Gia nhan PriceMultiplier, diem nhan PointsMultiplier.
/// </summary>
public class GoldenDay
{
    public int GoldenDayId { get; set; }
    public int? FieldId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;
    [Column(TypeName = "decimal(4,2)")]
    public decimal PriceMultiplier { get; set; } = 1m;
    public int PointsMultiplier { get; set; } = 2;
    public bool IsActive { get; set; } = true;
    public virtual Field? Field { get; set; }
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
    // Null = khuyen mai toan he thong (Admin tao); co gia tri = ma cua chu san, chi ap dung cho san cua ho
    public int? OwnerId { get; set; }
    public virtual User? Owner { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

/// <summary>
/// 1 booking = 1 san + 1 khung gio + 1 ngay (da bo bang BookingDetail).
/// UnitPrice la gia goc theo bang gia; DiscountAmount don tat ca giam gia (KM + hang thanh vien + diem);
/// TotalAmount la so tien phai tra cuoi cung.
/// </summary>
public class Booking
{
    public int BookingId { get; set; }
    public int UserId { get; set; }
    public int FieldId { get; set; }
    public int TimeSlotId { get; set; }
    public DateOnly BookingDate { get; set; }
    public int? PromotionId { get; set; }
    public string Status { get; set; } = "Pending"; // Pending / Confirmed / Completed / Cancelled
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsed { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    // Nguoi thao tac tao booking (Staff/Admin dat ho khach); null = khach tu dat
    public int? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Field Field { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
    public virtual Promotion? Promotion { get; set; }
    public virtual User? CreatedBy { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual Review? Review { get; set; }
}

public class Payment
{
    public int PaymentId { get; set; }
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = null!; // VNPay / Momo / Cash / Wallet
    public string Status { get; set; } = "Pending"; // Pending / Paid / Refunded / Failed
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

/// <summary>Vi tien ao noi bo. Moi bien dong so du deu phai di qua WalletTransaction.</summary>
public class Wallet
{
    public int WalletId { get; set; }
    public int UserId { get; set; }
    public decimal Balance { get; set; }
    public bool IsLocked { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual User User { get; set; } = null!;
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

public class WalletTransaction
{
    public int WalletTransactionId { get; set; }
    public int WalletId { get; set; }
    public string Type { get; set; } = null!; // Deposit / Payment / Refund / Cashback / Bonus / Adjust
    public decimal Amount { get; set; }       // duong = cong tien, am = tru tien
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
    public int? BookingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual Wallet Wallet { get; set; } = null!;
}

public class PointTransaction
{
    public int PointTransactionId { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = null!; // Earn / Redeem / ReviewBonus / Revoke / Adjust
    public int Points { get; set; }           // duong = cong diem, am = tru diem
    public string? Description { get; set; }
    public int? BookingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual User User { get; set; } = null!;
}

/// <summary>Chu san gui yeu cau bao tri, Admin duyet/tu choi.</summary>
public class MaintenanceRequest
{
    public int MaintenanceRequestId { get; set; }
    public int FieldId { get; set; }
    public int OwnerId { get; set; }
    public string Reason { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public virtual Field Field { get; set; } = null!;
    public virtual User Owner { get; set; } = null!;
}
