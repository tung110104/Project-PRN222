# Sports Field Booking System (PRN222)

Hệ thống đặt sân thể thao — ASP.NET Core 8 MVC, 3-layer, SQL Server, chạy bằng Visual Studio 2022.

## Cấu trúc solution

```
SportsFieldBooking.sln
├── SportsFieldBooking.DataAccess   → Entities, DbContext, Repository + Unit of Work
├── SportsFieldBooking.Business     → Services + Design Patterns
└── SportsFieldBooking.Web          → MVC (Controllers, Views), cookie authentication
Database/SportsFieldBookingDB.sql   → Script tạo DB + seed data (DB-First)
```

## Cách chạy (Visual Studio 2022)

1. **Tạo database**: mở SSMS → chạy file `Database/SportsFieldBookingDB.sql` (tạo `SportsFieldBookingDB` + dữ liệu mẫu).
2. **Sửa connection string** trong `SportsFieldBooking.Web/appsettings.json` nếu cần:
   - Windows Authentication (mặc định): `Server=localhost;Database=SportsFieldBookingDB;Trusted_Connection=True;TrustServerCertificate=True`
   - SQL Authentication: `Server=localhost;Database=SportsFieldBookingDB;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True`
   - Nếu dùng SQL Express: đổi `Server=localhost` thành `Server=.\SQLEXPRESS`
3. Mở `SportsFieldBooking.sln` bằng VS2022 → đặt **SportsFieldBooking.Web** làm Startup Project → **F5**.

## Tài khoản demo (mật khẩu: `123456`)

| Vai trò | Email |
|---|---|
| Admin | admin@sfb.com |
| Staff (chủ sân) | staff@sfb.com |
| Customer | customer@sfb.com |

Mã giảm giá mẫu: `SUMMER26` (-20%), `NEWBIE10` (-10%).

## Chức năng theo vai trò

- **Customer**: tìm/lọc sân (từ khóa, loại sân, khu vực, giá, đánh giá), xem chi tiết + khung giờ trống theo ngày, đặt nhiều khung giờ, áp mã giảm giá, thanh toán mô phỏng (VNPay/Momo/Cash), xem lịch sử, hủy booking (trước giờ đá ≥ 2h, tự hoàn tiền), đánh giá sân sau khi dùng.
- **Staff**: quản lý sân của mình (CRUD, tự sinh khung giờ 06:00–22:00), xác nhận/hủy booking của sân mình, xem báo cáo.
- **Admin**: tất cả quyền Staff trên mọi sân + quản lý người dùng (khóa/mở, đổi vai trò), quản lý khuyến mãi, báo cáo doanh thu (theo ngày, top 5 sân).

## Design Patterns

| Pattern | Vị trí | Vai trò |
|---|---|---|
| Repository + Unit of Work | `DataAccess/Repositories` | Trừu tượng hóa truy cập dữ liệu, gom transaction |
| Strategy | `Business/Patterns/PricingStrategy.cs` | Tính giá giờ thường vs cao điểm (17–21h) |
| Factory | `Business/Patterns/NotificationFactory.cs` | Tạo thông báo Email/SMS (demo ghi console) |
| Singleton | `Business/Patterns/AppConfigSingleton.cs` | Cấu hình chung (giờ hủy tối thiểu, số ngày đặt trước) |

## Chống trùng lịch (concurrency)

Hai lớp bảo vệ khi 2 người đặt cùng sân/khung giờ/ngày đồng thời:

1. `BookingService.CreateBookingAsync` kiểm tra trùng trong **transaction** trước khi ghi.
2. Unique filtered index `UX_BookingDetails_NoOverlap (FieldId, TimeSlotId, BookingDate) WHERE Status='Active'` ở DB — nếu 2 request lọt qua bước 1 cùng lúc, request sau bị `DbUpdateException` và được trả về thông báo "khung giờ vừa được người khác đặt".

Khi hủy booking, các `BookingDetail` chuyển sang `Cancelled` nên khung giờ tự mở lại cho người khác.
