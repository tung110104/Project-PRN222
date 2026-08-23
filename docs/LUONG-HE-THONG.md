# Tài liệu luồng hệ thống — Sports Field Booking

> Mô tả toàn bộ luồng nghiệp vụ và luồng làm việc của từng vai trò, **đối chiếu trực tiếp với code**.
> Cập nhật: 23/08/2026 — sau khi bổ sung SignalR real-time, hoàn tiền chuyển khoản, OTP quên mật khẩu, xác nhận tiền mặt, báo cáo theo từng sân.

---

## 1. Kiến trúc tổng quan

```
Trình duyệt ──► SportsFieldBooking.Web (ASP.NET Core 8 MVC, cookie auth, SignalR Hub)
                      │ gọi qua interface service
                SportsFieldBooking.Business (nghiệp vụ: giá, ví, điểm, hoàn tiền, email, thông báo)
                      │ qua IUnitOfWork + GenericRepository
                SportsFieldBooking.DataAccess (EF Core 8, DB-first)
                      │
                SQL Server — SportsFieldBookingDB
```

- **Đăng nhập:** cookie authentication, phiên 8 giờ. Mỗi request, `OnValidatePrincipal` kiểm tra lại `IsActive` trong DB — tài khoản bị khóa là văng phiên **ngay request kế tiếp** (riêng Super Account id=0 không nằm trong DB nên bỏ qua bước này).
- **Real-time:** SignalR Hub tại `/notificationHub`. Mỗi user khi kết nối được gom vào group `user_{userId}` — thông báo lưu DB (bảng `AppNotifications`) rồi đẩy lên chuông navbar mọi tab đang mở. Lỗi push không bao giờ làm hỏng nghiệp vụ chính (mọi nơi đều bọc try/catch).
- **Cổng thanh toán:** VNPay sandbox thật (nếu cấu hình `VnPay:TmnCode` + `HashSecret` — ký/xác thực HMAC-SHA512) hoặc cổng demo nội bộ khi chưa cấu hình.
- **Email:** SMTP thật (nếu cấu hình `Smtp:Host`) hoặc in ra console (demo). Lỗi gửi mail cũng không làm hỏng nghiệp vụ.
- **Design patterns:** Singleton (`AppConfigSingleton` — mọi hằng số nghiệp vụ), Strategy (`PricingStrategy` — tính giá), Factory (`NotificationFactory`), Repository + Unit of Work.

## 2. Các vai trò và quyền

| Vai trò | Đăng nhập tại | Quyền chính |
|---|---|---|
| **Khách vãng lai** | — | Tìm sân (kèm API JSON `/api/fields/*`), xem chi tiết sân + bảng giá theo ngày + đánh giá. Muốn đặt phải đăng nhập |
| **Customer** | `/Account/Login` | Đặt sân, thanh toán (Ví/VNPay/MoMo/Tiền mặt), nạp ví, tích điểm + hạng thành viên, đổi voucher, hủy booking, đánh giá sân, cập nhật hồ sơ + STK ngân hàng nhận hoàn tiền, quên mật khẩu qua OTP email |
| **Owner** (chủ sân — kiêm người trực quầy, role Staff đã gộp vào) | `/Account/Login` | Toàn bộ quyền Customer + quản lý sân của mình: ảnh sân, bảng giá đa cấp, khung giờ, ngày vàng riêng, khuyến mãi + gửi email, yêu cầu bảo trì, xác nhận/hủy booking sân mình, xác nhận thu tiền mặt, đặt hộ khách, xử lý hoàn tiền chuyển khoản của sân mình, báo cáo doanh thu sân mình |
| **Admin** | `/Account/AdminLogin` | Toàn quyền nghiệp vụ: quản lý user/role, khóa tài khoản, quản lý ví (khóa, cộng/trừ), cộng/trừ điểm, ngày vàng hệ thống, duyệt bảo trì, khuyến mãi hệ thống, xử lý mọi hoàn tiền, báo cáo toàn hệ thống. Tài khoản cấu hình trong `appsettings.json` (`AdminAccount:Email/Password`) — lần đăng nhập đầu tự tạo/gắn user Admin trong DB |
| **Super Account** | `/Account/AdminLogin` | **Chỉ cứu hộ:** xem danh sách user, cấp/thu hồi quyền Admin, khóa/mở khóa tài khoản. KHÔNG đặt sân, không ví, không nghiệp vụ. Không tồn tại trong DB lẫn appsettings — chỉ là 2 hằng SHA-256 hash trong `AccountController` |

