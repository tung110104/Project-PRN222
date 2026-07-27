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
    public DbSet<FieldPricingRule> FieldPricingRules => Set<FieldPricingRule>();
    public DbSet<GoldenDay> GoldenDays => Set<GoldenDay>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<MaintenanceRequest> MaintenanceRequests => Set<MaintenanceRequest>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

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
            e.Property(x => x.Street).HasMaxLength(200);
            e.Property(x => x.WardCode).HasMaxLength(20);
            e.Property(x => x.WardName).HasMaxLength(100);
            e.Property(x => x.ProvinceCode).HasMaxLength(20);
            e.Property(x => x.ProvinceName).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.PricePerHour).HasColumnType("decimal(12,0)");
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

        modelBuilder.Entity<FieldPricingRule>(e =>
        {
            e.ToTable("FieldPricingRules");
            e.HasKey(x => x.PricingRuleId);
            e.Property(x => x.RuleName).HasMaxLength(100);
            e.Property(x => x.Price).HasColumnType("decimal(12,0)");
            e.HasOne(x => x.Field).WithMany(f => f.PricingRules).HasForeignKey(x => x.FieldId);
        });

        modelBuilder.Entity<GoldenDay>(e =>
        {
            e.ToTable("GoldenDays");
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => new { x.Date, x.FieldId }).IsUnique();
            e.HasOne(x => x.Field).WithMany().HasForeignKey(x => x.FieldId);
        });

        modelBuilder.Entity<Promotion>(e =>
        {
            e.ToTable("Promotions");
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.MaxDiscount).HasColumnType("decimal(12,0)");
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Field).WithMany().HasForeignKey(x => x.FieldId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings");
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.GuestName).HasMaxLength(100);
            e.Property(x => x.GuestPhone).HasMaxLength(20);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(12,0)");
            e.Property(x => x.PromoDiscount).HasColumnType("decimal(12,0)");
            e.Property(x => x.TierDiscount).HasColumnType("decimal(12,0)");
            e.Property(x => x.PointsDiscount).HasColumnType("decimal(12,0)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(12,0)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");

            // Chống đặt trùng (mục 9 — thay index cũ trên BookingDetails):
            // 1 sân + 1 khung giờ + 1 ngày chỉ có 1 booking còn hiệu lực
            e.HasIndex(x => new { x.FieldId, x.TimeSlotId, x.BookingDate })
                .IsUnique()
                .HasFilter("[Status] IN ('Pending','Confirmed','Completed')")
                .HasDatabaseName("UX_Bookings_NoOverlap");

            e.HasOne(x => x.User).WithMany(u => u.Bookings).HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany(u => u.CreatedBookings).HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Field).WithMany(f => f.Bookings).HasForeignKey(x => x.FieldId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TimeSlot).WithMany(t => t.Bookings).HasForeignKey(x => x.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Promotion).WithMany(p => p.Bookings).HasForeignKey(x => x.PromotionId);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.Method).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.TransactionCode).HasMaxLength(50);
            e.Property(x => x.Amount).HasColumnType("decimal(12,0)");
            e.HasOne(x => x.Booking).WithMany(b => b.Payments).HasForeignKey(x => x.BookingId);

            // Chặn thanh toán trùng ở tầng DB: mỗi booking tối đa 1 payment đang hiệu lực
            e.HasIndex(x => x.BookingId)
                .IsUnique()
                .HasFilter("[Status] IN ('Pending','Paid')")
                .HasDatabaseName("UX_Payments_OneActivePerBooking");
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

        modelBuilder.Entity<Wallet>(e =>
        {
            e.ToTable("Wallets");
            e.Property(x => x.Balance).HasColumnType("decimal(12,0)");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(u => u.Wallet).HasForeignKey<Wallet>(x => x.UserId);
        });

        modelBuilder.Entity<WalletTransaction>(e =>
        {
            e.ToTable("WalletTransactions");
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.Amount).HasColumnType("decimal(12,0)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(12,0)");
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Wallet).WithMany(w => w.Transactions).HasForeignKey(x => x.WalletId);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PointTransaction>(e =>
        {
            e.ToTable("PointTransactions");
            e.Property(x => x.Type).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.User).WithMany(u => u.PointTransactions).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaintenanceRequest>(e =>
        {
            e.ToTable("MaintenanceRequests");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.AdminNote).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Field).WithMany(f => f.MaintenanceRequests).HasForeignKey(x => x.FieldId);
            e.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.ToTable("SystemSettings");
            e.HasKey(x => x.SettingKey);
            e.Property(x => x.SettingKey).HasMaxLength(50);
            e.Property(x => x.SettingValue).HasMaxLength(200);
        });
    }
}
