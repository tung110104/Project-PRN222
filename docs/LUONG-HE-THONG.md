# Tài liệu luồng hệ thống — Sports Field Booking

> Mô tả toàn bộ luồng nghiệp vụ và luồng làm việc của từng vai trò.
> Cập nhật: 29/07/2026 — sau khi gộp role Staff vào Owner.

---

## 1. Kiến trúc tổng quan

```
Trình duyệt ──► SportsFieldBooking.Web (ASP.NET Core 8 MVC, cookie auth)
                      │ gọi service
                SportsFieldBooking.Business (nghiệp vụ, tính giá, ví, điểm, email)
                      │ qua IUnitOfWork + Repository
                SportsFieldBooking.DataAccess (EF Core 8, DB-first)
                      │
                SQL Server — SportsFieldBookingDB
```

- **Đăng nhập:** cookie authentication (8 giờ). Mỗi request kiểm tra lại `IsActive` — tài khoản bị khóa là văng phiên ngay.
- **Cổng thanh toán:** VNPay sandbox thật (nếu cấu hình `VnPay:TmnCode`) hoặc cổng demo nội bộ.
- **Email:** SMTP thật (nếu cấu hình `Smtp:Host`) hoặc in ra console (demo).

## 2. Các vai trò và quyền

| Vai trò | Đăng nhập tại | Quyền chính |
|---|---|---|
| **Khách vãng lai** | — | Tìm sân, xem chi tiết sân + bảng giá, xem đánh giá. Muốn đặt phải đăng nhập |
| **Customer** | `/Account/Login` | Đặt sân, thanh toán (ví/VNPay/MoMo/tiền mặt), nạp ví, tích điểm, đổi voucher, hủy booking, đánh giá sân |
| **Owner** (chủ sân — kiêm người trực quầy, đã gộp role Staff) | `/Account/Login` | Toàn bộ quyền Customer + quản lý sân của mình: bảng giá, khung giờ, ngày vàng riêng, khuyến mãi + gửi email, request bảo trì, duyệt/hủy booking sân mình, đặt hộ khách, báo cáo sân mình |
| **Admin** | `/Account/AdminLogin` | Toàn quyền nghiệp vụ: quản lý user/role, khóa tài khoản, quản lý ví (khóa ví, cộng/trừ), cộng/trừ điểm, ngày vàng hệ thống, duyệt bảo trì, khuyến mãi hệ thống, báo cáo toàn hệ thống |
| **Super Account** | `/Account/AdminLogin` | **Chỉ cứu hộ:** xem danh sách user, cấp/thu hồi Admin, khóa/mở khóa tài khoản. KHÔNG đặt sân, không ví, không nghiệp vụ. Không tồn tại trong DB lẫn appsettings — chỉ là SHA-256 hash trong code |

## 3. Luồng của từng vai trò

### 3.1. Khách vãng lai (chưa đăng nhập)

```
Trang chủ → lọc theo loại sân / Tỉnh-Thành / giá tối đa / số sao
→ Xem chi tiết sân: ảnh, mô tả, đánh giá, bảng giá THEO TỪNG KHUNG GIỜ của ngày đang chọn
→ Bấm đặt sân → chuyển đến trang đăng nhập (returnUrl quay lại sân đang xem)
```

### 3.2. Customer

```
Đăng ký (/Account/Register) → Đăng nhập
→ Tìm sân → chọn ngày → chọn 1..n khung giờ (mỗi khung giờ = 1 booking riêng)
→ nhập mã khuyến mãi (nếu có) → Đặt sân
→ Trang thanh toán: chọn phương thức + số điểm muốn dùng
     ├─ Ví: trừ ngay, có cashback nếu sân cấu hình
     ├─ VNPay/MoMo: redirect sang cổng thanh toán → xác nhận → ghi nhận
     └─ Tiền mặt: trả tại sân, chủ sân xác nhận
→ Sau giờ đá, hệ thống tự chuyển booking Hoàn thành → cộng điểm
→ Đánh giá sân (1-5 sao + bình luận) → được thưởng điểm
→ Hủy booking (trước giờ đá ≥ 2 tiếng): đã trả bằng ví thì hoàn ví ngay
```

Trang **Ví & Điểm** của Customer:
- Nạp tiền (tối thiểu 10k) qua cổng thanh toán; nạp từ 200k tặng 15k, từ 500k tặng 50k.
- Xem lịch sử giao dịch ví (mọi biến động đều có dòng log + số dư sau giao dịch).
- Đổi điểm lấy voucher (100đ→5%, 200đ→10%, 500đ→15%) — mã gửi về email, dùng 1 lần.
- Xem hạng thành viên: Thường/Bạc(500)/Vàng(2000)/Kim cương(5000) theo điểm tích lũy trọn đời → giảm 0/3/5/10% mọi booking.