Phân quyền theo controller (attribute `[Authorize(Roles = ...)]`):

| Trang / Controller | Customer | Owner | Admin | SuperAdmin |
|---|---|---|---|---|
| Home, Field (xem), ApiFields | ✅ (cả khách vãng lai) | ✅ | ✅ | ✅ |
| Booking, Wallet, Profile, PaymentGateway | ✅ | ✅ | ✅ | ❌ |
| FieldsManage, FieldPricing, StaffBookings, Promotions, Maintenance, Refunds, Reports | ❌ | ✅ (phạm vi sân mình) | ✅ (tất cả) | ❌ |
| GoldenDays (ngày vàng hệ thống) | ❌ | ❌ | ✅ | ❌ |
| Users | ❌ | ❌ | ✅ | ✅ (chỉ ToggleActive + SetAdmin) |
| Notifications (chuông) | ✅ | ✅ | ✅ | ✅ |

## 3. Luồng của từng vai trò

### 3.1. Khách vãng lai (chưa đăng nhập)

```
Trang chủ → lọc theo từ khóa / loại sân / Tỉnh-Thành / giá tối đa / số sao
  (tìm không tải lại trang qua GET /api/fields/search; gợi ý tên sân qua /api/fields/suggest)
→ Xem chi tiết sân: ảnh, mô tả, đánh giá, bảng giá THEO TỪNG KHUNG GIỜ của ngày đang chọn
  (giá đã áp rule bảng giá + hệ số ngày vàng của ngày đó; slot đã đặt bị khóa)
→ Bấm đặt sân → chuyển trang đăng nhập (returnUrl quay lại sân đang xem)
```

### 3.2. Customer

**Đăng ký / đăng nhập / quên mật khẩu**

```
Đăng ký (/Account/Register): họ tên + email (duy nhất) + mật khẩu ≥ 6 ký tự → role Customer
Đăng nhập (/Account/Login): sai thông tin / bị khóa / là Admin → chặn kèm thông báo riêng
Quên mật khẩu (chỉ Customer/Owner — Admin cứu hộ bằng Super Account):
  Nhập email → hệ thống LUÔN báo chung chung (không lộ email tồn tại hay không)
  → OTP 6 số gửi qua email, hiệu lực 10 phút, dùng 1 lần, sai quá 5 lần thì vô hiệu
  → Nhập OTP → đặt mật khẩu mới → email xác nhận đã đổi mật khẩu
```

**Đặt sân → thanh toán → hoàn thành**

```
Tìm sân → chọn ngày → chọn 1..n khung giờ (MỖI khung giờ = 1 booking riêng)
→ nhập mã khuyến mãi (nếu có) → Đặt sân
  (chủ sân nhận thông báo real-time "Đặt sân mới"; khách nhận email xác nhận đặt)
→ Trang thanh toán (/Booking/Pay): chọn phương thức + số điểm muốn dùng
     ├─ Ví:        chỉ khi sân bật nhận ví; trừ ngay; có cashback % nếu sân cấu hình
     ├─ VNPay/MoMo: redirect sang cổng (sandbox thật hoặc demo) → xác nhận → ghi nhận
     └─ Tiền mặt:  chỉ GHI NHẬN yêu cầu (Pending) — chủ sân bấm "Xác nhận đã thu tiền"
                   thì booking mới Confirmed (tiền mặt KHÔNG dùng được điểm)
→ Thanh toán xong: booking Confirmed, email biên lai, chủ sân được báo "đã thanh toán"
→ Qua ngày đá: hệ thống tự chuyển Completed (khi mở danh sách booking)
  → cộng điểm NẾU đã thanh toán + thông báo số điểm được cộng
→ Đánh giá sân (1-5 sao + bình luận, mỗi booking 1 lần, chỉ khi Completed) → +10 điểm
→ Hủy booking: chỉ được hủy trước giờ đá ≥ 2 tiếng → hoàn điểm, hoàn lượt mã KM,
  hoàn tiền theo quy tắc mục 4.5 + email; chủ sân được báo "khung giờ trống trở lại"
```

**Trang Ví & Điểm (`/Wallet`)**

