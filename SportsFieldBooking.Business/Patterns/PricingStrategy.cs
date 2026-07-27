using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Business.Patterns;

/// <summary>
/// Strategy Pattern (nâng cấp mục 2): giá nhiều cấp theo FieldPricingRule.
/// Mỗi chiến lược là một cách lấy giá; PricingEngine chọn chiến lược dựa trên
/// rule khớp nhất (khung giờ / thứ trong tuần / khoảng ngày-mùa) của từng sân.
/// </summary>
public interface IPricingStrategy
{
    decimal CalculatePrice(Field field, TimeSlot slot, DateOnly date);
    string StrategyName { get; }
}

/// <summary>Không rule nào khớp → dùng giá mặc định của sân.</summary>
public class DefaultPricingStrategy : IPricingStrategy
{
    public string StrategyName => "Giá mặc định";
    public decimal CalculatePrice(Field field, TimeSlot slot, DateOnly date) => field.PricePerHour;
}

/// <summary>Giá lấy từ một dòng luật (cao điểm / cuối tuần / mùa…).</summary>
public class RulePricingStrategy : IPricingStrategy
{
    private readonly FieldPricingRule _rule;
    public RulePricingStrategy(FieldPricingRule rule) => _rule = rule;

    public string StrategyName => _rule.RuleName;
    public decimal CalculatePrice(Field field, TimeSlot slot, DateOnly date) => _rule.Price;
}

/// <summary>
/// Bộ máy chọn giá. Hàm thuần — nhận danh sách rule + ngày vàng do service nạp sẵn,
/// không tự truy cập DB (dễ test, giữ đúng vai Pattern).
/// </summary>
public static class PricingEngine
{
    /// <summary>Một rule khớp khi MỌI điều kiện khác NULL đều thỏa.</summary>
    public static bool Matches(FieldPricingRule rule, TimeSlot slot, DateOnly date)
    {
        if (!rule.IsActive) return false;
        if (rule.DayOfWeek.HasValue && rule.DayOfWeek.Value != (int)date.DayOfWeek) return false;
        if (rule.StartDate.HasValue && date < rule.StartDate.Value) return false;
        if (rule.EndDate.HasValue && date > rule.EndDate.Value) return false;
        if (rule.StartTime.HasValue && slot.StartTime < rule.StartTime.Value) return false;
        if (rule.EndTime.HasValue && slot.StartTime >= rule.EndTime.Value) return false;
        return true;
    }

    /// <summary>Rule khớp có Priority cao nhất (hòa thì lấy giá cao hơn).</summary>
    public static FieldPricingRule? GetMatchedRule(
        IEnumerable<FieldPricingRule> rules, TimeSlot slot, DateOnly date)
        => rules.Where(r => Matches(r, slot, date))
                .OrderByDescending(r => r.Priority)
                .ThenByDescending(r => r.Price)
                .FirstOrDefault();

    public static IPricingStrategy GetStrategy(
        IEnumerable<FieldPricingRule> rules, TimeSlot slot, DateOnly date)
    {
        var matched = GetMatchedRule(rules, slot, date);
        return matched == null
            ? new DefaultPricingStrategy()
            : new RulePricingStrategy(matched);
    }

    /// <summary>Giá gốc của 1 slot trong 1 ngày (chưa gồm ưu đãi ngày vàng).</summary>
    public static decimal GetBasePrice(
        Field field, IEnumerable<FieldPricingRule> rules, TimeSlot slot, DateOnly date)
        => GetStrategy(rules, slot, date).CalculatePrice(field, slot, date);

    /// <summary>Ưu đãi ngày vàng (mục 1): giảm % trên giá gốc nếu ngày đó là ngày vàng.</summary>
    public static decimal ApplyGoldenDay(decimal basePrice, GoldenDay? goldenDay)
    {
        if (goldenDay == null || !goldenDay.IsActive || goldenDay.DiscountPercent <= 0)
            return basePrice;
        return basePrice - basePrice * goldenDay.DiscountPercent / 100m;
    }

    /// <summary>Giá cuối cùng của slot = rule khớp nhất rồi trừ ưu đãi ngày vàng.</summary>
    public static decimal GetPrice(
        Field field, IEnumerable<FieldPricingRule> rules, TimeSlot slot, DateOnly date, GoldenDay? goldenDay)
        => ApplyGoldenDay(GetBasePrice(field, rules, slot, date), goldenDay);
}
