namespace SportsFieldBooking.Business.Patterns;

/// <summary>Singleton Pattern: cau hinh dung chung cua he thong.</summary>
public sealed class AppConfigSingleton
{
    private static readonly Lazy<AppConfigSingleton> _instance =
        new(() => new AppConfigSingleton());

    public static AppConfigSingleton Instance => _instance.Value;

    private AppConfigSingleton() { }

    /// <summary>So gio toi thieu truoc gio da de duoc phep huy booking.</summary>
    public int CancelBeforeHours { get; } = 2;

    /// <summary>So ngay toi da duoc dat truoc.</summary>
    public int MaxAdvanceBookingDays { get; } = 30;

    // ----- Cau hinh tich diem -----
    /// <summary>So tien (VND) quy doi ra 1 diem khi hoan thanh booking.</summary>
    public decimal VndPerPoint { get; } = 10_000m;

    /// <summary>Gia tri (VND) cua 1 diem khi dung diem tru tien.</summary>
    public decimal PointValueVnd { get; } = 100m;

    /// <summary>Toi da bao nhieu % gia tri booking duoc tru bang diem.</summary>
    public int MaxRedeemPercent { get; } = 50;

    /// <summary>Diem thuong khi viet danh gia san.</summary>
    public int ReviewBonusPoints { get; } = 10;

    // ----- Cau hinh vi tien ao -----
    /// <summary>So tien nap toi thieu moi lan (VND).</summary>
    public decimal MinDepositAmount { get; } = 10_000m;

    /// <summary>
    /// Khuyen mai nap tien: nap tu MinAmount tro len duoc tang them BonusAmount vao vi
    /// (giao dich loai "Bonus"). Chi ap dung muc cao nhat thoa man.
    /// </summary>
    public IReadOnlyList<DepositBonusTier> DepositBonusTiers { get; } = new List<DepositBonusTier>
    {
        new(500_000m, 50_000m),
        new(200_000m, 15_000m),
    };

    /// <summary>Muc thuong nap cao nhat ma so tien nap dat duoc (null neu khong co).</summary>
    public DepositBonusTier? GetDepositBonus(decimal amount) =>
        DepositBonusTiers.OrderByDescending(t => t.MinAmount).FirstOrDefault(t => amount >= t.MinAmount);

    /// <summary>Cac goi doi diem lay voucher giam gia (voucher gui qua email).</summary>
    public IReadOnlyList<VoucherOption> VoucherOptions { get; } = new List<VoucherOption>
    {
        new(100, 5,  20_000m,  30),
        new(200, 10, 50_000m,  30),
        new(500, 15, 150_000m, 60),
    };
}

/// <summary>Muc khuyen mai nap tien: nap tu MinAmount duoc tang BonusAmount.</summary>
public record DepositBonusTier(decimal MinAmount, decimal BonusAmount);

/// <summary>Goi doi diem lay voucher: ton PointCost diem, nhan ma giam DiscountPercent% (toi da MaxDiscount), han dung ValidityDays ngay.</summary>
public record VoucherOption(int PointCost, int DiscountPercent, decimal MaxDiscount, int ValidityDays);

/// <summary>Hang thanh vien xet theo LifetimePoints (diem tich luy tron doi, khong giam khi tieu diem).</summary>
public record MembershipTier(string Name, int MinLifetimePoints, int DiscountPercent, string BadgeCss);

public static class MembershipTiers
{
    public static readonly IReadOnlyList<MembershipTier> All = new List<MembershipTier>
    {
        new("Kim cương", 5000, 10, "bg-info text-dark"),
        new("Vàng",      2000, 5,  "bg-warning text-dark"),
        new("Bạc",       500,  3,  "bg-secondary"),
        new("Thường",    0,    0,  "bg-light text-dark border"),
    };

    public static MembershipTier GetTier(int lifetimePoints)
        => All.First(t => lifetimePoints >= t.MinLifetimePoints);
}