- Nạp tiền (tối thiểu 10k) — luôn đi qua cổng thanh toán; nạp từ 200k tặng 15k, từ 500k tặng 50k (giao dịch `Bonus` riêng để đối soát).
- Mọi biến động ví/điểm đều có dòng lịch sử kèm số dư sau giao dịch (`WalletTransactions`, `PointTransactions`).
- Đổi điểm lấy voucher: 100đ→giảm 5% (tối đa 20k, hạn 30 ngày), 200đ→10% (tối đa 50k, 30 ngày), 500đ→15% (tối đa 150k, 60 ngày) — mã dùng 1 lần, **chỉ gửi qua email**.
- Hạng thành viên theo điểm TRỌN ĐỜI (`LifetimePoints` — tiêu điểm không tụt hạng): Thường 0 / Bạc 500 (giảm 3%) / Vàng 2000 (5%) / Kim cương 5000 (10%) — giảm tự động mọi booking.
- Tiền ví **không rút ra được**, chỉ dùng đặt sân.

**Hồ sơ cá nhân (`/Profile`)** — sửa họ tên/SĐT, đổi mật khẩu, cập nhật **tài khoản ngân hàng** (nhận hoàn tiền khi sân không nhận ví), thống kê booking/chi tiêu/hạng.

### 3.3. Owner (chủ sân)

Đăng nhập xong vào thẳng **Quản lý sân**. Owner chỉ thấy và thao tác được **sân của chính mình** (mọi action đều kiểm tra `OwnerId` phía server).

```
Quản lý sân (/FieldsManage):
   thêm/sửa sân; địa chỉ chọn Tỉnh/Thành → Phường/Xã từ API hành chính VN
   upload ảnh (jpg/jpeg/png/webp, ≤5MB/ảnh; ảnh đầu tự làm đại diện; đổi/xóa ảnh được)
   bật/tắt nhận ví ảo + cashback 0-50% theo từng sân
   xóa sân: có lịch sử booking → chỉ chuyển "Closed"; chưa có → xóa hẳn kèm file ảnh
   tạo sân mới → tự sinh khung giờ mặc định 06:00-22:00 (mỗi giờ 1 slot)

Giá & Khung giờ (/FieldPricing):
   thêm khung giờ (chặn chồng lấn); tắt/bật khung giờ (không xóa để giữ lịch sử)
   bảng giá đa cấp: khung giờ + loại ngày (All/Weekday/Weekend) + khoảng tháng (mùa,
   hỗ trợ vắt năm 11→2); rule Priority cao nhất thắng; không khớp → giá cơ bản của sân
   ngày vàng RIÊNG của sân: hệ số giá 1-5, hệ số điểm 1-10
   (không xóa được ngày vàng hệ thống của Admin)

Khuyến mãi (/Promotions):
   tạo mã giảm % (MaxDiscount + số lượng + hạn) — mã của Owner CHỈ áp dụng cho sân mình
   gửi email: chọn người nhận từ danh sách khách TỪNG ĐẶT sân mình (xếp theo số lần đặt)

Bảo trì (/Maintenance):
   gửi yêu cầu (lý do + khoảng ngày, không lùi quá khứ, mỗi sân 1 yêu cầu Pending)
   → mọi Admin nhận thông báo real-time → chờ duyệt; bị từ chối thì thấy lý do

Quản lý đặt lịch (/StaffBookings):
   xem booking sân mình theo trạng thái; Xác nhận booking; Hủy booking (không giới hạn 2h)
   XÁC NHẬN / TỪ CHỐI yêu cầu trả tiền mặt của khách
   ĐẶT HỘ KHÁCH (walk-in / điện thoại): chọn khách + sân + ngày → load khung giờ trống
   qua AJAX → đặt dưới tên khách, CreatedById ghi người thao tác để truy vết
   → tick "đã trả tiền mặt" để xác nhận thu tiền luôn

Hoàn tiền (/Refunds):
   danh sách yêu cầu hoàn chuyển khoản CỦA SÂN MÌNH → chuyển khoản thủ công
   → "Đã chuyển khoản" (kèm mã GD; bắt buộc khách đã có STK) hoặc "Từ chối" (bắt buộc lý do)

Báo cáo (/Reports): doanh thu (chỉ tính payment Paid), lượt đặt, doanh thu THEO TỪNG SÂN
   (kể cả sân 0đ để đối chiếu), top sân — CHỈ trong phạm vi sân của mình
```