### 3.3. Owner (chủ sân)

Đăng nhập xong vào thẳng **Quản lý sân**. Owner chỉ thấy và thao tác được **sân của chính mình**.

```
Quản lý sân:      thêm/sửa/xóa sân (xóa sân có booking → tự chuyển Ngừng hoạt động)
                  bật/tắt nhận ví ảo + % cashback theo từng sân
                  địa chỉ chọn từ API hành chính (Tỉnh/Thành → Phường/Xã)

Giá & Khung giờ:  thêm/tắt khung giờ; bảng giá đa cấp theo khung giờ + loại ngày
                  (thường/cuối tuần) + khoảng tháng (mùa); rule ưu tiên cao nhất thắng
                  ngày vàng RIÊNG của sân (nhân hệ số giá + hệ số điểm)

Khuyến mãi:       tạo mã giảm % (giới hạn tiền + số lượng + hạn) cho sân mình
                  → gửi email tự động cho khách từng đặt sân mình

Bảo trì:          gửi yêu cầu (lý do + khoảng ngày) → chờ Admin duyệt

Quản lý đặt lịch: xem booking sân mình theo trạng thái; xác nhận / hủy
                  ĐẶT HỘ KHÁCH (walk-in / điện thoại): chọn khách + sân + khung giờ trống,
                  tick "đã trả tiền mặt" để ghi nhận thanh toán Cash luôn

Báo cáo:          doanh thu, lượt đặt, top sân — CHỈ tính sân của mình
```

### 3.4. Admin

Đăng nhập tại `/Account/AdminLogin`, vào thẳng **Báo cáo** (toàn hệ thống).

```
Người dùng:   khóa/mở khóa (khóa = đăng xuất ngay phiên đang mở), đổi role,
              cấp/thu hồi Admin, cộng/trừ ví + điểm thủ công (bắt buộc ghi lý do)
Quản lý ví:   xem mọi ví, khóa ví (ví khóa: không nạp, không thanh toán được)
Ngày vàng:    tạo/xóa ngày vàng TOÀN HỆ THỐNG (áp dụng mọi sân)
Bảo trì:      duyệt/từ chối yêu cầu của Owner
              duyệt → sân chuyển Maintenance, booking trong khoảng đó TỰ HỦY:
              hoàn ví (nếu trả ví) / đánh dấu chờ hoàn (nếu trả kiểu khác) + email báo khách
Khuyến mãi:   tạo mã toàn hệ thống, gửi email cho mọi khách
Báo cáo:      toàn hệ thống
+ Admin cũng đặt sân / đặt hộ / quản lý mọi sân như Owner
```

### 3.5. Super Account (cứu hộ)

```
/Account/AdminLogin → nhập email + mật khẩu bí mật (đối chiếu SHA-256 hash trong code,
KHÔNG query DB) → chỉ vào được trang Người dùng:
   - Cấp Admin / Thu hồi Admin  (cứu hộ khi mất tài khoản admin)
   - Khóa / Mở khóa tài khoản
Mọi trang khác (đặt sân, ví, quản lý...) → Không có quyền
```

## 4. Các luồng nghiệp vụ chính

### 4.1. Tính giá 1 booking (thứ tự áp dụng)

```
1. Giá gốc khung giờ = FieldPricingRule khớp nhất (giờ + loại ngày + tháng/mùa,
   Priority cao nhất thắng) — không có rule nào khớp → PricePerHour của sân
2. × hệ số ngày vàng (ngày vàng riêng của sân ưu tiên hơn ngày vàng hệ thống)
3. − mã khuyến mãi (giảm %, tối đa MaxDiscount)
4. − giảm giá theo hạng thành viên (% trên phần còn lại)
   ==> chốt TotalAmount khi TẠO booking
5. − điểm quy đổi (1 điểm = 100đ, tối đa 50% giá trị) — chọn ở BƯỚC THANH TOÁN
6. Phần còn lại trả bằng Ví / VNPay / MoMo / Tiền mặt
```

### 4.2. Thanh toán & cổng thanh toán

