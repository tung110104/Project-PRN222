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
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();

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
            e.Property(x => x.BankAccountNumber).HasMaxLength(50);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.BankAccountHolder).HasMaxLength(150);
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
            e.Property(x => x.Ward).HasMaxLength(100);
            e.Property(x => x.Province).HasMaxLength(100);
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
            e.HasKey(x => x.PricingRuleId); // ten khoa khong theo convention <EntityName>Id
            e.Property(x => x.RuleName).HasMaxLength(100);
            e.Property(x => x.DayType).HasMaxLength(10);
            e.Property(x => x.Price).HasColumnType("decimal(12,0)");
            e.HasOne(x => x.Field).WithMany(f => f.PricingRules).HasForeignKey(x => x.FieldId);
        });

        modelBuilder.Entity<GoldenDay>(e =>
        {
            e.ToTable("GoldenDays");
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Field).WithMany(f => f.GoldenDays).HasForeignKey(x => x.FieldId);
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
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings");
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.UnitPrice).HasColumnType("decimal(12,0)");
            e.Property(x => x.DiscountAmount).HasColumnType("decimal(12,0)");
            e.Property(x => x.TotalAmount).HasColumnType("decimal(12,0)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.User).WithMany(u => u.Bookings).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Promotion).WithMany(p => p.Bookings).HasForeignKey(x => x.PromotionId);
            e.HasOne(x => x.Field).WithMany(f => f.Bookings).HasForeignKey(x => x.FieldId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TimeSlot).WithMany(t => t.Bookings).HasForeignKey(x => x.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
            // Chong trung lich: 1 san + 1 khung gio + 1 ngay chi co 1 booking chua bi huy
            e.HasIndex(x => new { x.FieldId, x.TimeSlotId, x.BookingDate })
                .IsUnique()
                .HasFilter("[Status] <> 'Cancelled'");
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
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Amount).HasColumnType("decimal(12,0)");
            e.Property(x => x.BalanceAfter).HasColumnType("decimal(12,0)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Wallet).WithMany(w => w.Transactions).HasForeignKey(x => x.WalletId);
        });

        modelBuilder.Entity<PointTransaction>(e =>
        {
            e.ToTable("PointTransactions");
            e.Property(x => x.Type).HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.User).WithMany(u => u.PointTransactions).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<MaintenanceRequest>(e =>
        {
            e.ToTable("MaintenanceRequests");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.AdminNote).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasOne(x => x.Field).WithMany(f => f.MaintenanceRequests).HasForeignKey(x => x.FieldId);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RefundRequest>(e =>
        {
            e.ToTable("RefundRequests");
            e.Property(x => x.Amount).HasColumnType("decimal(12,0)");
            e.Property(x => x.Reason).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.BankAccountNumber).HasMaxLength(50);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.BankAccountHolder).HasMaxLength(150);
            e.Property(x => x.ProcessedNote).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppNotification>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.NotificationId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(500);
            e.Property(x => x.Url).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetOtp>(e =>
        {
            e.ToTable("PasswordResetOtps");
            e.Property(x => x.OtpCode).HasMaxLength(10);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            e.HasIndex(x => new { x.UserId, x.IsUsed });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
