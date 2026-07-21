using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SportsFieldBooking.Business.Services;
using SportsFieldBooking.DataAccess;
using SportsFieldBooking.DataAccess.Entities;
using SportsFieldBooking.DataAccess.Repositories;
using SportsFieldBooking.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddDbContext<SportsFieldBookingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository + Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Business services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailSender, GmailEmailSender>();
builder.Services.AddScoped<IFieldService, FieldService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserService, UserService>();

// Custom cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // Trang chi danh cho Admin thi chuyen den trang login admin
        options.Events.OnRedirectToLogin = context =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/Users") ||
                path.StartsWithSegments("/Promotions"))
            {
                var returnUrl = Uri.EscapeDataString(
                    path + context.Request.QueryString);
                context.Response.Redirect(
                    "/Account/AdminLogin?returnUrl=" + returnUrl);
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });

var app = builder.Build();

// Dong bo tai khoan Admin tu appsettings: luon ton tai va mat khau luon khop cau hinh
using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var adminEmail = config["AdminAccount:Email"];
    var adminPassword = config["AdminAccount:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) &&
        !string.IsNullOrWhiteSpace(adminPassword))
    {
        var db = scope.ServiceProvider
            .GetRequiredService<SportsFieldBookingDbContext>();
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();

        var adminRole = db.Roles.FirstOrDefault(r => r.RoleName == "Admin");
        if (adminRole == null)
        {
            adminRole = new Role { RoleName = "Admin" };
            db.Roles.Add(adminRole);
            db.SaveChanges();
        }

        var normalizedEmail = adminEmail.Trim().ToLowerInvariant();
        var admin = db.Users.FirstOrDefault(
            u => u.Email.ToLower() == normalizedEmail);

        if (admin == null)
        {
            db.Users.Add(new User
            {
                FullName = config["AdminAccount:FullName"] ?? "Administrator",
                Email = normalizedEmail,
                PasswordHash = auth.HashPassword(adminPassword),
                RoleId = adminRole.RoleId,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
        }
        else
        {
            admin.PasswordHash = auth.HashPassword(adminPassword);
            admin.RoleId = adminRole.RoleId;
            admin.IsActive = true;
        }

        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
