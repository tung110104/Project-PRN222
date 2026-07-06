using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.Business.Patterns;

/// <summary>Strategy Pattern: tinh gia theo khung gio thuong / cao diem.</summary>
public interface IPricingStrategy
{
    decimal CalculatePrice(Field field, TimeSlot slot);
}

public class NormalPricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(Field field, TimeSlot slot) => field.PricePerHour;
}

public class PeakHourPricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(Field field, TimeSlot slot) => field.PeakPricePerHour;
}

public static class PricingContext
{
    private static readonly TimeOnly PeakStart = new(17, 0);
    private static readonly TimeOnly PeakEnd = new(21, 0);

    public static IPricingStrategy GetStrategy(TimeSlot slot)
        => slot.StartTime >= PeakStart && slot.StartTime < PeakEnd
            ? new PeakHourPricingStrategy()
            : new NormalPricingStrategy();

    public static decimal GetPrice(Field field, TimeSlot slot)
        => GetStrategy(slot).CalculatePrice(field, slot);

    public static bool IsPeak(TimeSlot slot)
        => slot.StartTime >= PeakStart && slot.StartTime < PeakEnd;
}
