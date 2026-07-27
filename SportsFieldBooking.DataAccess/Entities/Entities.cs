namespace SportsFieldBooking.DataAccess.Entities;

// ===================== NGƯỜI DÙNG & VAI TRÒ =====================

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

    /// <summary>Điểm hiện có (dùng để đổi/trừ khi thanh toán).</summary>
    public int PointBalance { get; set; }

    /// <summary>Tổng điểm tích lũy trọn đời (tính hạng thành viên, tiêu điểm không giảm).</summary>
    public int LifetimePoints { get; set; }

    public virtual Role Role { get; set; } = null!;
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Booking> CreatedBookings { get; set; } = new List<Booking>();
    public virtual ICollection<Field> OwnedFields { get; set; } = new List<Field>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual Wallet? Wallet { get; set; }
    public virtual ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
}

// ===================== SÂN =====================

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

    // Địa chỉ chi tiết (mục 7): Tỉnh/TP → Phường/Xã → đường, số nhà (đổ từ API hành chính)
    public string Street { get; set; } = null!;
    public string WardCode { get; set; } = null!;
    public string WardName { get; set; } = null!;
    public string ProvinceCode { get; set; } = null!;
    public string ProvinceName { get; set; } = null!;

    /// <summary>Giá mặc định/giờ — dùng khi không có FieldPricingRule nào khớp.</summary>
    public decimal PricePerHour { get; set; }

    public string? Description { get; set; }
    public string Status { get; set; } = "Active"; // Active / Maintenance / Closed

    /// <summary>Chủ sân bật/tắt chấp nhận thanh toán bằng ví (mục 4).</summary>
    public bool AcceptWalletPayment { get; set; }

    /// <summary>% hoàn tiền vào ví khi khách trả bằng ví (0 = tắt cashback).</summary>
    public int CashbackPercent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual FieldType FieldType { get; set; } = null!;
    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<FieldImage> FieldImages { get; set; } = new List<FieldImage>();
    public virtual ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<FieldPricingRule> PricingRules { get; set; } = new List<FieldPricingRule>();
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

// ===================== GIÁ NHIỀU CẤP (mục 2) =====================

/// <summary>
/// Một dòng luật giá của sân. Điều kiện nào NULL nghĩa là "không xét".
/// Khi tính tiền chọn rule khớp có Priority cao nhất; không rule nào khớp → Field.PricePerHour.
/// </summary>
public class FieldPricingRule
{
    public int PricingRuleId { get; set; }
    public int FieldId { get; set; }
    public string RuleName { get; set; } = null!;

    /// <summary>0=Chủ nhật … 6=Thứ 7 (theo DayOfWeek của .NET). NULL = mọi thứ trong tuần.</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>Khoảng ngày áp dụng (mùa / dịp lễ). NULL = quanh năm.</summary>
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Khung giờ áp dụng (giờ cao điểm riêng của sân). NULL = cả ngày.</summary>
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    public decimal Price { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Field Field { get; set; } = null!;
}

/// <summary>Ngày vàng (mục 1): Admin cấu hình toàn hệ thống (FieldId NULL) hoặc chủ sân cấu hình theo sân.</summary>
public class GoldenDay
{
    public int GoldenDayId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Hệ số nhân điểm khi đặt vào ngày vàng (mặc định 2).</summary>
    public int PointMultiplier { get; set; } = 2;

    /// <summary>% ưu đãi giá ngày vàng (0 = không giảm, chỉ nhân điểm).</summary>
    public int DiscountPercent { get; set; }

    /// <summary>NULL = áp dụng toàn hệ thống; có giá trị = riêng một sân.</summary>
    public int? FieldId { get; set; }

    public bool IsActive { get; set; } = true;
    public virtual Field? Field { get; set; }
}

// ===================== KHUYẾN MÃI (mục 11) =====================

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

    /// <summary>NULL = mã toàn hệ thống (Admin); có giá trị = mã của chủ sân này.</summary>
    public int? OwnerId { get; set; }

    /// <summary>NULL = áp dụng mọi sân (trong phạm vi owner nếu có); có giá trị = riêng một sân.</summary>
    public int? FieldId { get; set; }