**Thông báo real-time Owner nhận:** khách đặt sân mới, khách thanh toán, khách đăng ký trả tiền mặt, khách hủy booking, sân có đánh giá mới, yêu cầu bảo trì được duyệt/từ chối, có yêu cầu hoàn tiền chuyển khoản mới.

### 3.4. Admin

Đăng nhập tại `/Account/AdminLogin`, vào thẳng **Báo cáo** (toàn hệ thống). Có 3 cách xác thực tại trang này (thứ tự trong code): Super Account (đối chiếu hash trong code, không query DB) → tài khoản trong `appsettings.json` (tự tạo/gắn user Admin trong DB) → user role Admin trong DB.

```
Người dùng (/Users):   khóa/mở khóa (phiên đang mở bị đăng xuất ngay), đổi role tùy ý,
                       cộng/trừ VÍ + ĐIỂM thủ công (BẮT BUỘC ghi lý do; không trừ quá số dư;
                       người bị điều chỉnh nhận thông báo kèm lý do)
Quản lý ví (/Wallet/Manage): xem mọi ví, khóa/mở ví (ví khóa: không nạp, không trả bằng ví)
Ngày vàng (/GoldenDays):     tạo/xóa ngày vàng TOÀN HỆ THỐNG (FieldId=null, áp dụng mọi sân;
                             ngày vàng riêng của sân được ưu tiên hơn khi trùng ngày)
Bảo trì (/Maintenance):      duyệt → sân chuyển Maintenance (nếu hôm nay trong khoảng),
                             booking trùng lịch TỰ HỦY + hoàn tiền + email + thông báo
                             từ chối → kèm ghi chú lý do cho Owner
Khuyến mãi (/Promotions):    tạo mã toàn hệ thống; gửi email chọn người nhận từ MỌI khách active
Hoàn tiền (/Refunds):        xử lý MỌI yêu cầu hoàn chuyển khoản
Báo cáo (/Reports):          toàn hệ thống
+ Admin dùng được mọi chức năng của Owner trên MỌI sân (sửa sân, giá, đặt hộ, xác nhận cash...)
+ Admin cũng đặt sân / có ví / có hồ sơ như một người dùng thường
```

### 3.5. Super Account (cứu hộ)

```
/Account/AdminLogin → nhập email + mật khẩu → đối chiếu SHA-256 hash hard-code
(KHÔNG query DB, không thể bị khóa/xóa/sửa qua giao diện; đổi mật khẩu = sửa hằng số + build lại)
→ đăng nhập với claim id=0, role SuperAdmin → chỉ vào được trang Người dùng:
   - Cấp Admin / Thu hồi Admin (thu hồi → chuyển về Customer; cấp → tự mở khóa tài khoản)
   - Khóa / Mở khóa tài khoản
Mọi trang nghiệp vụ khác → Access Denied (không controller nào cấp quyền SuperAdmin)
```

## 4. Các luồng nghiệp vụ chính (đối chiếu code)

### 4.1. Tính giá 1 booking (`PricingEngine` + `BookingService.CreateBookingAsync`)

```
1. Giá gốc khung giờ = FieldPricingRule khớp nhất:
   khớp khi slot.StartTime ∈ [rule.Start, rule.End) + đúng loại ngày + đúng khoảng tháng
   thứ tự chọn: Priority cao nhất → có giới hạn tháng → DayType cụ thể (khác All)
   không rule nào khớp → PricePerHour của sân          (Strategy pattern)
2. × hệ số ngày vàng (ngày vàng RIÊNG của sân ưu tiên hơn hệ thống), làm tròn nghìn đồng
3. − mã khuyến mãi: giảm %, chặn trần MaxDiscount
   (mã hệ thống dùng mọi sân; mã của Owner chỉ dùng cho sân của Owner đó;
    kiểm tra IsActive + hạn + Quantity ≥ số khung giờ đặt; mỗi booking trừ 1 lượt)
4. − giảm giá hạng thành viên (% trên phần CÒN LẠI sau khuyến mãi)
   ==> chốt UnitPrice / DiscountAmount / TotalAmount khi TẠO booking
5. − điểm quy đổi (1 điểm = 100đ, tối đa 50% giá trị booking) — chọn ở BƯỚC THANH TOÁN
6. Phần còn lại trả bằng Ví / VNPay / MoMo / Tiền mặt
```

