# Sports Field Booking System (PRN222)

Hệ thống đặt sân thể thao — ASP.NET Core 8 MVC, 3-layer, SQL Server, chạy bằng Visual Studio 2022.

> Bản chỉnh sửa theo yêu cầu giảng viên — chi tiết tại `docs/YEU-CAU-CHINH-SUA.md`.

## Cấu trúc solution

```
SportsFieldBooking.sln
├── SportsFieldBooking.DataAccess   → Entities, DbContext, Repository + Unit of Work
├── SportsFieldBooking.Business     → Services + Design Patterns
└── SportsFieldBooking.Web          → MVC (Controllers, Views), cookie authentication
Database/SportsFieldBookingDB.sql   → Script tạo DB + seed data (DB-First)
```

## Cách chạy (Visual Studio 2022)

1. **Tạo database**: mở SSMS → chạy file `Database/SportsFieldBookingDB.sql` (schema mới — chạy lại sẽ **xóa và tạo lại** DB).
2. **Sửa connection string** trong `SportsFieldBooking.Web/appsettings.json` nếu cần:
   - Windows Authentication (mặc định): `Server=localhost;Database=SportsFieldBookingDB;Trusted_Connection=True;TrustServerCertificate=True`
   - SQL Authentication: `Server=localhost;Database=SportsFieldBookingDB;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True`
   - Nếu dùng SQL Express: đổi `Server=localhost` thành `Server=.\SQLEXPRESS`
3. Mở `SportsFieldBooking.sln` bằng VS2022 → đặt **SportsFieldBooking.Web** làm Startup Project → **F5**.

## Tài khoản demo (mật khẩu: `123456`)

| Vai trò | Email | Ghi chú |
|---|---|---|
| Admin | admin@sfb.com | Duyệt bảo trì, quản lý user/ví/điểm, ngày vàng hệ thống |
| Owner (chủ sân) | owner@sfb.com | Quản lý sân, bảng giá, khuyến mãi, request bảo trì |
| Staff | staff@sfb.com | Quản lý đặt lịch, đặt hộ khách |
| Customer | customer@sfb.com | Ví có sẵn 500.000đ để demo |
| **Super Account** | *(ẩn — không công bố)* | **Tài khoản cứu hộ** — không tồn tại trong DB lẫn `appsettings.json`; chỉ lưu SHA-256 hash một chiều trong `AccountController.IsSuperAccount()`. Đăng nhập tại `/Account/AdminLogin` để cấp/thu hồi Admin |

Mã giảm giá mẫu: `SUMMER26` (-20%, hệ thống), `NEWBIE10` (-10%, hệ thống), `OWNER15` (-15%, của chủ sân — gửi qua email).

## Chức năng chính (theo yêu cầu chỉnh sửa)

1. **Giá đa cấp** — `FieldPricingRules`: mỗi sân một bảng giá theo khung giờ + loại ngày (thường/cuối tuần) + khoảng tháng (mùa, hỗ trợ vắt năm 11→2); rule khớp có Priority cao nhất thắng, không có rule → giá cơ bản. Chủ sân CRUD tại trang **Giá & Khung giờ**.
2. **Ngày vàng** — `GoldenDays`: ngày lễ/sự kiện nhân hệ số giá + hệ số tích điểm; Admin tạo toàn hệ thống, Owner tạo riêng cho sân.
3. **Ví tiền ảo** — `Wallets` + `WalletTransactions`: nạp qua cổng thanh toán, thanh toán, hoàn tiền khi hủy/bảo trì, cashback (chủ sân cấu hình %), admin khóa/điều chỉnh có lý do. **Chủ sân bật/tắt nhận ví theo từng sân** (`AcceptWalletPayment`). Tiền ví không rút được. **Khuyến mãi nạp**: nạp từ 200k tặng 15k, từ 500k tặng 50k (giao dịch `Bonus` riêng, cấu hình trong `AppConfigSingleton.DepositBonusTiers`).
4. **Tích điểm + hạng thành viên** — `PointTransactions`: 10.000đ = 1 điểm khi booking hoàn thành (nhân hệ số ngày vàng), thưởng điểm khi review; **thưởng sự kiện**: +20 điểm hoàn thành booking đầu tiên, +50 điểm khi hoàn thành đủ 5 booking trong tháng (chống cộng trùng theo lịch sử điểm); dùng điểm trừ tiền khi thanh toán (1 điểm = 100đ, tối đa 50% booking). **Đổi điểm lấy voucher** tại trang Ví & Điểm (100đ→5%/tối đa 20k, 200đ→10%/50k, 500đ→15%/150k) — voucher là mã `Promotion` dùng 1 lần, **gửi về email** của khách. Hạng Thường/Bạc/Vàng/Kim cương theo điểm trọn đời → giảm 0/3/5/10% mọi booking.
5. **Super account** — trong `appsettings.json`, kiểm tra trước khi query DB, role `SuperAdmin`, chỉ vào trang Người dùng để cấp/thu hồi Admin (cứu hộ).
6. **Role nhiều luồng** — Customer / Owner / Staff / Admin / SuperAdmin, mỗi role menu + trang chủ riêng.
7. **Địa chỉ chi tiết + API** — Fields tách `Province`/`Ward`/`Address`; form thêm/sửa sân đổ dropdown từ `provinces.open-api.vn` (v2, 2 cấp sau sáp nhập); bộ lọc tìm sân theo Tỉnh/Thành.
8. **Bảo trì** — Owner gửi `MaintenanceRequest`, Admin duyệt → sân khóa đặt trong khoảng bảo trì, booking trùng lịch **tự hủy + hoàn tiền (về ví nếu trả bằng ví) + email thông báo** (không hỏi ý kiến — tránh booking treo).
9. **Bỏ BookingDetail** — 1 booking = 1 sân + 1 khung giờ + 1 ngày; chọn nhiều khung giờ tạo nhiều booking.
10. **Mọi role đặt được sân** — Staff/Admin/Owner đặt cho mình như khách, và **đặt hộ khách** (walk-in/điện thoại) tại Quản lý đặt lịch → Đặt hộ khách, kèm tùy chọn thu tiền mặt ngay; booking ghi `CreatedById` để truy vết.
11. **Khuyến mãi gửi email** — `Promotions.OwnerId`: mã của chủ sân chỉ áp dụng cho sân của họ, **gửi qua email** cho khách từng đặt sân đó (Admin gửi mã hệ thống cho mọi khách). SMTP cấu hình trong `appsettings.json` (`Smtp:*`); để trống → in email ra console (demo).

