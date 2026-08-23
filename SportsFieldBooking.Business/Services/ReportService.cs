using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.DataAccess.Repositories;

namespace SportsFieldBooking.Business.Services;

public record RevenueByDay(DateOnly Date, decimal Revenue, int BookingCount);
public record TopField(string FieldName, int BookingCount, decimal Revenue);
/// <summary>Doanh thu cua TUNG san trong khoang bao cao (liet ke ca san chua co doanh thu).</summary>
public record FieldRevenue(int FieldId, string FieldName, int BookingCount, decimal Revenue);

public class ReportSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalFields { get; set; }
    public List<RevenueByDay> RevenueByDays { get; set; } = new();
    public List<TopField> TopFields { get; set; } = new();
    public List<FieldRevenue> FieldRevenues { get; set; } = new();
}

public interface IReportService
{
    /// <summary>ownerId = null: bao cao toan he thong (Admin); co gia tri: chi san cua chu san do.</summary>
    Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to, int? ownerId = null);
}

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    public ReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<ReportSummary> GetSummaryAsync(DateOnly from, DateOnly to, int? ownerId = null)
    {
        // Doanh thu tinh theo payment da thanh toan (Paid): chi tinh tien da thu that.
        // Booking gio gan truc tiep voi Field (da bo BookingDetail) nen chi can Include 2 cap.
        var paidQuery = _uow.Payments.Query()
            .Include(p => p.Booking).ThenInclude(b => b.Field)
            .Where(p => p.Status == "Paid" && p.PaidAt != null);

        if (ownerId.HasValue)
            paidQuery = paidQuery.Where(p => p.Booking.Field.OwnerId == ownerId.Value);

        var paidPayments = await paidQuery.ToListAsync();

        var inRange = paidPayments
            .Where(p => DateOnly.FromDateTime(p.PaidAt!.Value) >= from &&
                        DateOnly.FromDateTime(p.PaidAt!.Value) <= to)
            .ToList();

        var fieldsQuery = _uow.Fields.Query().AsQueryable();
        if (ownerId.HasValue) fieldsQuery = fieldsQuery.Where(f => f.OwnerId == ownerId.Value);

        // Doanh thu theo TUNG san: liet ke moi san trong pham vi (ke ca san 0 doanh thu de doi chieu),
        // xep giam dan theo doanh thu
        var fields = await fieldsQuery
            .Select(f => new { f.FieldId, f.FieldName })
            .ToListAsync();
        var byField = inRange
            .GroupBy(p => p.Booking.FieldId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(p => p.Amount)));
        var fieldRevenues = fields
            .Select(f => byField.TryGetValue(f.FieldId, out var v)
                ? new FieldRevenue(f.FieldId, f.FieldName, v.Count, v.Revenue)
                : new FieldRevenue(f.FieldId, f.FieldName, 0, 0))
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.FieldName)
            .ToList();

        var summary = new ReportSummary
        {
            TotalRevenue = inRange.Sum(p => p.Amount),
            TotalBookings = inRange.Select(p => p.BookingId).Distinct().Count(),
            TotalCustomers = await _uow.Users.Query().CountAsync(u => u.Role.RoleName == "Customer"),
            TotalFields = await fieldsQuery.CountAsync(),
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
                .ToList(),
            FieldRevenues = fieldRevenues
        };
        return summary;
    }
}
