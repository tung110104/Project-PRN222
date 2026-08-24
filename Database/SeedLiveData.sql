-- =====================================================================
-- SEED DU LIEU "WEB SONG": 12 khach hang moi, ~180 booking (15/05 -> 31/08/2026),
-- thanh toan Wallet/VNPay/Cash, vi + nap tien + cashback + hoan tien,
-- tich diem (Earn/Redeem/Revoke/ReviewBonus), review, notification, refund request.
-- Toan bo tuan theo dung logic cua PointService / WalletService / PaymentService / RefundService.
-- Chay lai an toan: neu email seed da ton tai thi script dung ngay.
-- =====================================================================
SET NOCOUNT ON;
SET DATEFIRST 1; -- Thu 2 = 1 ... CN = 7 (weekend = 6,7)
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM Users WHERE Email = 'tuanngo.fc@gmail.com')
BEGIN
    RAISERROR(N'Du lieu seed da ton tai (tuanngo.fc@gmail.com). Khong chay lai.', 16, 1);
    RETURN;
END

BEGIN TRY
BEGIN TRAN;

DECLARE @now      datetime = '2026-08-24T11:30:00';
DECLARE @today    date     = '2026-08-24';
DECLARE @pwd      nvarchar(200) = '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92'; -- SHA256('123456')
DECLARE @adminId  int = (SELECT TOP 1 UserId FROM Users WHERE Email = 'admin@sfb.com');
DECLARE @staffId  int = (SELECT TOP 1 UserId FROM Users WHERE Email = 'staff@sfb.com');

-- ---------------------------------------------------------------
-- 1) 12 khach hang moi (RoleId = 3, mat khau 123456)
-- ---------------------------------------------------------------
CREATE TABLE #newusers (FullName nvarchar(100), Email nvarchar(100), Phone nvarchar(20), CreatedAt datetime);
INSERT #newusers VALUES
 (N'Ngô Minh Tuấn',       'tuanngo.fc@gmail.com',        '0912456781', '2026-02-08T09:15:00'),
 (N'Trần Đức Anh',        'ducanh.tran92@gmail.com',     '0987234561', '2026-02-19T14:40:00'),
 (N'Lê Hoàng Phong',      'phonglh.sport@gmail.com',     '0905671234', '2026-03-02T19:05:00'),
 (N'Phạm Thị Mai',        'maipham.badminton@gmail.com', '0934567812', '2026-03-11T08:22:00'),
 (N'Vũ Quang Huy',        'huyvu.tennis@gmail.com',      '0978123456', '2026-03-15T17:48:00'),
 (N'Đỗ Văn Nam',          'namdo.fc88@gmail.com',        '0918765432', '2026-03-27T21:10:00'),
 (N'Bùi Thị Thu Trang',   'trangbui.sport@gmail.com',    '0967345128', '2026-04-05T10:33:00'),
 (N'Hoàng Văn Sơn',       'sonhoang.basket@gmail.com',   '0942318765', '2026-04-12T16:55:00'),
 (N'Nguyễn Thị Kim Chi',  'kimchi.nguyen01@gmail.com',   '0923456187', '2026-04-18T11:27:00'),
 (N'Đặng Minh Khoa',      'khoadang.futsal@gmail.com',   '0956781234', '2026-04-24T20:14:00'),
 (N'Lý Thanh Bình',       'binhly.sport@gmail.com',      '0989123457', '2026-04-29T13:36:00'),
 (N'Cao Thị Hồng Nhung',  'nhungcao.tennis@gmail.com',   '0936912845', '2026-05-01T09:58:00');

INSERT Users (FullName, Email, PasswordHash, Phone, RoleId, IsActive, Points, LifetimePoints, CreatedAt)
SELECT FullName, Email, @pwd, Phone, 3, 1, 0, 0, CreatedAt FROM #newusers;

