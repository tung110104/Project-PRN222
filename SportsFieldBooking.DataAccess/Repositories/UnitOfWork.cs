using Microsoft.EntityFrameworkCore.Storage;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.DataAccess.Repositories;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }
    IGenericRepository<Role> Roles { get; }
    IGenericRepository<Field> Fields { get; }
    IGenericRepository<FieldType> FieldTypes { get; }
    IGenericRepository<FieldImage> FieldImages { get; }
    IGenericRepository<TimeSlot> TimeSlots { get; }
    IGenericRepository<FieldPricingRule> FieldPricingRules { get; }
    IGenericRepository<GoldenDay> GoldenDays { get; }
    IGenericRepository<Booking> Bookings { get; }
    IGenericRepository<Payment> Payments { get; }
    IGenericRepository<Review> Reviews { get; }
    IGenericRepository<Promotion> Promotions { get; }
    IGenericRepository<Wallet> Wallets { get; }
    IGenericRepository<WalletTransaction> WalletTransactions { get; }
    IGenericRepository<PointTransaction> PointTransactions { get; }
    IGenericRepository<MaintenanceRequest> MaintenanceRequests { get; }

    SportsFieldBookingDbContext Context { get; }
    Task<int> SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SportsFieldBookingDbContext _context;

    public UnitOfWork(SportsFieldBookingDbContext context)
    {
        _context = context;
        Users = new GenericRepository<User>(context);
        Roles = new GenericRepository<Role>(context);
        Fields = new GenericRepository<Field>(context);
        FieldTypes = new GenericRepository<FieldType>(context);
        FieldImages = new GenericRepository<FieldImage>(context);
        TimeSlots = new GenericRepository<TimeSlot>(context);
        FieldPricingRules = new GenericRepository<FieldPricingRule>(context);
        GoldenDays = new GenericRepository<GoldenDay>(context);
        Bookings = new GenericRepository<Booking>(context);
        Payments = new GenericRepository<Payment>(context);
        Reviews = new GenericRepository<Review>(context);
        Promotions = new GenericRepository<Promotion>(context);
        Wallets = new GenericRepository<Wallet>(context);
        WalletTransactions = new GenericRepository<WalletTransaction>(context);
        PointTransactions = new GenericRepository<PointTransaction>(context);
        MaintenanceRequests = new GenericRepository<MaintenanceRequest>(context);
    }

    public IGenericRepository<User> Users { get; }
    public IGenericRepository<Role> Roles { get; }
    public IGenericRepository<Field> Fields { get; }
    public IGenericRepository<FieldType> FieldTypes { get; }
    public IGenericRepository<FieldImage> FieldImages { get; }
    public IGenericRepository<TimeSlot> TimeSlots { get; }
    public IGenericRepository<FieldPricingRule> FieldPricingRules { get; }
    public IGenericRepository<GoldenDay> GoldenDays { get; }
    public IGenericRepository<Booking> Bookings { get; }
    public IGenericRepository<Payment> Payments { get; }
    public IGenericRepository<Review> Reviews { get; }
    public IGenericRepository<Promotion> Promotions { get; }
    public IGenericRepository<Wallet> Wallets { get; }
    public IGenericRepository<WalletTransaction> WalletTransactions { get; }
    public IGenericRepository<PointTransaction> PointTransactions { get; }
    public IGenericRepository<MaintenanceRequest> MaintenanceRequests { get; }

    public SportsFieldBookingDbContext Context => _context;
    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    public Task<IDbContextTransaction> BeginTransactionAsync() => _context.Database.BeginTransactionAsync();
    public void Dispose() => _context.Dispose();
}
