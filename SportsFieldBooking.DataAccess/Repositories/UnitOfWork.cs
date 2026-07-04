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
    IGenericRepository<Booking> Bookings { get; }
    IGenericRepository<BookingDetail> BookingDetails { get; }
    IGenericRepository<Payment> Payments { get; }
    IGenericRepository<Review> Reviews { get; }
    IGenericRepository<Promotion> Promotions { get; }

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
        Bookings = new GenericRepository<Booking>(context);
        BookingDetails = new GenericRepository<BookingDetail>(context);
        Payments = new GenericRepository<Payment>(context);
        Reviews = new GenericRepository<Review>(context);
        Promotions = new GenericRepository<Promotion>(context);
    }

    public IGenericRepository<User> Users { get; }
    public IGenericRepository<Role> Roles { get; }
    public IGenericRepository<Field> Fields { get; }
    public IGenericRepository<FieldType> FieldTypes { get; }
    public IGenericRepository<FieldImage> FieldImages { get; }
    public IGenericRepository<TimeSlot> TimeSlots { get; }
    public IGenericRepository<Booking> Bookings { get; }
    public IGenericRepository<BookingDetail> BookingDetails { get; }
    public IGenericRepository<Payment> Payments { get; }
    public IGenericRepository<Review> Reviews { get; }
    public IGenericRepository<Promotion> Promotions { get; }

    public SportsFieldBookingDbContext Context => _context;
    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    public Task<IDbContextTransaction> BeginTransactionAsync() => _context.Database.BeginTransactionAsync();
    public void Dispose() => _context.Dispose();
}
