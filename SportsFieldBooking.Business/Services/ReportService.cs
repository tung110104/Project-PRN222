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
    public List<RevenueByDay> RevenueByDays { get; set; } = new();
    public List<TopField> TopFields { get; set; } = new();
}

public interface IReportService
{
    Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to);
}

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    public ReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to)
    {
        // Doanh thu tinh theo cac payment da thanh toan (Paid)
        var paidPayments = await _uow.Payments.Query()
            .Include(p => p.Booking).ThenInclude(b => b.BookingDetails).ThenInclude(d => d.Field)
            .Where(p => p.Status == "Paid" && p.PaidAt != null)
            .ToListAsync();

        var inRange = paidPayments
            .Where(p => DateOnly.FromDateTime(p.PaidAt!.Value) >= from &&
                        DateOnly.FromDateTime(p.PaidAt!.Value) <= to)
            .ToList();

        var summary = new ReportSummary
        {
            TotalRevenue = inRange.Sum(p => p.Amount),
            TotalBookings = inRange.Select(p => p.BookingId).Distinct().Count(),
            TotalCustomers = await _uow.Users.Query().CountAsync(u => u.Role.RoleName == "Customer"),
            TotalFields = await _uow.Fields.Query().CountAsync(),
            RevenueByDays = inRange
                .GroupBy(p => DateOnly.FromDateTime(p.PaidAt!.Value))
                .Select(g => new RevenueByDay(g.Key, g.Sum(p => p.Amount), g.Count()))
                .OrderBy(r => r.Date)
                .ToList(),
            TopFields = inRange
                .SelectMany(p => p.Booking.BookingDetails.Where(d => d.Status == "Active"))
                .GroupBy(d => d.Field.FieldName)
                .Select(g => new TopField(g.Key, g.Count(), g.Sum(d => d.Price)))
                .OrderByDescending(t => t.BookingCount)
                .Take(5)
                .ToList()
        };
        return summary;
    }
}
