using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess;
using SportsFieldBooking.DataAccess.Repositories;
using SportsFieldBooking.Web.Hubs;
using SportsFieldBooking.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// SignalR: day thong bao real-time (chuong tren navbar - vd bao chu san khi co khach dat san)
builder.Services.AddSignalR();

builder.Services.AddDbContext<SportsFieldBookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository + Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Business services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFieldService, FieldService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IPointService, PointService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Business day thong bao real-time qua abstraction IRealtimeNotifier -> Web implement bang SignalR
builder.Services.AddScoped<IRealtimeNotifier, SignalRNotifier>();

// Cong thanh toan VNPay sandbox (chua cau hinh TmnCode -> tu fallback sang cong demo noi bo)
builder.Services.AddScoped<IVnPayService, VnPayService>();

// Custom cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // Tai khoan bi khoa thi phien dang nhap dang mo cung bi vo hieu ngay request ke tiep
        // (khong cho "dung not" den khi cookie het han)
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var idClaim = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                // Super account (id = 0) khong nam trong DB -> bo qua kiem tra
                if (!int.TryParse(idClaim, out var userId) || userId == 0) return;

                var uow = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                var stillActive = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(uow.Users.Query(), u => u.UserId == userId && u.IsActive);
                if (!stillActive)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// API JSON (ApiFieldsController dung attribute routing: /api/fields/...)
app.MapControllers();

// Hub SignalR cho thong bao real-time
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
