# HƯỚNG DẪN NÂNG CẤP V2 — Sports Field Booking

> Toàn bộ 11 mục yêu cầu của giảng viên đã được cài đặt (27/07/2026).
> **Code chưa được build kiểm chứng** — làm theo các bước dưới đây trước khi commit.

## 1. Các bước chạy lần đầu (BẮT BUỘC theo thứ tự)

1. **Chạy lại database**: mở SSMS → chạy `Database/SportsFieldBookingDB.sql`
   (script DROP toàn bộ DB cũ — dữ liệu cũ sẽ mất vì schema đổi tận gốc).
2. Mở solution trong Visual Studio → **Build ⇧+Ctrl+B** → sửa lỗi compile nếu có
   (đổi lớn, có thể sót vài tham chiếu nhỏ ở view).
3. F5 chạy thử theo kịch bản test ở mục 4.

## 2. Tài khoản demo (mật khẩu `123456` trừ khi ghi khác)

| Vai trò | Email | Ghi chú |
|---|---|---|
| Admin | admin@sportsbooking.com / `Admin@123` | đồng bộ từ appsettings, login tại `/Account/AdminLogin` |
| **Super Account** | super@sportsbooking.com / `Super@2026!` | KHÔNG có trong DB, login tại `/Account/AdminLogin`, vào trang Cứu hộ |
| Owner (chủ sân) | owner@sfb.com | role MỚI — sở hữu 4 sân demo |
| Staff (nhân viên) | staff@sfb.com | duyệt booking + đặt hộ, KHÔNG sở hữu sân |
| Customer | customer@sfb.com | có sẵn 200.000đ trong ví |
| Customer | hoa@gmail.com | |

Mã giảm giá: `SUMMER26`, `NEWBIE10` (toàn hệ thống), `VICTORY20` (riêng sân Victory — mã của chủ sân).

## 3. Tóm tắt thay đổi theo 11 mục

1. **Ngày vàng** — bảng `GoldenDays`; Admin tạo toàn hệ thống, Owner tạo theo sân; ưu đãi % trừ vào giá + nhân điểm khi hoàn thành. Trang: Ngày vàng.
2. **Giá nhiều cấp** — bỏ `PeakPricePerHour`; bảng `FieldPricingRules` (khung giờ / thứ / khoảng ngày-mùa, Priority); engine chọn rule khớp nhất trong `PricingStrategy.cs` (vẫn là Strategy Pattern). Trang: Quản lý sân → Bảng giá.
3. **Super Account** — cấu hình `SuperAccount` trong appsettings, không lưu DB, đối chiếu trước khi query; trang `/SuperAdmin` cấp/thu hồi Admin.
4. **Ví tiền ảo** — bảng `Wallets` + `WalletTransactions` (mọi biến động có log); cờ `AcceptWalletPayment` + `CashbackPercent` từng sân; nạp (giả lập, bonus nạp ≥500k), trả bằng ví = trừ ngay + tự Confirmed + cashback; hoàn ví ngay khi hủy; Admin điều chỉnh/khóa ví tại Quản trị ví. Tiền ví KHÔNG rút được.
5. **Điểm & hạng** — bảng `PointTransactions`; tích điểm khi Completed (10.000đ = 1đ, nhân theo ngày vàng, thưởng lần đầu, thưởng review); hạng theo `LifetimePoints` (Bạc/Vàng/Kim cương giảm 3/5/10%); đổi điểm trừ tiền lúc đặt (trần 50%); đổi voucher gửi email. Tham số chỉnh tại Cấu hình hệ thống.
   **Thứ tự giảm: rule/ngày vàng → mã KM → hạng → điểm → còn lại trả ví/tiền.** (lưu trong các cột PromoDiscount/TierDiscount/PointsDiscount của Bookings)