**Điều kiện tạo booking:** ngày không ở quá khứ, đặt trước tối đa 30 ngày, sân `Active`, ngày không nằm trong khoảng bảo trì **đã duyệt**, slot đang bật, đặt hôm nay thì slot phải chưa qua giờ. Chống trùng lịch 2 lớp: kiểm tra conflict trong transaction + unique filtered index trên `Bookings` (bắt `DbUpdateException`).

### 4.2. Thanh toán & cổng thanh toán (`PaymentService` + `PaymentGatewayController`)

```
Trang Pay → chọn phương thức + điểm (server kiểm tra trần điểm lần cuối khi chốt)
├─ Ví:    sân phải bật AcceptWalletPayment; ví không khóa + đủ số dư
│         → transaction: trừ điểm → trừ ví (ghi ledger) → Payment Paid → booking Confirmed
│         → cashback X% về ví nếu sân cấu hình (giao dịch Cashback riêng)
├─ VNPay: đã cấu hình → redirect cổng SANDBOX THẬT; TxnRef nhúng "BK-{bookingId}-{điểm}"
│         quay về /PaymentGateway/VnPayReturn → xác thực chữ ký HMAC-SHA512 → PayAsync
│         chưa cấu hình → cổng demo nội bộ (trang giả lập + nút xác nhận/hủy)
├─ MoMo:  luôn đi cổng demo (mô phỏng)
└─ Cash:  tạo Payment "Pending" (KHÔNG dùng điểm được) → chủ sân được báo real-time
          → chủ sân "Xác nhận đã thu tiền" → Paid + booking Confirmed (khách trả thẳng
          tại quầy chưa đăng ký trước thì tạo luôn bản ghi Paid)
          → hoặc "Từ chối" (khách không đến trả) → Payment Failed, khách được báo chọn cách khác
Số tiền LUÔN tính lại phía server khi chốt — không tin amount từ client.
Nạp ví qua VNPay: TxnRef "DP-{userId}-...", số tiền lấy từ vnp_Amount đã xác thực chữ ký.
```

### 4.3. Nạp ví (`WalletService.DepositAsync`)

```
Trang Ví & Điểm → nhập số tiền (≥10k) → LUÔN qua cổng thanh toán (VNPay thật hoặc demo)
→ cổng báo thành công → cộng ví (giao dịch Deposit) + email biên lai + thông báo chuông
→ đạt mốc khuyến mãi (lấy mốc CAO NHẤT thỏa mãn) → cộng thêm giao dịch Bonus:
  nạp ≥200k → +15k; nạp ≥500k → +50k
```

### 4.4. Tích điểm & hạng (`PointService`)

```
Booking Completed + ĐÃ thanh toán → cộng điểm: 10.000đ = 1 điểm
  × hệ số điểm ngày vàng (ưu tiên ngày vàng riêng của sân)
  + 20 điểm booking hoàn thành ĐẦU TIÊN (chỉ 1 lần, kiểm tra lịch sử chống cộng trùng)
  + 50 điểm khi hoàn thành đủ 5 booking trong 1 tháng dương lịch (mỗi tháng tối đa 1 lần)
  → khách nhận thông báo tổng điểm vừa cộng
Review sân → +10 điểm (loại ReviewBonus)
Hủy booking có dùng điểm → hoàn điểm (loại Revoke, KHÔNG cộng vào LifetimePoints)
Admin cộng/trừ thủ công → loại Adjust, bắt buộc lý do, không âm quá số dư
Hạng xét theo LifetimePoints (chỉ tăng) → tiêu điểm không tụt hạng
```

### 4.5. Hủy booking & hoàn tiền (`BookingService.CancelAsync` + `RefundService`)

