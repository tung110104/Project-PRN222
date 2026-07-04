using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsFieldBooking.DataAccess.Entities;

namespace SportsFieldBooking.DataAccess;

public class SportsFieldBookingDbContext : DbContext
{
    public SportsFieldBookingDbContext() { }
    public SportsFieldBookingDbContext(DbContextOptions<SportsFieldBookingDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<FieldType> FieldTypes => Set<FieldType>();
    public DbSet<Field> Fields => Set<Field>();
    public DbSet<FieldImage> FieldImages => Set<FieldImage>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingDetail> BookingDetails => Set<BookingDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();
            optionsBuilder.UseSqlServer(config.GetConnectionString("DefaultConnection")
                ?? "Server=localhost;Database=SportsFieldBookingDB;Trusted_Connection=True;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(x => x.RoleName).HasMaxLength(50);
            e.HasIndex(x => x.RoleName).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(100);
            e.Property(x => x.PasswordHash).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<FieldType>(e =>
        {
            e.ToTable("FieldTypes");
            e.Property(x => x.TypeName).HasMaxLength(50);
            e.HasIndex(x => x.TypeName).IsUnique();
        });

        modelBuilder.Entity<Field>(e =>
        {
            e.ToTable("Fields");
            e.Property(x => x.FieldName).HasMaxLength(100);
            e.Property(x => x.Address).HasMaxLength(200);
            e.Property(x => x.District).HasMaxLength(50);
            e.Property(x => x.City).HasMaxLength(50);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.PricePerHour).HasColumnType("decimal(12,0)");
            e.Property(x => x.PeakPricePerHour).HasColumnType("decimal(12,0)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.FieldType).WithMany(t => t.Fields).HasForeignKey(x => x.FieldTypeId);
            e.HasOne(x => x.Owner).WithMany(u => u.OwnedFields).HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FieldImage>(e =>
        {
            e.ToTable("FieldImages");
            e.HasKey(x => x.ImageId);
            e.Property(x => x.ImageUrl).HasMaxLength(300);
            e.HasOne(x => x.Field).WithMany(f => f.FieldImages).HasForeignKey(x => x.FieldId);
        });

        modelBuilder.Entity<TimeSlot>(e =>
        {
            e.ToTable("TimeSlots");
            e.HasIndex(x => new { x.FieldId, x.StartTime }).IsUnique();
            e.HasOne(x => x.Field).WithMany(f => f.TimeSlots).HasForeignKey(x => x.FieldId);
        });

        modelBuilder.Entity<Promotion>(e =>
        {
            e.ToTable("Promotions");
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.MaxDiscount).HasColumnType("decimal(12,0)");
            e.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings");
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.TotalAmount).HasColumnType("decimal(12,0)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.User).WithMany(u => u.Bookings).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Promotion).WithMany(p => p.Bookings).HasForeignKey(x => x.PromotionId);
        });

        modelBuilder.Entity<BookingDetail>(e =>
        {
            e.ToTable("BookingDetails");
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Price).HasColumnType("decimal(12,0)");
            e.HasIndex(x => new { x.FieldId, x.TimeSlotId, x.BookingDate })
                .IsUnique()
                .HasFilter("[Status] = 'Active'");
            e.HasOne(x => x.Booking).WithMany(b => b.BookingDetails).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Field).WithMany(f => f.BookingDetails).HasForeignKey(x => x.FieldId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TimeSlot).WithMany(t => t.BookingDetails).HasForeignKey(x => x.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.Method).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.TransactionCode).HasMaxLength(50);
            e.Property(x => x.Amount).HasColumnType("decimal(12,0)");
            e.HasOne(x => x.Booking).WithMany(b => b.Payments).HasForeignKey(x => x.BookingId);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("Reviews");
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.BookingId).IsUnique();
            e.HasOne(x => x.Field).WithMany(f => f.Reviews).HasForeignKey(x => x.FieldId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(u => u.Reviews).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Booking).WithOne(b => b.Review).HasForeignKey<Review>(x => x.BookingId);
        });
    }
}
