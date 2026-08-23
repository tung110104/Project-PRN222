using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Business.Patterns;

/// <summary>
/// Strategy Pattern: tinh gia 1 khung gio cua san theo ngay cu the.
/// Thu tu uu tien: rule bang gia (gio/ngay/thang-mua) -> gia co ban; sau do nhan he so ngay vang neu co.
/// </summary>
public interface IPricingStrategy
{
    decimal CalculatePrice(Field field, TimeSlot slot, DateOnly date);
}

/// <summary>Gia co ban cua san - dung khi khong co rule nao khop.</summary>
public class BasePricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(Field field, TimeSlot slot, DateOnly date) => field.PricePerHour;
}

/// <summary>Gia theo bang gia chi tiet (FieldPricingRules): khung gio + loai ngay + khoang thang.</summary>
public class RuleBasedPricingStrategy : IPricingStrategy
{
    private readonly FieldPricingRule _rule;
    public RuleBasedPricingStrategy(FieldPricingRule rule) => _rule = rule;
    public decimal CalculatePrice(Field field, TimeSlot slot, DateOnly date) => _rule.Price;
}

public static class PricingEngine
{
    /// <summary>Rule co khop voi (slot, date) khong: khung gio + loai ngay + khoang thang (ho tro mua vat qua nam, vd 11->2).</summary>
    public static bool Matches(FieldPricingRule r, TimeSlot slot, DateOnly date)
    {
        if (!r.IsActive) return false;
        if (slot.StartTime < r.StartTime || slot.StartTime >= r.EndTime) return false;

        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        if (r.DayType == "Weekday" && isWeekend) return false;
        if (r.DayType == "Weekend" && !isWeekend) return false;

        if (r.StartMonth.HasValue && r.EndMonth.HasValue)
        {
            var m = date.Month;
            var inRange = r.StartMonth <= r.EndMonth
                ? m >= r.StartMonth && m <= r.EndMonth
                : m >= r.StartMonth || m <= r.EndMonth; // mua vat qua nam (vd thang 11 -> thang 2)
            if (!inRange) return false;
        }
        return true;
    }

    /// <summary>
    /// Chon strategy phu hop: rule khop co Priority cao nhat (cu the hon thang), khong co -> gia co ban.
    /// field.PricingRules phai duoc Include truoc khi goi.
    /// </summary>
    public static IPricingStrategy GetStrategy(Field field, TimeSlot slot, DateOnly date)
    {
        var rule = field.PricingRules
            .Where(r => Matches(r, slot, date))
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.StartMonth.HasValue) // co gioi han thang = cu the hon
            .ThenByDescending(r => r.DayType != "All")
            .FirstOrDefault();

        return rule != null ? new RuleBasedPricingStrategy(rule) : new BasePricingStrategy();
    }

    /// <summary>
    /// Gia cuoi cung cua 1 slot trong 1 ngay: gia theo strategy nhan them he so ngay vang (neu ngay do la ngay vang).
    /// goldenDay = ngay vang ap dung cho san nay (rieng cua san hoac toan he thong), null neu khong co.
    /// </summary>
    public static decimal GetPrice(Field field, TimeSlot slot, DateOnly date, GoldenDay? goldenDay)
    {
        var price = GetStrategy(field, slot, date).CalculatePrice(field, slot, date);
        if (goldenDay != null && goldenDay.IsActive)
            price = Math.Round(price * goldenDay.PriceMultiplier / 1000m) * 1000m; // lam tron nghin dong
        return price;
    }
}