-- Vai khach co san thong tin ngan hang (dung cho hoan tien chuyen khoan)
UPDATE Users SET BankAccountNumber = '9704221812345671', BankName = N'Vietcombank',  BankAccountHolder = N'NGO MINH TUAN'  WHERE Email = 'tuanngo.fc@gmail.com';
UPDATE Users SET BankAccountNumber = '1903612345678',    BankName = N'Techcombank',  BankAccountHolder = N'TRAN DUC ANH'   WHERE Email = 'ducanh.tran92@gmail.com';
UPDATE Users SET BankAccountNumber = '0071000456789',    BankName = N'MB Bank',      BankAccountHolder = N'LE THI HOA'     WHERE Email = 'hoa@gmail.com';

-- ---------------------------------------------------------------
-- 2) Danh sach khach + trong so dat san (khach quen dat nhieu hon)
-- ---------------------------------------------------------------
CREATE TABLE #uw (id int IDENTITY(1,1) PRIMARY KEY, uid int NOT NULL);
-- khach "ruot" x4
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('tuanngo.fc@gmail.com','ducanh.tran92@gmail.com','phonglh.sport@gmail.com','customer@sfb.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('tuanngo.fc@gmail.com','ducanh.tran92@gmail.com','phonglh.sport@gmail.com','customer@sfb.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('tuanngo.fc@gmail.com','ducanh.tran92@gmail.com','phonglh.sport@gmail.com','customer@sfb.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('tuanngo.fc@gmail.com','ducanh.tran92@gmail.com','phonglh.sport@gmail.com','customer@sfb.com');
-- khach thuong xuyen x2
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('maipham.badminton@gmail.com','huyvu.tennis@gmail.com','namdo.fc88@gmail.com','hoa@gmail.com','khoadang.futsal@gmail.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('maipham.badminton@gmail.com','huyvu.tennis@gmail.com','namdo.fc88@gmail.com','hoa@gmail.com','khoadang.futsal@gmail.com');
-- khach vang lai x1
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('trangbui.sport@gmail.com','sonhoang.basket@gmail.com','kimchi.nguyen01@gmail.com','binhly.sport@gmail.com','nhungcao.tennis@gmail.com');

CREATE TABLE #cust (uid int PRIMARY KEY, uname nvarchar(100));
INSERT #cust SELECT DISTINCT us.UserId, us.FullName FROM #uw u JOIN Users us ON us.UserId = u.uid;

-- ---------------------------------------------------------------
-- 3) Vi cho tat ca khach (2 vi cu giu nguyen so du hien tai)
-- ---------------------------------------------------------------
INSERT Wallets (UserId, Balance, IsLocked, UpdatedAt)
SELECT c.uid, 0, 0, '2026-05-12T08:00:00' FROM #cust c WHERE NOT EXISTS (SELECT 1 FROM Wallets w WHERE w.UserId = c.uid);

CREATE TABLE #wal (uid int PRIMARY KEY, wid int, bal decimal(18,2));
INSERT #wal SELECT w.UserId, w.WalletId, w.Balance FROM Wallets w JOIN #cust c ON c.uid = w.UserId;

-- Nap tien lan dau cho cac vi moi (thang 5), kem thuong nap theo dung muc cua he thong
DECLARE @wuid int, @wwid int, @wbal decimal(18,2), @dep decimal(18,2), @dbonus decimal(18,2), @dtime datetime, @rr1 int;
DECLARE wdep CURSOR LOCAL FAST_FORWARD FOR SELECT uid, wid, bal FROM #wal WHERE bal = 0 ORDER BY uid;
OPEN wdep;
FETCH NEXT FROM wdep INTO @wuid, @wwid, @wbal;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @rr1 = ABS(CHECKSUM(NEWID()));
    SET @dep = CASE @rr1 % 5 WHEN 0 THEN 100000 WHEN 1 THEN 200000 WHEN 2 THEN 300000 WHEN 3 THEN 500000 ELSE 500000 END;
    SET @dtime = DATEADD(MINUTE, @rr1 % 60, DATEADD(HOUR, 8 + (@rr1 % 12), CAST(DATEADD(DAY, @rr1 % 25, CAST('2026-05-12' AS date)) AS datetime)));
    SET @wbal = @wbal + @dep;
    INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
    VALUES (@wwid, 'Deposit', @dep, @wbal, N'Nạp tiền vào ví qua cổng thanh toán', NULL, @dtime);
    SET @dbonus = CASE WHEN @dep >= 500000 THEN 50000 WHEN @dep >= 200000 THEN 15000 ELSE 0 END;
    IF @dbonus > 0
    BEGIN
        SET @wbal = @wbal + @dbonus;
        INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
        VALUES (@wwid, 'Bonus', @dbonus, @wbal, CONCAT(N'Khuyến mãi nạp tiền: tặng ', FORMAT(@dbonus, 'N0', 'vi-VN'), N'đ'), NULL, DATEADD(SECOND, 5, @dtime));
    END
    UPDATE #wal SET bal = @wbal WHERE uid = @wuid;
    FETCH NEXT FROM wdep INTO @wuid, @wwid, @wbal;
END
CLOSE wdep; DEALLOCATE wdep;

-- ---------------------------------------------------------------
-- 4) Bang theo doi diem / thuong thang trong luc seed
-- ---------------------------------------------------------------
CREATE TABLE #pts (uid int PRIMARY KEY, pts int, life int, firstdone bit);
INSERT #pts SELECT uid, 0, 0, 0 FROM #cust;
CREATE TABLE #mon (uid int, ym char(7), cnt int, bonusgiven bit, PRIMARY KEY (uid, ym));

-- ---------------------------------------------------------------
-- 5) Sinh ~180 booking ngau nhien 15/05 -> 31/08 (gio toi duoc chuong hon)
-- ---------------------------------------------------------------
CREATE TABLE #dates (d date PRIMARY KEY);
DECLARE @dd date = '2026-05-15';
WHILE @dd <= '2026-08-31' BEGIN INSERT #dates VALUES (@dd); SET @dd = DATEADD(DAY, 1, @dd); END