6. **Role** — 4 role: Admin / **Owner** (chủ sân — tách mới) / Staff (duyệt + đặt hộ, không sở hữu sân) / Customer. Mọi `[Authorize]` đã cập nhật.
7. **Địa chỉ** — Fields đổi sang `Street, WardCode, WardName, ProvinceCode, ProvinceName`; form Create/Edit sân đổ dropdown từ `provinces.open-api.vn` (thử API v2 → fallback v1, file `_AddressApiScripts.cshtml`); bộ lọc tìm sân theo tỉnh/phường.
8. **Bảo trì** — bảng `MaintenanceRequests`; Owner gửi (lý do + khoảng ngày), Admin duyệt → sân Maintenance + **tự hủy booking trùng khoảng, hoàn ví ngay / đánh dấu RefundPending, cộng điểm đền bù, gửi email** (không chờ khách đồng ý). Trang: Bảo trì.
9. **Bỏ BookingDetail** — Booking chứa thẳng FieldId/TimeSlotId/BookingDate/giá; 1 booking = 1 khung giờ (form đặt đổi checkbox → radio); unique index chống trùng dời sang `UX_Bookings_NoOverlap` trên Bookings; thêm `UX_Payments_OneActivePerBooking` chặn thanh toán trùng ở tầng DB.
10. **Đặt hộ** — mọi role đặt được sân; Staff/Admin có trang "Đặt hộ khách" (chọn khách có sẵn hoặc khách vãng lai nhập tên/SĐT; cột `CreatedById`, `GuestName`, `GuestPhone`).
11. **Promotion theo chủ sân + email** — Promotions thêm `OwnerId`/`FieldId`; Owner tự tạo mã cho sân mình; nút **Gửi mail** gửi mã cho khách từng đặt sân liên quan qua Gmail SMTP (`GmailEmailSender.SendEmailAsync`).

## 4. Kịch bản test nhanh (15 phút)

1. Chạy SQL → F5 → login `customer@sfb.com`.
2. Tìm sân (lọc tỉnh TP.HCM) → vào sân Thống Nhất → đổi ngày sang **thứ 7** xem giá cuối tuần 350k, khung 17h xem giá 450k.
3. Đặt khung 17h + mã `SUMMER26` + quy đổi 100 điểm (chưa có điểm → bỏ trống) → trang thanh toán → chọn **Ví** (đủ 200k? nếu thiếu thì vào Ví & Điểm nạp thêm) → booking tự **Confirmed**, nhận cashback 5%.
4. Vào Ví & Điểm xem lịch sử giao dịch.
5. Login `owner@sfb.com` → Quản lý sân → Bảng giá → thêm rule; Khuyến mãi → tạo mã riêng → **Gửi mail**; Bảo trì → gửi yêu cầu.
6. Login admin → Bảo trì → **Duyệt** → kiểm tra booking dính lịch bị hủy + email + tiền hoàn về ví khách.
7. Login `staff@sfb.com` → Quản lý đặt lịch → **Đặt hộ khách** (thử khách vãng lai) → Confirm đơn Momo.
8. `/Account/AdminLogin` với `super@sportsbooking.com` / `Super@2026!` → thu hồi / cấp Admin thử.

## 5. Lưu ý cho từng thành viên (phân công cũ)

- **Người 1 (Auth/User):** thêm Super Account trong `AccountController.AdminLogin`, role Owner mới, `UserService` thêm Grant/RevokeAdmin, GetCustomers.
- **Người 2 (Sân):** Field đổi địa chỉ 5 cột + AcceptWalletPayment/CashbackPercent; thêm `FieldPricingRule` CRUD; `_AddressApiScripts`.
- **Người 3 (Booking/Promotion):** `BookingService` viết lại toàn bộ (đọc kỹ pipeline giảm giá + transaction); `PricingStrategy` thành rule engine; `PromotionService` thêm phạm vi + gửi mail.
- **Người 4 (Payment/Report):** `PaymentService` thêm kênh Wallet; `ReportService` lọc ngày trong SQL + ownerId + tổng ví; 2 service mới `WalletService`, `PointService` cần người nhận thêm.

## 6. Việc còn lại / rủi ro đã biết

- Chưa build được ở máy công cụ — **chắc chắn phải Build + sửa lỗi vặt trong VS trước khi commit.**
- API địa chỉ cần Internet khi mở form sân; nếu provinces.open-api.vn đổi cấu trúc thì sửa `_AddressApiScripts.cshtml`.
- Gửi email dùng Gmail trong appsettings (app password đang có sẵn) — thử với email thật của nhóm.
- `NotificationFactory` không còn được gọi (email thật thay thế) — giữ lại làm minh chứng Factory Pattern hoặc xóa tùy nhóm.
- Nên tạo nhánh riêng khi commit (`git checkout -b feature/v2-redesign`) vì thay đổi rất lớn.
