# Yêu cầu chỉnh sửa hệ thống Sports Field Booking

> Tổng hợp yêu cầu của giảng viên + chi tiết hóa các tính năng ví tiền ảo và tích điểm.
> Cập nhật: 27/07/2026

---

## 1. Ngày vàng

- Ngày đặc biệt (lễ, Tết, sự kiện) có ưu đãi hoặc mức giá riêng.
- Do hệ thống (Admin) hoặc chủ sân cấu hình theo ngày cụ thể.
- Liên kết với hệ thống giá (mục 4–5) và tích điểm (nhân điểm khi đặt vào ngày vàng, mục 6).

## 2. Hệ thống giá nhiều cấp

Thay 2 mức giá cố định hiện tại (`PricePerHour`, `PeakPricePerHour`) bằng bảng giá linh hoạt:

- **Theo khung giờ:** mỗi sân có khung giờ cao điểm riêng; chủ sân thêm/sửa/xóa khung giờ và giá tương ứng.
- **Theo ngày:** thứ trong tuần (ngày thường vs cuối tuần), ngày lễ / ngày vàng.
- **Theo tháng / theo mùa:** giá mùa cao điểm khác mùa thấp điểm.
- **Theo từng sân:** mỗi sân một bảng giá độc lập — cùng khung giờ nhưng sân A và sân B giá khác nhau.

Kỹ thuật: thêm entity dạng `FieldPricingRule` (FieldId, khung giờ, thứ, khoảng ngày/mùa, giá) + logic chọn rule khớp nhất khi tính tiền booking.

## 3. Super Account (tài khoản cứu hộ)

- Tài khoản đặc biệt **ẩn, không lưu trong database** — cấu hình trong `appsettings.json` (hoặc hardcode).
- Có **toàn quyền** hệ thống.
- Mục đích chính: **cấp / thu hồi quyền Admin** khi tài khoản Admin bị mất hoặc bị khóa — tài khoản cứu hộ cuối cùng.
- Đăng nhập bằng luồng kiểm tra riêng (đối chiếu config trước khi query DB).

## 4. Ví tiền ảo

### Nguyên tắc

- Mỗi user có 1 ví (`Wallet.Balance`) + bảng `WalletTransaction` ghi mọi biến động (không bao giờ sửa Balance mà không có log).
- **Chủ sân bật/tắt chấp nhận tiền ảo cho sân của mình** (cờ `AcceptWalletPayment` trên Field): bật → trang thanh toán hiện lựa chọn "Thanh toán bằng ví"; tắt → ẩn hoàn toàn.
- Tiền trong ví **không rút ra được** — chỉ dùng đặt sân (ghi rõ trong báo cáo để tránh nghiệp vụ chuyển tiền thật).
- Sân tắt tiền ảo thì booking cũ đã trả bằng ví vẫn được hoàn về ví khi hủy — cờ chỉ chặn thanh toán mới.

### Tính năng

| # | Tính năng | Loại giao dịch | Phạm vi |
|---|---|---|---|
| 1 | Nạp tiền vào ví (giả lập cổng thanh toán hoặc admin xác nhận) | `Deposit` | Tối thiểu |
| 2 | Thanh toán booking bằng ví (kiểm tra số dư, trừ ngay) | `Payment` | Tối thiểu |
| 3 | Hoàn tiền vào ví khi hủy booking hợp lệ / sân bảo trì | `Refund` | Tối thiểu |
| 4 | Cashback: chủ sân cấu hình hoàn X% khi trả bằng ví | `Cashback` | Nâng cao |
| 5 | Khuyến mãi nạp: "nạp 500k tặng 50k" | `Bonus` | Nâng cao |
| 6 | Lịch sử giao dịch ví (thời gian, số tiền, booking, số dư sau GD) | — | Tối thiểu |
| 7 | Admin quản trị ví: xem mọi ví, cộng/trừ thủ công có lý do, khóa ví | `Adjust` | Nâng cao |

## 5. Tích điểm & hạng thành viên

### Nguyên tắc

- Điểm **khác** tiền ảo: tiền ảo là tiền nạp vào để tiêu; điểm là thưởng theo hoạt động.
- Điểm là tính năng **toàn hệ thống** (không bật/tắt theo sân — hạng thành viên áp dụng chung mọi sân).
- Mọi biến động điểm đi qua bảng `PointTransaction`.

### Tích điểm

1. **Đặt sân hoàn tất** → cộng điểm theo tỷ lệ tiền (VD 10.000đ = 1 điểm). Chỉ cộng khi booking hoàn thành; hủy sau khi cộng thì thu hồi (`Revoke`).
2. **Sự kiện:** nhân đôi điểm ngày vàng; thưởng lần đặt đầu; đặt đủ N lần/tháng.
3. **Review sân** sau khi đá xong → cộng điểm nhỏ (5–10 điểm).

### Dùng điểm

4. **Đổi điểm trừ tiền** khi thanh toán (VD 100 điểm = 10.000đ, có mức trần % booking) — giao dịch `Redeem`.
5. **Đổi điểm lấy voucher** — voucher gửi về email (nối với mục 11).

### Hạng thành viên

| Hạng | Điều kiện (điểm tích lũy trọn đời) | Quyền lợi |
|---|---|---|
| Thường | mặc định | — |
| Bạc | ≥ 500 | giảm 3% mọi booking |
| Vàng | ≥ 2.000 | giảm 5%, ưu tiên đặt ngày vàng |
| Kim cương | ≥ 5.000 | giảm 10%, cashback cao hơn, hủy muộn không mất phí |