CREATE TABLE #cand (
    tsid int, fid int, bdate date, stime time, etime time, uid int,
    rs int, rm int, rp int, rq int, rr int, rt int, rn int, rx int,
    PRIMARY KEY (bdate, tsid)
);
INSERT #cand (tsid, fid, bdate, stime, etime, uid, rs, rm, rp, rq, rr, rt, rn, rx)
SELECT TOP (180) ts.TimeSlotId, ts.FieldId, d.d, ts.StartTime, ts.EndTime, 0,
    ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100,
    ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100,
    ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100
FROM #dates d CROSS JOIN TimeSlots ts
WHERE ts.IsActive = 1
  AND (ABS(CHECKSUM(NEWID())) % 1000) < CASE WHEN ts.StartTime >= '17:00' AND ts.StartTime < '21:00' THEN 78 ELSE 17 END
ORDER BY NEWID();

DECLARE @nw int = (SELECT COUNT(*) FROM #uw);
UPDATE #cand SET uid = (SELECT uid FROM #uw WHERE id = 1 + ABS(CHECKSUM(NEWID())) % @nw);

-- Ep 2 booking ngay vang 15/08 (san Thong Nhat, x5 diem) de demo tinh nang
IF NOT EXISTS (SELECT 1 FROM #cand WHERE bdate = '2026-08-15' AND tsid = 13)
    INSERT #cand VALUES (13, 1, '2026-08-15', '18:00', '19:00', (SELECT TOP 1 uid FROM #uw ORDER BY id), 10, 30, 99, 99, 20, 40, 99, 10);
IF NOT EXISTS (SELECT 1 FROM #cand WHERE bdate = '2026-08-15' AND tsid = 15)
    INSERT #cand VALUES (15, 1, '2026-08-15', '20:00', '21:00', (SELECT TOP 1 uid FROM #uw WHERE id > 4 ORDER BY id), 10, 60, 99, 99, 20, 55, 99, 20);

-- ---------------------------------------------------------------
-- 6) Vong lap chinh: tao booking + payment + vi + diem + review + notification
-- ---------------------------------------------------------------
DECLARE @tsid int, @fid int, @bdate date, @stime time, @etime time, @uid int,
        @rs int, @rm int, @rp int, @rq int, @rr int, @rt int, @rn int, @rx int;
DECLARE @fname nvarchar(100), @acceptW bit, @cashPct int, @uname nvarchar(100), @uphone nvarchar(20);
DECLARE @isWeekend bit, @price decimal(18,2), @promoId int, @promoDisc decimal(18,2),
        @usePts int, @maxp int, @discount decimal(18,2), @total decimal(18,2),
        @status nvarchar(20), @method nvarchar(20), @willPay bit, @past bit,
        @created datetime, @paidAt datetime, @doneAt datetime, @startAt datetime, @refT datetime, @revT datetime,
        @bid int, @wid int, @bal decimal(18,2), @pts int, @life int, @first bit,
        @ym char(7), @cnt int, @mbonus bit, @earn int, @mult int, @cb decimal(18,2),
        @note nvarchar(200), @createdBy int, @rating int, @cmt nvarchar(300), @payStatus nvarchar(20);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT tsid, fid, bdate, stime, etime, uid, rs, rm, rp, rq, rr, rt, rn, rx
    FROM #cand ORDER BY bdate, stime, tsid;
OPEN cur;
FETCH NEXT FROM cur INTO @tsid, @fid, @bdate, @stime, @etime, @uid, @rs, @rm, @rp, @rq, @rr, @rt, @rn, @rx;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @fname = FieldName, @acceptW = AcceptWalletPayment, @cashPct = CashbackPercent FROM Fields WHERE FieldId = @fid;
    SELECT @uname = uname FROM #cust WHERE uid = @uid;
    SELECT @uphone = Phone FROM Users WHERE UserId = @uid;
    SELECT @wid = wid, @bal = bal FROM #wal WHERE uid = @uid;
    SELECT @pts = pts, @life = life, @first = firstdone FROM #pts WHERE uid = @uid;

    SET @isWeekend = CASE WHEN DATEPART(WEEKDAY, @bdate) IN (6, 7) THEN 1 ELSE 0 END;
    SET @past = CASE WHEN @bdate < @today THEN 1 ELSE 0 END;
    SET @startAt = CAST(@bdate AS datetime) + CAST(@stime AS datetime);

    -- Gia theo bang gia linh hoat (uu tien Priority cao nhat), khong co rule thi lay gia goc
    SET @price = NULL;
    SELECT TOP 1 @price = r.Price
    FROM FieldPricingRules r
    WHERE r.FieldId = @fid AND r.IsActive = 1
      AND r.StartTime <= @stime AND @stime < r.EndTime
      AND (r.DayType = 'All'
           OR (r.DayType = 'Weekend' AND @isWeekend = 1)
           OR (r.DayType = 'Weekday' AND @isWeekend = 0))
      AND (r.StartMonth IS NULL
           OR (r.StartMonth <= r.EndMonth AND MONTH(@bdate) BETWEEN r.StartMonth AND r.EndMonth)
           OR (r.StartMonth > r.EndMonth AND (MONTH(@bdate) >= r.StartMonth OR MONTH(@bdate) <= r.EndMonth)))
    ORDER BY r.Priority DESC;
    IF @price IS NULL SELECT @price = PricePerHour FROM Fields WHERE FieldId = @fid;

    -- Trang thai
    IF @past = 1
        SET @status = CASE WHEN @rs < 78 THEN 'Completed' WHEN @rs < 88 THEN 'CancelPaid' WHEN @rs < 96 THEN 'CancelFree' ELSE 'Completed' END;
    ELSE
        SET @status = CASE WHEN @rs < 55 THEN 'Confirmed' WHEN @rs < 85 THEN 'PendingCash' ELSE 'Pending' END;
    SET @willPay = CASE WHEN @status IN ('Completed', 'CancelPaid', 'Confirmed') THEN 1 ELSE 0 END;

    -- Thoi gian tao / thanh toan / hoan thanh
    IF @past = 1
        SET @created = DATEADD(MINUTE, @rt % 60, DATEADD(HOUR, 7 + (@rx % 14), CAST(DATEADD(DAY, -(1 + @rt % 5), @bdate) AS datetime)));
    ELSE
    BEGIN
        SET @created = DATEADD(MINUTE, @rt % 60, DATEADD(HOUR, 7 + (@rx % 14), CAST(DATEADD(DAY, -(1 + @rt % 7), @bdate) AS datetime)));
        IF @created >= @now SET @created = DATEADD(HOUR, -(4 + @rx % 60), @now);
    END
    SET @paidAt = DATEADD(MINUTE, 5 + @rq % 40, @created);
    SET @doneAt = DATEADD(MINUTE, 2 + @rr % 10, CAST(@bdate AS datetime) + CAST(@etime AS datetime));

    -- Khuyen mai SUMMER26: 20% toi da 100k (chi ap dung 01/06 - 31/08)
    SET @promoId = NULL; SET @promoDisc = 0;
    IF @rp < 15 AND @bdate >= '2026-06-01' AND @bdate <= '2026-08-31'
    BEGIN
        SET @promoId = 1;
        SET @promoDisc = ROUND(@price * 20 / 100.0, 0);
        IF @promoDisc > 100000 SET @promoDisc = 100000;
    END

    -- Dung diem (toi da 50% gia tri, 1 diem = 100d) - chi khi thanh toan
    SET @usePts = 0;
    IF @willPay = 1 AND @pts >= 40 AND @rq < 35
    BEGIN
        SET @maxp = FLOOR((@price - @promoDisc) * 0.5 / 100.0);
        SET @usePts = @pts;
        IF @usePts > @maxp SET @usePts = @maxp;
        IF @usePts > 50 + (@rq * 5) SET @usePts = 50 + (@rq * 5);
        IF @usePts < 10 SET @usePts = 0;
    END

    SET @discount = @promoDisc + @usePts * 100;
    SET @total = @price - @discount;
    IF @total < 0 SET @total = 0;

    -- Phuong thuc thanh toan
    SET @method = NULL;
    IF @willPay = 1
    BEGIN
        IF @acceptW = 1
            SET @method = CASE WHEN @rm < 45 THEN 'Wallet' WHEN @rm < 80 THEN 'VNPay' ELSE 'Cash' END;
        ELSE
            SET @method = CASE WHEN @rm < 60 THEN 'VNPay' ELSE 'Cash' END;

        -- Vi khong du tien: 70% nap them ngay truoc khi tra, 30% doi sang VNPay
        IF @method = 'Wallet' AND @bal < @total
        BEGIN
            IF @rx < 70
            BEGIN
                SET @dep = CEILING((@total - @bal) / 100000.0) * 100000;
                IF @rx % 3 = 0 SET @dep = @dep + 100000;
                IF @dep < 100000 SET @dep = 100000;
                SET @bal = @bal + @dep;
                INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
                VALUES (@wid, 'Deposit', @dep, @bal, N'Nạp tiền vào ví qua cổng thanh toán', NULL, DATEADD(MINUTE, -12, @paidAt));
                SET @dbonus = CASE WHEN @dep >= 500000 THEN 50000 WHEN @dep >= 200000 THEN 15000 ELSE 0 END;
                IF @dbonus > 0
                BEGIN
                    SET @bal = @bal + @dbonus;
                    INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
                    VALUES (@wid, 'Bonus', @dbonus, @bal, CONCAT(N'Khuyến mãi nạp tiền: tặng ', FORMAT(@dbonus, 'N0', 'vi-VN'), N'đ'), NULL, DATEADD(MINUTE, -11, @paidAt));
                END
            END
            ELSE
                SET @method = 'VNPay';
        END
    END

    -- Ghi chu + nguoi dat ho
    SET @note = CASE WHEN @rn < 6 THEN N'Đặt sân cho nhóm bạn công ty'
                     WHEN @rn < 10 THEN N'Trận giao hữu cuối tuần'
                     WHEN @rn < 13 THEN N'Nhờ chuẩn bị thêm nước uống'
                     ELSE NULL END;
    SET @createdBy = CASE WHEN @method = 'Cash' AND @rn % 10 < 4 THEN @staffId ELSE NULL END;

    -- Booking
    INSERT Bookings (UserId, FieldId, TimeSlotId, BookingDate, PromotionId, Status, UnitPrice, DiscountAmount, PointsUsed, TotalAmount, Note, CreatedById, CreatedAt)
    VALUES (@uid, @fid, @tsid, @bdate, @promoId,
            CASE @status WHEN 'CancelPaid' THEN 'Cancelled' WHEN 'CancelFree' THEN 'Cancelled' WHEN 'PendingCash' THEN 'Pending' ELSE @status END,
            @price, CASE WHEN @willPay = 1 THEN @discount ELSE @promoDisc END,
            CASE WHEN @willPay = 1 THEN @usePts ELSE 0 END,
            CASE WHEN @willPay = 1 THEN @total ELSE @price - @promoDisc END,
            @note, @createdBy, @created);
    SET @bid = SCOPE_IDENTITY();

    IF @willPay = 1
    BEGIN
        -- Tru diem (Redeem)
        IF @usePts > 0
        BEGIN
            SET @pts = @pts - @usePts;
            INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
            VALUES (@uid, 'Redeem', -@usePts, CONCAT(N'Dùng ', @usePts, N' điểm trừ ', FORMAT(@usePts * 100, 'N0', 'vi-VN'), N'đ cho booking #', @bid), @bid, @paidAt);
        END

        -- Tru vi + cashback
        IF @method = 'Wallet'
        BEGIN
            SET @bal = @bal - @total;
            INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
            VALUES (@wid, 'Payment', -@total, @bal, CONCAT(N'Thanh toán booking #', @bid, N' - ', @fname), @bid, @paidAt);
            IF @cashPct > 0 AND @total > 0
            BEGIN
                SET @cb = ROUND(@total * @cashPct / 100.0, 0);
                SET @bal = @bal + @cb;
                INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
                VALUES (@wid, 'Cashback', @cb, @bal, CONCAT(N'Cashback ', @cashPct, N'% booking #', @bid), @bid, DATEADD(MINUTE, 1, @paidAt));
            END
        END

        SET @payStatus = CASE WHEN @status = 'CancelPaid' THEN 'Refunded' ELSE 'Paid' END;
        -- Hoan tien qua chuyen khoan dang cho duyet -> payment van la Paid
        IF @status = 'CancelPaid' AND @method <> 'Wallet' AND @acceptW = 0 AND @bdate >= '2026-08-18'
            SET @payStatus = 'Paid';
        INSERT Payments (BookingId, Amount, Method, Status, TransactionCode, PaidAt)
        VALUES (@bid, @total, @method, @payStatus, CONCAT(UPPER(@method), '-', FORMAT(@paidAt, 'yyyyMMddHHmmss'), '-', @bid), @paidAt);
    END

    IF @status = 'PendingCash'
        INSERT Payments (BookingId, Amount, Method, Status, TransactionCode, PaidAt)
        VALUES (@bid, @price - @promoDisc, 'Cash', 'Pending', NULL, NULL);

    -- Hoan thanh: tich diem + thuong + review + notification
    IF @status = 'Completed'
    BEGIN
        SET @mult = CASE WHEN @bdate = '2026-08-15' AND @fid = 1 THEN 5 ELSE 1 END;
        SET @earn = FLOOR(@total / 10000) * @mult;
        IF @earn > 0
        BEGIN
            SET @pts = @pts + @earn; SET @life = @life + @earn;
            INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
            VALUES (@uid, 'Earn', @earn,
                    CONCAT(N'Tích điểm booking #', @bid, CASE WHEN @mult = 5 THEN N' (x5 Sinh nhật sân Thống Nhất)' ELSE N'' END),
                    @bid, @doneAt);

            IF @first = 0
            BEGIN
                SET @pts = @pts + 20; SET @life = @life + 20; SET @first = 1;
                INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
                VALUES (@uid, 'Earn', 20, N'Thưởng hoàn thành booking đầu tiên', @bid, DATEADD(SECOND, 3, @doneAt));
            END

            SET @ym = FORMAT(@bdate, 'MM/yyyy');
            IF EXISTS (SELECT 1 FROM #mon WHERE uid = @uid AND ym = @ym)
                UPDATE #mon SET cnt = cnt + 1 WHERE uid = @uid AND ym = @ym;
            ELSE
                INSERT #mon VALUES (@uid, @ym, 1, 0);
            SELECT @cnt = cnt, @mbonus = bonusgiven FROM #mon WHERE uid = @uid AND ym = @ym;
            IF @cnt >= 5 AND @mbonus = 0
            BEGIN
                SET @pts = @pts + 50; SET @life = @life + 50;
                UPDATE #mon SET bonusgiven = 1 WHERE uid = @uid AND ym = @ym;
                INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
                VALUES (@uid, 'Earn', 50, CONCAT(N'Thưởng hoàn thành đủ 5 booking trong tháng ', @ym), @bid, DATEADD(SECOND, 6, @doneAt));
            END

            INSERT Notifications (UserId, Title, Message, Url, IsRead, CreatedAt)
            VALUES (@uid, N'Booking hoàn thành — tích điểm',
                    CONCAT(N'Booking #', @bid, N' đã hoàn thành. Bạn được cộng ', @earn, N' điểm',
                           CASE WHEN @mult = 5 THEN N' (x5 ngày vàng Sinh nhật sân Thống Nhất)' ELSE N'' END,
                           N'. Số dư điểm hiện tại: ', @pts, N'.'),
                    '/Wallet/Index',
                    CASE WHEN @doneAt < '2026-08-18' THEN 1 ELSE @rt % 2 END, @doneAt);
        END

        -- Review (~35% booking hoan thanh)
        IF @rr < 35
        BEGIN
            SET @rating = CASE WHEN @rt < 60 THEN 5 WHEN @rt < 85 THEN 4 WHEN @rt < 95 THEN 3 ELSE 2 END;
            SET @cmt = CASE @rating
                WHEN 5 THEN CASE @rx % 3 WHEN 0 THEN N'Sân đẹp, mặt sân chất lượng, sẽ quay lại thường xuyên!'
                                         WHEN 1 THEN N'Dịch vụ tuyệt vời, chủ sân thân thiện, đặt sân online rất tiện.'
                                         ELSE N'Rất hài lòng, ánh sáng tốt, chỗ gửi xe rộng rãi.' END
                WHEN 4 THEN CASE @rx % 2 WHEN 0 THEN N'Sân ổn, giá hợp lý. Sẽ ủng hộ tiếp.'
                                         ELSE N'Khá tốt, chỉ tiếc phòng thay đồ hơi nhỏ.' END
                WHEN 3 THEN N'Bình thường, đèn buổi tối hơi tối, mong chủ sân nâng cấp.'
                ELSE N'Mặt sân xuống cấp, cần bảo trì sớm.' END;
            SET @revT = DATEADD(HOUR, 1 + @rt % 20, @doneAt);
            IF @revT >= @now SET @revT = DATEADD(MINUTE, 30, @doneAt);
            IF @revT < @now
            BEGIN
                INSERT Reviews (FieldId, UserId, BookingId, Rating, Comment, CreatedAt)
                VALUES (@fid, @uid, @bid, @rating, @cmt, @revT);
                SET @pts = @pts + 10; SET @life = @life + 10;
                INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
                VALUES (@uid, 'ReviewBonus', 10, CONCAT(N'Thưởng đánh giá booking #', @bid), @bid, @revT);
            END
        END
    END

    -- Huy sau khi da thanh toan: hoan diem + hoan tien (vi hoac chuyen khoan)
    IF @status = 'CancelPaid'
    BEGIN
        SET @refT = DATEADD(HOUR, 4 + @rt % 40, @paidAt);
        IF @refT >= @startAt SET @refT = DATEADD(HOUR, -3, @startAt);
        IF @refT <= @paidAt SET @refT = DATEADD(MINUTE, 45, @paidAt);

        IF @usePts > 0
        BEGIN
            SET @pts = @pts + @usePts; -- hoan diem khong cong LifetimePoints
            INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
            VALUES (@uid, 'Revoke', @usePts, CONCAT(N'Hoàn ', @usePts, N' điểm (hủy booking) - booking #', @bid), @bid, @refT);
        END

        IF @method = 'Wallet' OR @acceptW = 1
        BEGIN
            SET @bal = @bal + @total;
            INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
            VALUES (@wid, 'Refund', @total, @bal, CONCAT(N'Hoàn tiền hủy booking #', @bid, N' - ', @fname), @bid, @refT);
            INSERT Notifications (UserId, Title, Message, Url, IsRead, CreatedAt)
            VALUES (@uid, N'Hoàn tiền vào ví',
                    CONCAT(N'Booking #', @bid, N' (', @fname, N') đã hủy. ', FORMAT(@total, 'N0', 'vi-VN'), N'đ được hoàn vào ví của bạn.'),
                    '/Wallet/Index', CASE WHEN @refT < '2026-08-18' THEN 1 ELSE 0 END, @refT);
        END
        ELSE
        BEGIN
            INSERT RefundRequests (BookingId, UserId, Amount, Reason, Status, BankAccountNumber, BankName, BankAccountHolder, ProcessedNote, ProcessedById, ProcessedAt, CreatedAt)
            VALUES (@bid, @uid, @total, N'Bận việc đột xuất, không thể đến sân.',
                    CASE WHEN @bdate >= '2026-08-18' THEN 'Pending' ELSE 'Completed' END,
                    CONCAT('9704', RIGHT(CONCAT('000000', @uphone), 9)),
                    CASE @uid % 4 WHEN 0 THEN N'Vietcombank' WHEN 1 THEN N'Techcombank' WHEN 2 THEN N'MB Bank' ELSE N'ACB' END,
                    UPPER(@uname),
                    CASE WHEN @bdate >= '2026-08-18' THEN NULL ELSE N'Đã chuyển khoản hoàn tiền cho khách.' END,
                    CASE WHEN @bdate >= '2026-08-18' THEN NULL ELSE @adminId END,
                    CASE WHEN @bdate >= '2026-08-18' THEN NULL ELSE DATEADD(DAY, 1, @refT) END,
                    @refT);
        END
    END

    UPDATE #pts SET pts = @pts, life = @life, firstdone = @first WHERE uid = @uid;
    UPDATE #wal SET bal = @bal WHERE uid = @uid;

    FETCH NEXT FROM cur INTO @tsid, @fid, @bdate, @stime, @etime, @uid, @rs, @rm, @rp, @rq, @rr, @rt, @rn, @rx;
END
CLOSE cur; DEALLOCATE cur;

-- ---------------------------------------------------------------
-- 7) Chot so du diem + vi ve bang chinh
-- ---------------------------------------------------------------
UPDATE u SET u.Points = p.pts, u.LifetimePoints = p.life
FROM Users u JOIN #pts p ON p.uid = u.UserId;

UPDATE w SET w.Balance = x.bal, w.UpdatedAt = @now
FROM Wallets w JOIN #wal x ON x.wid = w.WalletId;

COMMIT;

-- ---------------------------------------------------------------
-- Tong ket
-- ---------------------------------------------------------------
PRINT N'=== SEED XONG ===';
SELECT 'Bookings' AS Bang, COUNT(*) AS SoDong FROM Bookings
UNION ALL SELECT 'Payments', COUNT(*) FROM Payments
UNION ALL SELECT 'WalletTransactions', COUNT(*) FROM WalletTransactions
UNION ALL SELECT 'PointTransactions', COUNT(*) FROM PointTransactions
UNION ALL SELECT 'Reviews', COUNT(*) FROM Reviews
UNION ALL SELECT 'Notifications', COUNT(*) FROM Notifications
UNION ALL SELECT 'RefundRequests', COUNT(*) FROM RefundRequests
UNION ALL SELECT 'Users', COUNT(*) FROM Users
UNION ALL SELECT 'Wallets', COUNT(*) FROM Wallets;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT CONCAT(N'LOI: ', ERROR_MESSAGE(), N' (dong ', ERROR_LINE(), N')');
    THROW;
END CATCH