**Thứ tự áp giảm giá:** giá theo rule/ngày vàng → mã khuyến mãi → giảm theo hạng → điểm quy đổi → trả phần còn lại bằng ví/VNPay/Momo/Cash.

## Cổng thanh toán (nạp ví + thanh toán booking)

Nạp tiền vào ví và thanh toán online (VNPay/MoMo) đều đi qua **cổng thanh toán** như thật: redirect sang gateway → người dùng xác nhận → gateway trả kết quả → hệ thống mới ghi nhận tiền.

- **VNPay Sandbox thật:** đăng ký merchant miễn phí tại https://sandbox.vnpayment.vn/devreg/ rồi điền `VnPay:TmnCode` + `VnPay:HashSecret` vào `appsettings.json` → mọi giao dịch VNPay redirect sang `sandbox.vnpayment.vn` (chuẩn vnp_ v2.1.0, ký HMAC-SHA512, xác thực chữ ký ở `PaymentGateway/VnPayReturn`). Thẻ test: NCB `9704198526191432198`, tên `NGUYEN VAN A`, ngày `07/15`, OTP `123456`.
- **Cổng demo nội bộ:** chưa cấu hình VNPay → hệ thống hiện trang cổng mô phỏng (`PaymentGateway/Demo`) có thông tin đơn hàng, QR, đếm ngược 15 phút, nút xác nhận/hủy — luồng redirect y như thật để demo không cần mạng/credentials.
- Số tiền thanh toán booking luôn tính lại trên server (`PaymentService.PayAsync`), không tin giá trị từ client; giao dịch VNPay thật được xác thực chữ ký + số tiền trước khi ghi nhận.

## Design Patterns

| Pattern | Vị trí | Vai trò |
|---|---|---|
| Repository + Unit of Work | `DataAccess/Repositories` | Trừu tượng hóa truy cập dữ liệu, gom transaction |
| Strategy | `Business/Patterns/PricingStrategy.cs` | Chọn chiến lược giá: rule bảng giá vs giá cơ bản; nhân hệ số ngày vàng |
| Factory | `Business/Patterns/NotificationFactory.cs` | Tạo thông báo Email/SMS (demo) — email thật qua `EmailService` (SMTP) |
| Singleton | `Business/Patterns/AppConfigSingleton.cs` | Cấu hình chung: giờ hủy tối thiểu, tỷ lệ tiền↔điểm, trần đổi điểm, hạng thành viên |

## Chống trùng lịch (concurrency)

Hai lớp bảo vệ khi 2 người đặt cùng sân/khung giờ/ngày đồng thời:

1. `BookingService.CreateBookingAsync` kiểm tra trùng trong **transaction** trước khi ghi.
2. Unique filtered index `UX_Bookings_NoOverlap (FieldId, TimeSlotId, BookingDate) WHERE Status <> 'Cancelled'` ở DB — nếu 2 request lọt qua bước 1 cùng lúc, request sau bị `DbUpdateException` và được trả thông báo "khung giờ vừa được người khác đặt".

Khi hủy booking, trạng thái chuyển `Cancelled` nên khung giờ tự mở lại cho người khác.

## Đối soát ví & điểm

Mọi biến động số dư ví / điểm đều đi qua bảng giao dịch (`WalletTransactions` có `BalanceAfter`, `PointTransactions`) — không bao giờ update trực tiếp không log, phục vụ đối soát và khiếu nại.