- Hạng tính theo tổng điểm **đã tích lũy trọn đời** — tiêu điểm chỉ giảm số dư, không tụt hạng.
- Huy hiệu hạng hiển thị ở profile và trang admin.
- Admin cấu hình tham số: tỷ lệ tiền↔điểm, ngưỡng hạng, % giảm; cộng/trừ điểm thủ công có lý do.

### Thứ tự áp dụng giảm giá (phải chốt thống nhất)

```
Giá theo rule (giờ/ngày/mùa)
  → trừ mã khuyến mãi
  → trừ giảm giá theo hạng
  → trừ điểm quy đổi
  → phần còn lại trả bằng ví / tiền mặt
```

## 6. Role có nhiều luồng nghiệp vụ

Mỗi role có luồng riêng, phân quyền rõ ràng:

- **Customer:** tìm sân → đặt → thanh toán (ví/điểm/khuyến mãi) → đánh giá.
- **Owner:** quản lý sân, bảng giá + khung giờ, bật/tắt tiền ảo, tạo khuyến mãi gửi mail, request bảo trì, xem báo cáo sân mình.
- **Staff:** duyệt/quản lý booking, đặt sân hộ khách (mục 9).
- **Admin:** quản lý user/role, duyệt bảo trì, quản trị ví & điểm, cấu hình ngày vàng, báo cáo toàn hệ thống.
- **Super Account:** cứu hộ — cấp/thu hồi Admin (mục 3).

## 7. Địa chỉ chi tiết + API hành chính

- Tách địa chỉ sân: **Tỉnh/Thành phố → Phường/Xã → đường, số nhà** (thay 3 field text tự do `Address`, `District`, `City`).
- Dropdown chọn địa chỉ đổ dữ liệu từ **API hành chính Việt Nam** (VD `provinces.open-api.vn`), không cho nhập tay.
- Dùng luôn cho bộ lọc tìm sân theo khu vực.

## 8. Chủ sân request bảo trì → Admin duyệt

- Owner gửi yêu cầu bảo trì (lý do + khoảng thời gian), **Admin duyệt/từ chối**.
- Duyệt → sân chuyển `Maintenance`, khóa đặt sân trong khoảng đó.
- Booking đã đặt trùng thời gian bảo trì: **tự động hủy + hoàn tiền ngay** (về ví nếu đã trả bằng ví; tiền mặt/chuyển khoản thì đánh dấu "chờ hoàn thủ công").
- **Không hỏi ý kiến chờ khách đồng ý** (sân bảo trì thì booking chắc chắn không thực hiện được; chờ phản hồi sẽ tạo booking treo). Thay vào đó **gửi email thông báo** ngay khi hủy: lý do bảo trì, xác nhận đã hoàn tiền, kèm lựa chọn bù đắp:
  - Link đổi lịch sang khung giờ/ngày khác (cùng sân hoặc sân khác cùng chủ), hoặc
  - Mã khuyến mãi đền bù cho lần đặt sau (dùng luồng gửi mail của mục 11) / cộng điểm đền bù.
- Nâng cao (tùy chọn): trong email cho khách chọn "giữ tiền trong ví + nhận thêm X% bonus" thay vì hoàn.

## 9. Bỏ bảng BookingDetail

- Xóa entity `BookingDetail`; đưa `FieldId`, `TimeSlotId`, `BookingDate`, `Price` thẳng vào `Booking` (quan hệ Booking – TimeSlot trực tiếp).
- 1 booking = 1 sân + 1 khung giờ + 1 ngày.
- Ảnh hưởng rộng nhất: DB, `BookingService`, `BookingController`, views Booking, Reports, Review.

## 10. Staff và Admin cũng đặt được sân

- Mọi role tạo được booking, không chỉ Customer.
- Nghiệp vụ chính: Staff/Admin **đặt hộ khách** walk-in / đặt qua điện thoại — chọn khách có sẵn hoặc nhập thông tin khách vãng lai.

## 11. Mã khuyến mãi gửi qua email

- **Chủ sân tự tạo mã** cho sân của mình (thêm `OwnerId`/`FieldId` vào `Promotion` — hiện tại promotion là toàn hệ thống).
- Mã tạo xong **gửi tự động qua email** cho khách (VD: khách từng đặt sân đó) — không phát offline.
- Cần tích hợp gửi mail (SMTP/SendGrid).
- Voucher đổi bằng điểm (mục 5) cũng đi qua kênh email này.

---

## Thứ tự triển khai đề xuất

1. **Nền tảng DB:** bỏ `BookingDetail` (mục 9) + bảng giá `FieldPricingRule` (mục 2) + tách địa chỉ (mục 7) — làm trước vì đụng schema.
2. **Nghiệp vụ giá:** khung giờ theo sân, ngày vàng, logic tính tiền (mục 1, 2).
3. **Role & booking:** luồng theo role, Staff/Admin đặt hộ (mục 6, 10), super account (mục 3).
4. **Ví & điểm:** wallet + transactions, tích điểm + hạng (mục 4, 5).
5. **Phụ trợ:** request bảo trì (mục 8), khuyến mãi gửi mail (mục 11), API địa chỉ cho UI (mục 7).