```
Trang Pay → chọn phương thức + điểm
├─ Ví:      kiểm tra ví không khóa + đủ số dư → trừ ví (ghi ledger) → booking Đã xác nhận
│           → cashback X% về ví nếu sân cấu hình
├─ VNPay:   đã cấu hình TmnCode → redirect cổng VNPay SANDBOX THẬT (ký HMAC-SHA512,
│           xác thực chữ ký + số tiền khi quay về /PaymentGateway/VnPayReturn)
│           chưa cấu hình → cổng demo nội bộ (trang giả lập QR + nút xác nhận/hủy)
├─ MoMo:    đi qua cổng demo (mô phỏng)
└─ Cash:    chờ chủ sân xác nhận khi khách trả tại quầy
Số tiền LUÔN tính lại phía server khi chốt — không tin dữ liệu từ client.
```

### 4.3. Nạp ví

```
Trang Ví & Điểm → nhập số tiền (≥10k) → qua cổng thanh toán (VNPay thật hoặc demo)
→ cổng báo thành công → cộng ví (giao dịch Deposit)
→ đạt mốc khuyến mãi → cộng thêm giao dịch Bonus (200k→+15k, 500k→+50k)
Tiền ví KHÔNG rút ra được — chỉ dùng đặt sân.
```

### 4.4. Tích điểm & hạng

```
Booking Hoàn thành + ĐÃ thanh toán → cộng điểm: 10.000đ = 1 điểm
  × hệ số điểm nếu đá vào ngày vàng
  + 20 điểm nếu là booking hoàn thành ĐẦU TIÊN của user
  + 50 điểm nếu hoàn thành đủ 5 booking trong 1 tháng (mỗi tháng thưởng 1 lần)
Review sân sau khi đá → +10 điểm
Hủy booking đã cộng điểm → thu hồi điểm (Revoke)
Điểm tiêu (đổi voucher / trừ tiền) chỉ giảm SỐ DƯ — hạng xét theo điểm TRỌN ĐỜI nên không tụt hạng
```

### 4.5. Hủy booking & hoàn tiền

```
Khách tự hủy: chỉ được hủy trước giờ đá ≥ 2 tiếng (Owner/Admin hủy hộ: không giới hạn)
Khi hủy:
  - trả lại lượt mã khuyến mãi (Quantity +1)
  - hoàn điểm đã dùng (Revoke)
  - đã trả bằng VÍ → hoàn tiền vào ví NGAY (giao dịch Refund) + payment chuyển Refunded
  - trả VNPay/MoMo/Cash → đánh dấu chờ hoàn thủ công
```

### 4.6. Bảo trì sân

```
Owner gửi yêu cầu (lý do + từ ngày + đến ngày)
→ Admin duyệt:
    - sân chuyển trạng thái Maintenance (ẩn khỏi đặt sân)
    - mọi booking trong khoảng bảo trì TỰ HỦY + hoàn tiền (như 4.5)
    - email tự động cho từng khách: lý do + xác nhận hoàn tiền
→ Admin từ chối: sân hoạt động bình thường, Owner thấy lý do từ chối
(Không hỏi ý kiến khách trước khi hủy — tránh booking treo, đúng yêu cầu đề bài)
```

### 4.7. Khuyến mãi qua email

```
Owner tạo mã cho sân mình / Admin tạo mã hệ thống
→ bấm "Gửi email": Owner gửi cho khách từng đặt sân của mình; Admin gửi mọi khách
→ Voucher đổi bằng điểm cũng đi kênh email này (mã Promotion dùng 1 lần, có hạn)
Mã nhập ở bước đặt sân; hệ thống kiểm tra hạn + số lượng + phạm vi sân khi áp dụng
```

### 4.8. Khóa tài khoản / khóa ví

```
Khóa TÀI KHOẢN (Admin/Super): không đăng nhập được ("Tài khoản đã bị khóa"),
  phiên đang mở bị đăng xuất ngay ở request kế tiếp
Khóa VÍ (Admin): tài khoản vẫn dùng bình thường nhưng không nạp/không trả bằng ví
```

## 5. Trạng thái booking & payment

```
Booking:  Pending (chờ) ──thanh toán/chủ sân xác nhận──► Confirmed (đã xác nhận)
          ──qua ngày đá (job tự chạy)──► Completed (hoàn thành → cộng điểm nếu đã TT)
          Pending/Confirmed ──khách (≥2h trước giờ)/chủ sân/bảo trì──► Cancelled

Payment:  Pending ──cổng xác nhận / ví trừ tiền / chủ sân nhận cash──► Paid
          Paid ──hủy booking trả bằng ví──► Refunded
```

## 6. Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | admin@sfb.com | 123456 (đăng nhập tại `/Account/AdminLogin`) |
| Chủ sân (có 4 sân) | owner@sfb.com | 123456 |
| Chủ sân 2 (chưa có sân) | staff@sfb.com | 123456 |
| Khách | customer@sfb.com | 123456 |
| Super Account | *(bí mật — hash trong code)* | *(bí mật)* |
