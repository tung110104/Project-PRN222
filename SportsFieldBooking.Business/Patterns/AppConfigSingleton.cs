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
}