    public virtual User? Owner { get; set; }
    public virtual Field? Field { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

// ===================== ĐẶT SÂN (mục 9: bỏ BookingDetail) =====================

/// <summary>1 booking = 1 sân + 1 khung giờ + 1 ngày.</summary>
public class Booking
{
    public int BookingId { get; set; }

    /// <summary>Khách sử dụng sân (với khách vãng lai đặt hộ: tài khoản người tạo, kèm GuestName/Phone).</summary>
    public int UserId { get; set; }

    /// <summary>Người tạo đơn nếu là Staff/Admin đặt hộ (mục 10). NULL = khách tự đặt.</summary>
    public int? CreatedById { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }

    public int FieldId { get; set; }
    public int TimeSlotId { get; set; }
    public DateOnly BookingDate { get; set; }

    /// <summary>Giá gốc theo rule (trước mọi giảm giá).</summary>
    public decimal UnitPrice { get; set; }

    public int? PromotionId { get; set; }
    public decimal PromoDiscount { get; set; }

    /// <summary>Giảm theo hạng thành viên (mục 5).</summary>
    public decimal TierDiscount { get; set; }

    /// <summary>Số điểm đã quy đổi + giá trị tiền tương ứng.</summary>
    public int PointsUsed { get; set; }
    public decimal PointsDiscount { get; set; }

    /// <summary>Số tiền phải trả sau mọi giảm giá.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Điểm đã cộng khi hoàn thành (để thu hồi đúng số nếu hủy).</summary>
    public int PointsEarned { get; set; }

    public string Status { get; set; } = "Pending"; // Pending / Confirmed / Completed / Cancelled
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual User? CreatedBy { get; set; }
    public virtual Field Field { get; set; } = null!;
    public virtual TimeSlot TimeSlot { get; set; } = null!;
    public virtual Promotion? Promotion { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual Review? Review { get; set; }
}

public class Payment
{
    public int PaymentId { get; set; }
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = null!; // Momo / Cash / Wallet
    public string Status { get; set; } = "Pending"; // Pending / Paid / Refunded / RefundPending
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

// ===================== VÍ TIỀN ẢO (mục 4) =====================

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

/// <summary>Mọi biến động ví đều có log — không bao giờ sửa Balance mà không có dòng này.</summary>
public class WalletTransaction
{
    public int WalletTransactionId { get; set; }
    public int WalletId { get; set; }

    /// <summary>Deposit / Payment / Refund / Cashback / Bonus / Adjust</summary>
    public string Type { get; set; } = null!;

    /// <summary>Dương = cộng vào ví, âm = trừ khỏi ví.</summary>
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public int? BookingId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Wallet Wallet { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}

// ===================== TÍCH ĐIỂM (mục 5) =====================

/// <summary>Mọi biến động điểm đều đi qua bảng này.</summary>
public class PointTransaction
{
    public int PointTransactionId { get; set; }
    public int UserId { get; set; }

    /// <summary>Earn / Revoke / Redeem / ReviewBonus / FirstBookingBonus / VoucherRedeem / Adjust</summary>
    public string Type { get; set; } = null!;

    /// <summary>Dương = cộng điểm, âm = trừ điểm.</summary>
    public int Points { get; set; }
    public int BalanceAfter { get; set; }
    public int? BookingId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
}

// ===================== BẢO TRÌ (mục 8) =====================

public class MaintenanceRequest
{
    public int MaintenanceRequestId { get; set; }
    public int FieldId { get; set; }
    public int RequestedById { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = "Pending"; // Pending / Approved / Rejected
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    public virtual Field Field { get; set; } = null!;
    public virtual User RequestedBy { get; set; } = null!;
}

// ===================== CẤU HÌNH HỆ THỐNG =====================

/// <summary>Tham số Admin cấu hình được: tỷ lệ tiền↔điểm, ngưỡng hạng, % giảm hạng, bonus nạp…</summary>
public class SystemSetting
{
    public string SettingKey { get; set; } = null!;
    public string SettingValue { get; set; } = null!;
}