```
Ai hủy được: khách tự hủy (trước giờ đá ≥ 2 tiếng) / Owner-Admin hủy (không giới hạn giờ)
Không hủy được booking đã Cancelled/Completed.
Khi hủy: trả lượt mã KM (Quantity+1) → hoàn điểm đã dùng → hoàn tiền → email + thông báo.

QUY TẮC HOÀN TIỀN (chỉ khi có Payment "Paid" — chưa thanh toán thì không có gì để hoàn):
  ├─ Đã trả bằng VÍ                     → hoàn NGAY vào ví (giao dịch Refund)
  ├─ Trả VNPay/MoMo/Cash + sân CÓ nhận ví → cũng hoàn NGAY vào ví (nhanh, dùng được luôn)
  └─ Trả VNPay/MoMo/Cash + sân KHÔNG nhận ví
        → tạo RefundRequest chuyển khoản, SNAPSHOT thông tin STK của khách tại thời điểm tạo
        → email báo khách (nhắc cập nhật STK trong Hồ sơ nếu chưa có)
        → thông báo real-time cho chủ sân + mọi Admin
        → Owner/Admin vào /Refunds chuyển khoản thủ công rồi bấm "Đã chuyển khoản"
          (chặn nếu khách chưa có STK) hoặc "Từ chối" kèm lý do → email + thông báo cho khách
Payment gốc chuyển trạng thái Refunded ngay khi bắt đầu hoàn.
```

### 4.6. Bảo trì sân (`MaintenanceService`)

```
Owner gửi yêu cầu (lý do + từ ngày + đến ngày; không lùi quá khứ; mỗi sân 1 yêu cầu Pending)
→ mọi Admin nhận thông báo real-time
→ Admin DUYỆT:
    - hôm nay nằm trong khoảng bảo trì → sân chuyển "Maintenance" ngay
    - mọi booking Pending/Confirmed trong khoảng đó TỰ HỦY: trả lượt KM + hoàn điểm
      + hoàn tiền theo quy tắc 4.5 + email từng khách + thông báo chuông từng khách
    - Owner được báo "đã duyệt, N booking đã hủy & hoàn tiền"
→ Admin TỪ CHỐI: kèm ghi chú → Owner được báo lý do
(Không hỏi ý kiến khách trước khi hủy — sân bảo trì thì booking chắc chắn không đá được)
Lưu ý: ngày nằm trong khoảng bảo trì ĐÃ DUYỆT cũng bị chặn ngay từ bước tạo booking.
```

### 4.7. Khuyến mãi & voucher (`PromotionService` + `PointService.RedeemVoucherAsync`)

```
Admin tạo mã hệ thống (OwnerId=null — mọi sân) / Owner tạo mã riêng (chỉ sân mình)
Mã duy nhất (unique), có %, MaxDiscount, khoảng ngày, số lượng, bật/tắt.
Gửi mã QUA EMAIL (không phát offline): mở màn hình chọn người nhận
  - mã Owner → khách từng đặt sân của Owner đó (xếp theo số lần đặt, kèm lần đặt gần nhất)
  - mã hệ thống → mọi Customer đang active
  - Owner chỉ gửi được mã của chính mình (server kiểm tra scope)
Voucher đổi điểm: sinh mã "VCxxxxxxxx" duy nhất, Quantity=1, áp dụng mọi sân, gửi email.
Khi đặt sân: kiểm tra hạn + lượt + phạm vi sân; hủy booking thì trả lại lượt.
```

### 4.8. Đánh giá sân (`ReviewService`)

```
Chỉ khi booking Completed + đúng chủ booking + chưa từng đánh giá booking đó
(server kiểm tra lại toàn bộ — không tin client)
→ lưu Review (1-5 sao + bình luận) → +10 điểm thưởng
→ chủ sân nhận thông báo real-time kèm số sao + trích bình luận
Điểm trung bình hiển thị ở trang chủ (lọc theo số sao) + chi tiết sân.
```

### 4.9. Thông báo real-time (`NotificationService` + SignalR)

```
NotifyAsync = LUU DB trước (xem lại được lịch sử) → đẩy SignalR tới group user_{id} sau
Chuông navbar: badge số chưa đọc, danh sách 15 thông báo mới nhất (fetch /Notifications/GetLatest),
bấm vào → đánh dấu đã đọc + mở trang liên quan (Url đính kèm); nút "đọc tất cả".

Sự kiện → người nhận:
  Đặt sân mới / thanh toán / yêu cầu trả cash / khách hủy / đánh giá mới   → Owner
  Booking được xác nhận / bị hủy / cash được xác nhận / cash bị từ chối    → Customer
  Tích điểm khi hoàn thành / cashback / nạp ví / điều chỉnh ví-điểm / khóa ví → Customer
  Hủy do bảo trì / đã chuyển khoản hoàn / từ chối hoàn                     → Customer
  Yêu cầu bảo trì mới / yêu cầu hoàn chuyển khoản mới                      → mọi Admin (+Owner)
  Kết quả duyệt bảo trì                                                    → Owner
```

