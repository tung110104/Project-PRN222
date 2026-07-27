using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public record RevenueByDay(DateOnly Date, decimal Revenue, int BookingCount);
public record TopField(string FieldName, int BookingCount, decimal Revenue);

public class ReportSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalFields { get; set; }
    public decimal TotalWalletBalance { get; set; }
    public List<RevenueByDay> RevenueByDays { get; set; } = new();
    public List<TopField> TopFields { get; set; } = new();
}

public interface IReportService
{
    /// <summary>ownerId != null → chỉ tính doanh thu các sân của chủ sân đó.</summary>
    Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to, int? ownerId = null);
}

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    public ReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(TimeOnly.MaxValue);

        // Doanh thu = payment Paid, lọc ngày ngay trong SQL
        var paidQuery = _uow.Payments.Query()
            .Include(p => p.Booking).ThenInclude(b => b.Field)
            .Where(p => p.Status == "Paid" && p.PaidAt != null &&
                        p.PaidAt >= fromDt && p.PaidAt <= toDt);

        if (ownerId.HasValue)
            paidQuery = paidQuery.Where(p => p.Booking.Field.OwnerId == ownerId.Value);

        var inRange = await paidQuery.ToListAsync();

        var fieldsQuery = _uow.Fields.Query().AsQueryable();
        if (ownerId.HasValue)
            fieldsQuery = fieldsQuery.Where(f => f.OwnerId == ownerId.Value);

        return new ReportSummary
        {
            TotalRevenue = inRange.Sum(p => p.Amount),
            TotalBookings = inRange.Select(p => p.BookingId).Distinct().Count(),
            TotalCustomers = await _uow.Users.Query().CountAsync(u => u.Role.RoleName == "Customer"),
            TotalFields = await fieldsQuery.CountAsync(),
            TotalWalletBalance = await _uow.Wallets.Query().SumAsync(w => (decimal?)w.Balance) ?? 0,
            RevenueByDays = inRange
                .GroupBy(p => DateOnly.FromDateTime(p.PaidAt!.Value))
                .Select(g => new RevenueByDay(g.Key, g.Sum(p => p.Amount), g.Count()))
                .OrderBy(r => r.Date)
                .ToList(),
            TopFields = inRange
                .GroupBy(p => p.Booking.Field.FieldName)
                .Select(g => new TopField(g.Key, g.Count(), g.Sum(p => p.Amount)))
                .OrderByDescending(t => t.BookingCount)
                .Take(5)
                .ToList()
        };
    }
}