### 4.10. Báo cáo doanh thu (`ReportService`)

```
Doanh thu = tổng Payment "Paid" theo NGÀY THANH TOÁN (PaidAt) trong khoảng lọc
(mặc định 30 ngày gần nhất; Owner tự động giới hạn sân mình, Admin toàn hệ thống)
Gồm: tổng doanh thu, tổng booking (distinct), tổng khách, tổng sân,
     doanh thu theo ngày (biểu đồ), top 5 sân theo lượt đặt,
     doanh thu THEO TỪNG SÂN — liệt kê cả sân 0đ để đối chiếu, xếp giảm dần
```

### 4.11. Khóa tài khoản / khóa ví

```
Khóa TÀI KHOẢN (Admin/Super): không đăng nhập được ("Tài khoản đã bị khóa");
  phiên đang mở bị OnValidatePrincipal đăng xuất ngay request kế tiếp
Khóa VÍ (Admin): tài khoản vẫn dùng bình thường nhưng không nạp / không trả bằng ví;
  khách được thông báo khi ví bị khóa/mở
```

## 5. Trạng thái

```
Booking:  Pending ──thanh toán thành công / chủ sân xác nhận──► Confirmed
          Confirmed ──qua ngày đá (tự chạy khi mở danh sách booking)──► Completed
                                                     (→ cộng điểm nếu đã thanh toán)
          Pending/Confirmed ──khách (≥2h)/chủ sân/Admin/bảo trì──► Cancelled

Payment:  Pending (chờ thu tiền mặt) ──chủ sân xác nhận──► Paid
          (Ví/VNPay/MoMo: tạo thẳng Paid)   Paid ──hủy booking──► Refunded
          Pending ──chủ sân từ chối cash──► Failed

RefundRequest:      Pending ──► Completed (đã chuyển khoản) / Rejected (kèm lý do)
MaintenanceRequest: Pending ──► Approved / Rejected
Field:              Active / Maintenance / Closed
```

## 6. Tham số cấu hình nghiệp vụ (`AppConfigSingleton` — Singleton pattern)

| Tham số | Giá trị | Ý nghĩa |
|---|---|---|
| CancelBeforeHours | 2 giờ | Khách chỉ hủy được trước giờ đá ít nhất chừng này |
| MaxAdvanceBookingDays | 30 ngày | Đặt trước tối đa |
| VndPerPoint | 10.000đ | Số tiền quy ra 1 điểm khi hoàn thành booking |
| PointValueVnd | 100đ | Giá trị 1 điểm khi trừ tiền |
| MaxRedeemPercent | 50% | Trần % giá trị booking trả được bằng điểm |
| ReviewBonusPoints | 10 | Điểm thưởng đánh giá |
| FirstBookingBonusPoints | 20 | Thưởng booking hoàn thành đầu tiên |
| MonthlyBookingTarget / Bonus | 5 booking / 50 điểm | Thưởng đủ 5 booking/tháng |
| MinDepositAmount | 10.000đ | Nạp ví tối thiểu |
| DepositBonusTiers | ≥200k→+15k; ≥500k→+50k | Khuyến mãi nạp (mức cao nhất thỏa mãn) |
| VoucherOptions | 100đ→5%/20k/30ng; 200đ→10%/50k/30ng; 500đ→15%/150k/60ng | Gói đổi voucher |
| MembershipTiers | Bạc 500→3%; Vàng 2000→5%; Kim cương 5000→10% | Hạng theo LifetimePoints |

## 7. Tài khoản demo (mật khẩu: `123456`)

| Vai trò | Email | Ghi chú |
|---|---|---|
| Admin | admin@sfb.com | Đăng nhập tại `/Account/AdminLogin`; cấu hình trong `appsettings.json` |
| Chủ sân (có sân) | owner@sfb.com | |
| Chủ sân 2 (chưa có sân) | staff@sfb.com | |
| Khách | customer@sfb.com | |
| Super Account | *(bí mật — hash trong code)* | *(bí mật)* |
