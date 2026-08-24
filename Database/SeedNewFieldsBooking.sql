-- =====================================================================
-- SEED BOOKING CHO 3 SAN MOI (K+, B+, A+ cua staff@sfb.com)
-- Dung khach hang co san, noi tiep dung so du vi + diem hien tai.
-- Booking tu 21/08 (sau ngay tao san) den 05/09/2026.
-- =====================================================================
SET NOCOUNT ON;
SET DATEFIRST 1;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM Bookings WHERE FieldId = 1002)
BEGIN
    RAISERROR(N'San K+ da co booking. Khong chay lai.', 16, 1);
    RETURN;
END

BEGIN TRY
BEGIN TRAN;

DECLARE @now    datetime = '2026-08-24T11:30:00';
DECLARE @today  date     = '2026-08-24';
-- Moc an toan: moi giao dich vi MOI phai sau giao dich vi cu cuoi cung (22/08 21:54)
DECLARE @walletFloor datetime = '2026-08-23T06:00:00';
DECLARE @adminId int = (SELECT TOP 1 UserId FROM Users WHERE Email = 'admin@sfb.com');

-- Khach hang co san + trang thai diem / vi hien tai
CREATE TABLE #cust (uid int PRIMARY KEY, uname nvarchar(100), uphone nvarchar(20));
INSERT #cust SELECT UserId, FullName, ISNULL(Phone, '0900000000') FROM Users WHERE RoleId = 3;

CREATE TABLE #pts (uid int PRIMARY KEY, pts int, life int, firstdone bit);
INSERT #pts
SELECT u.UserId, u.Points, u.LifetimePoints,
       CASE WHEN EXISTS (SELECT 1 FROM PointTransactions t WHERE t.UserId = u.UserId AND t.Description = N'Thưởng hoàn thành booking đầu tiên') THEN 1 ELSE 0 END
FROM Users u JOIN #cust c ON c.uid = u.UserId;

-- So booking Completed thang 08 hien co + da nhan thuong thang 08 chua
CREATE TABLE #mon (uid int PRIMARY KEY, cnt int, bonusgiven bit);
INSERT #mon
SELECT c.uid,
       (SELECT COUNT(*) FROM Bookings b WHERE b.UserId = c.uid AND b.Status = 'Completed' AND b.BookingDate >= '2026-08-01' AND b.BookingDate < '2026-09-01'),
       CASE WHEN EXISTS (SELECT 1 FROM PointTransactions t WHERE t.UserId = c.uid AND t.Description = N'Thưởng hoàn thành đủ 5 booking trong tháng 08/2026') THEN 1 ELSE 0 END
FROM #cust c;

CREATE TABLE #wal (uid int PRIMARY KEY, wid int, bal decimal(18,2));
INSERT #wal SELECT w.UserId, w.WalletId, w.Balance FROM Wallets w JOIN #cust c ON c.uid = w.UserId;

-- Trong so: khach quen dat nhieu hon
CREATE TABLE #uw (id int IDENTITY(1,1) PRIMARY KEY, uid int NOT NULL);
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('tuanngo.fc@gmail.com','ducanh.tran92@gmail.com','phonglh.sport@gmail.com','customer@sfb.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('tuanngo.fc@gmail.com','ducanh.tran92@gmail.com','phonglh.sport@gmail.com','customer@sfb.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('maipham.badminton@gmail.com','huyvu.tennis@gmail.com','namdo.fc88@gmail.com','hoa@gmail.com','khoadang.futsal@gmail.com');
INSERT #uw (uid) SELECT UserId FROM Users WHERE Email IN ('trangbui.sport@gmail.com','sonhoang.basket@gmail.com','kimchi.nguyen01@gmail.com','binhly.sport@gmail.com','nhungcao.tennis@gmail.com');

-- Ung vien booking: 21/08 -> 05/09, chi khung gio cua 3 san moi, uu tien gio toi
CREATE TABLE #dates (d date PRIMARY KEY);
DECLARE @dd date = '2026-08-21';
WHILE @dd <= '2026-09-05' BEGIN INSERT #dates VALUES (@dd); SET @dd = DATEADD(DAY, 1, @dd); END

CREATE TABLE #cand (
    tsid int, fid int, bdate date, stime time, etime time, uid int,
    rs int, rm int, rp int, rq int, rr int, rt int, rn int, rx int,
    PRIMARY KEY (bdate, tsid)
);
INSERT #cand (tsid, fid, bdate, stime, etime, uid, rs, rm, rp, rq, rr, rt, rn, rx)
SELECT TOP (13) ts.TimeSlotId, ts.FieldId, d.d, ts.StartTime, ts.EndTime, 0,
    ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100,
    ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100,
    ABS(CHECKSUM(NEWID())) % 100, ABS(CHECKSUM(NEWID())) % 100
FROM #dates d
CROSS JOIN TimeSlots ts
JOIN Fields f ON f.FieldId = ts.FieldId
WHERE ts.FieldId = 1002 AND ts.IsActive = 1
  AND d.d >= CAST(f.CreatedAt AS date)   -- khong dat truoc khi san ton tai
  AND (ABS(CHECKSUM(NEWID())) % 1000) < CASE WHEN ts.StartTime >= '17:00' AND ts.StartTime < '21:00' THEN 400 ELSE 90 END
ORDER BY NEWID();

DECLARE @nw int = (SELECT COUNT(*) FROM #uw);
UPDATE #cand SET uid = (SELECT uid FROM #uw WHERE id = 1 + ABS(CHECKSUM(NEWID())) % @nw);

-- ---------------------------------------------------------------
-- Vong lap chinh
-- ---------------------------------------------------------------
DECLARE @tsid int, @fid int, @bdate date, @stime time, @etime time, @uid int,
        @rs int, @rm int, @rp int, @rq int, @rr int, @rt int, @rn int, @rx int;
DECLARE @fname nvarchar(100), @acceptW bit, @cashPct int, @fcreated datetime, @uname nvarchar(100), @uphone nvarchar(20);
DECLARE @isWeekend bit, @price decimal(18,2), @promoId int, @promoDisc decimal(18,2),
        @usePts int, @maxp int, @discount decimal(18,2), @total decimal(18,2),
        @status nvarchar(20), @method nvarchar(20), @willPay bit, @past bit,
        @created datetime, @paidAt datetime, @doneAt datetime, @startAt datetime, @refT datetime, @revT datetime,
        @bid int, @wid int, @bal decimal(18,2), @pts int, @life int, @first bit,
        @cnt int, @mbonus bit, @earn int, @cb decimal(18,2), @dep decimal(18,2), @dbonus decimal(18,2),
        @note nvarchar(200), @rating int, @cmt nvarchar(300), @payStatus nvarchar(20);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT tsid, fid, bdate, stime, etime, uid, rs, rm, rp, rq, rr, rt, rn, rx
    FROM #cand ORDER BY bdate, stime, tsid;
OPEN cur;
FETCH NEXT FROM cur INTO @tsid, @fid, @bdate, @stime, @etime, @uid, @rs, @rm, @rp, @rq, @rr, @rt, @rn, @rx;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @fname = FieldName, @acceptW = AcceptWalletPayment, @cashPct = CashbackPercent, @fcreated = CreatedAt FROM Fields WHERE FieldId = @fid;
    SELECT @uname = uname, @uphone = uphone FROM #cust WHERE uid = @uid;
    SELECT @wid = wid, @bal = bal FROM #wal WHERE uid = @uid;
    SELECT @pts = pts, @life = life, @first = firstdone FROM #pts WHERE uid = @uid;

    SET @isWeekend = CASE WHEN DATEPART(WEEKDAY, @bdate) IN (6, 7) THEN 1 ELSE 0 END;
    SET @past = CASE WHEN @bdate < @today THEN 1 ELSE 0 END;
    SET @startAt = CAST(@bdate AS datetime) + CAST(@stime AS datetime);

    SET @price = NULL;
    SELECT TOP 1 @price = r.Price
    FROM FieldPricingRules r
    WHERE r.FieldId = @fid AND r.IsActive = 1
      AND r.StartTime <= @stime AND @stime < r.EndTime
      AND (r.DayType = 'All' OR (r.DayType = 'Weekend' AND @isWeekend = 1) OR (r.DayType = 'Weekday' AND @isWeekend = 0))
      AND (r.StartMonth IS NULL
           OR (r.StartMonth <= r.EndMonth AND MONTH(@bdate) BETWEEN r.StartMonth AND r.EndMonth)
           OR (r.StartMonth > r.EndMonth AND (MONTH(@bdate) >= r.StartMonth OR MONTH(@bdate) <= r.EndMonth)))
    ORDER BY r.Priority DESC;
    IF @price IS NULL SELECT @price = PricePerHour FROM Fields WHERE FieldId = @fid;

    IF @past = 1
        SET @status = CASE WHEN @rs < 88 THEN 'Completed' ELSE 'CancelPaid' END;
    ELSE
        SET @status = CASE WHEN @rs < 50 THEN 'Confirmed' WHEN @rs < 80 THEN 'PendingCash' ELSE 'Pending' END;
    SET @willPay = CASE WHEN @status IN ('Completed', 'CancelPaid', 'Confirmed') THEN 1 ELSE 0 END;

    -- Thoi diem tao: sau khi san duoc tao, truoc gio da
    SET @created = DATEADD(MINUTE, @rt % 50, DATEADD(HOUR, 8 + (@rx % 10), CAST(DATEADD(DAY, -(1 + @rt % 2), @bdate) AS datetime)));
    IF @created < DATEADD(MINUTE, 90, @fcreated) SET @created = DATEADD(MINUTE, 90 + (@rt % 120), @fcreated);
    IF @created >= @now SET @created = DATEADD(HOUR, -(2 + @rx % 20), @now);
    IF @created < DATEADD(MINUTE, 90, @fcreated) SET @created = DATEADD(MINUTE, 90 + (@rt % 60), @fcreated);
    SET @paidAt = DATEADD(MINUTE, 5 + @rq % 30, @created);
    SET @doneAt = DATEADD(MINUTE, 2 + @rr % 10, CAST(@bdate AS datetime) + CAST(@etime AS datetime));

    -- Khuyen mai SUMMER26 (het 31/08)
    SET @promoId = NULL; SET @promoDisc = 0;
    IF @rp < 15 AND @bdate <= '2026-08-31'
    BEGIN
        SET @promoId = 1;
        SET @promoDisc = ROUND(@price * 20 / 100.0, 0);
        IF @promoDisc > 100000 SET @promoDisc = 100000;
    END

    -- Dung diem
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

    -- Phuong thuc: booking QUA KHU chi VNPay/Cash (de giao dich vi moi luon nam sau giao dich vi cu);
    -- booking tuong lai (thanh toan 23-24/08) duoc dung vi
    SET @method = NULL;
    IF @willPay = 1
    BEGIN
        IF @past = 1
            SET @method = CASE WHEN @rm < 60 THEN 'VNPay' ELSE 'Cash' END;
        ELSE IF @acceptW = 1
            SET @method = CASE WHEN @rm < 45 THEN 'Wallet' WHEN @rm < 80 THEN 'VNPay' ELSE 'Cash' END;
        ELSE
            SET @method = CASE WHEN @rm < 60 THEN 'VNPay' ELSE 'Cash' END;

        IF @method = 'Wallet'
        BEGIN
            -- Thanh toan vi phai sau moc an toan
            IF @paidAt < @walletFloor SET @paidAt = DATEADD(MINUTE, 30 + @rq % 90, @walletFloor);
            IF @paidAt >= @now SET @paidAt = DATEADD(MINUTE, -(10 + @rq % 60), @now);
            IF @bal < @total
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
    END

    SET @note = CASE WHEN @rn < 8 THEN N'Đặt sân cho nhóm bạn' WHEN @rn < 12 THEN N'Khai trương sân mới, đến ủng hộ' ELSE NULL END;

    INSERT Bookings (UserId, FieldId, TimeSlotId, BookingDate, PromotionId, Status, UnitPrice, DiscountAmount, PointsUsed, TotalAmount, Note, CreatedById, CreatedAt)
    VALUES (@uid, @fid, @tsid, @bdate, @promoId,
            CASE @status WHEN 'CancelPaid' THEN 'Cancelled' WHEN 'PendingCash' THEN 'Pending' ELSE @status END,
            @price, CASE WHEN @willPay = 1 THEN @discount ELSE @promoDisc END,
            CASE WHEN @willPay = 1 THEN @usePts ELSE 0 END,
            CASE WHEN @willPay = 1 THEN @total ELSE @price - @promoDisc END,
            @note, NULL, @created);
    SET @bid = SCOPE_IDENTITY();

    IF @willPay = 1
    BEGIN
        IF @usePts > 0
        BEGIN
            SET @pts = @pts - @usePts;
            INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
            VALUES (@uid, 'Redeem', -@usePts, CONCAT(N'Dùng ', @usePts, N' điểm trừ ', FORMAT(@usePts * 100, 'N0', 'vi-VN'), N'đ cho booking #', @bid), @bid, @paidAt);
        END

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
        INSERT Payments (BookingId, Amount, Method, Status, TransactionCode, PaidAt)
        VALUES (@bid, @total, @method, @payStatus, CONCAT(UPPER(@method), '-', FORMAT(@paidAt, 'yyyyMMddHHmmss'), '-', @bid), @paidAt);
    END

    IF @status = 'PendingCash'
        INSERT Payments (BookingId, Amount, Method, Status, TransactionCode, PaidAt)
        VALUES (@bid, @price - @promoDisc, 'Cash', 'Pending', NULL, NULL);

    IF @status = 'Completed'
    BEGIN
        SET @earn = FLOOR(@total / 10000);
        IF @earn > 0
        BEGIN
            SET @pts = @pts + @earn; SET @life = @life + @earn;
            INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
            VALUES (@uid, 'Earn', @earn, CONCAT(N'Tích điểm booking #', @bid), @bid, @doneAt);

            IF @first = 0
            BEGIN
                SET @pts = @pts + 20; SET @life = @life + 20; SET @first = 1;
                INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
                VALUES (@uid, 'Earn', 20, N'Thưởng hoàn thành booking đầu tiên', @bid, DATEADD(SECOND, 3, @doneAt));
            END

            UPDATE #mon SET cnt = cnt + 1 WHERE uid = @uid;
            SELECT @cnt = cnt, @mbonus = bonusgiven FROM #mon WHERE uid = @uid;
            IF @cnt >= 5 AND @mbonus = 0
            BEGIN
                SET @pts = @pts + 50; SET @life = @life + 50;
                UPDATE #mon SET bonusgiven = 1 WHERE uid = @uid;
                INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
                VALUES (@uid, 'Earn', 50, N'Thưởng hoàn thành đủ 5 booking trong tháng 08/2026', @bid, DATEADD(SECOND, 6, @doneAt));
            END

            INSERT Notifications (UserId, Title, Message, Url, IsRead, CreatedAt)
            VALUES (@uid, N'Booking hoàn thành — tích điểm',
                    CONCAT(N'Booking #', @bid, N' đã hoàn thành. Bạn được cộng ', @earn, N' điểm. Số dư điểm hiện tại: ', @pts, N'.'),
                    '/Wallet/Index', @rt % 2, @doneAt);
        END

        IF @rr < 45
        BEGIN
            SET @rating = CASE WHEN @rt < 65 THEN 5 WHEN @rt < 90 THEN 4 ELSE 3 END;
            SET @cmt = CASE @rating
                WHEN 5 THEN CASE @rx % 2 WHEN 0 THEN N'Sân mới khai trương, mọi thứ đều mới và sạch. Rất đáng thử!'
                                         ELSE N'Chất lượng sân tốt, chủ sân nhiệt tình, sẽ quay lại.' END
                WHEN 4 THEN N'Sân mới đẹp, giá ổn, chỗ gửi xe hơi nhỏ.'
                ELSE N'Sân ổn nhưng dịch vụ cần hoàn thiện thêm vì mới mở.' END;
            SET @revT = DATEADD(HOUR, 1 + @rt % 12, @doneAt);
            IF @revT >= @now SET @revT = DATEADD(MINUTE, 20, @doneAt);
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

    IF @status = 'CancelPaid'
    BEGIN
        SET @refT = DATEADD(HOUR, 3 + @rt % 12, @paidAt);
        IF @refT >= @startAt SET @refT = DATEADD(HOUR, -2, @startAt);
        IF @refT <= @paidAt SET @refT = DATEADD(MINUTE, 40, @paidAt);

        IF @usePts > 0
        BEGIN
            SET @pts = @pts + @usePts;
            INSERT PointTransactions (UserId, Type, Points, Description, BookingId, CreatedAt)
            VALUES (@uid, 'Revoke', @usePts, CONCAT(N'Hoàn ', @usePts, N' điểm (hủy booking) - booking #', @bid), @bid, @refT);
        END

        IF (@method = 'Wallet' OR @acceptW = 1) AND @wid IS NOT NULL
        BEGIN
            -- Hoan vi phai sau moc an toan
            IF @refT < @walletFloor SET @refT = DATEADD(MINUTE, 60 + @rt % 180, @walletFloor);
            IF @refT >= @now SET @refT = DATEADD(MINUTE, -(5 + @rt % 30), @now);
            SET @bal = @bal + @total;
            INSERT WalletTransactions (WalletId, Type, Amount, BalanceAfter, Description, BookingId, CreatedAt)
            VALUES (@wid, 'Refund', @total, @bal, CONCAT(N'Hoàn tiền hủy booking #', @bid, N' - ', @fname), @bid, @refT);
            INSERT Notifications (UserId, Title, Message, Url, IsRead, CreatedAt)
            VALUES (@uid, N'Hoàn tiền vào ví',
                    CONCAT(N'Booking #', @bid, N' (', @fname, N') đã hủy. ', FORMAT(@total, 'N0', 'vi-VN'), N'đ được hoàn vào ví của bạn.'),
                    '/Wallet/Index', 0, @refT);
        END
        ELSE
        BEGIN
            INSERT RefundRequests (BookingId, UserId, Amount, Reason, Status, BankAccountNumber, BankName, BankAccountHolder, ProcessedNote, ProcessedById, ProcessedAt, CreatedAt)
            VALUES (@bid, @uid, @total, N'Đổi lịch, không đến được.', 'Pending',
                    CONCAT('9704', RIGHT(CONCAT('000000', @uphone), 9)),
                    CASE @uid % 4 WHEN 0 THEN N'Vietcombank' WHEN 1 THEN N'Techcombank' WHEN 2 THEN N'MB Bank' ELSE N'ACB' END,
                    UPPER(@uname), NULL, NULL, NULL, @refT);
        END
    END

    UPDATE #pts SET pts = @pts, life = @life, firstdone = @first WHERE uid = @uid;
    UPDATE #wal SET bal = @bal WHERE uid = @uid;

    FETCH NEXT FROM cur INTO @tsid, @fid, @bdate, @stime, @etime, @uid, @rs, @rm, @rp, @rq, @rr, @rt, @rn, @rx;
END
CLOSE cur; DEALLOCATE cur;

-- Chot diem + vi
UPDATE u SET u.Points = p.pts, u.LifetimePoints = p.life
FROM Users u JOIN #pts p ON p.uid = u.UserId;

UPDATE w SET w.Balance = x.bal, w.UpdatedAt = @now
FROM Wallets w JOIN #wal x ON x.wid = w.WalletId;

-- Tinh lai chuoi BalanceAfter theo thu tu thoi gian (phong khi lech)
;WITH z AS (SELECT WalletTransactionId, BalanceAfter, RunSum = SUM(Amount) OVER (PARTITION BY WalletId ORDER BY CreatedAt, WalletTransactionId ROWS UNBOUNDED PRECEDING) FROM WalletTransactions)
UPDATE z SET BalanceAfter = RunSum WHERE BalanceAfter <> RunSum;

COMMIT;

PRINT N'=== SEED SAN MOI XONG ===';
SELECT f.FieldName, SoBooking = COUNT(*),
       HoanThanh = SUM(CASE WHEN b.Status = 'Completed' THEN 1 ELSE 0 END),
       DaXacNhan = SUM(CASE WHEN b.Status = 'Confirmed' THEN 1 ELSE 0 END),
       ChoXuLy   = SUM(CASE WHEN b.Status = 'Pending' THEN 1 ELSE 0 END),
       DaHuy     = SUM(CASE WHEN b.Status = 'Cancelled' THEN 1 ELSE 0 END),
       DoanhThu  = ISNULL(SUM(CASE WHEN p.Status = 'Paid' THEN p.Amount END), 0)
FROM Fields f
LEFT JOIN Bookings b ON b.FieldId = f.FieldId
LEFT JOIN Payments p ON p.BookingId = b.BookingId
WHERE f.FieldId IN (1002, 1003, 1004)
GROUP BY f.FieldName;

-- Kiem tra nhat quan
SELECT MismatchChuoi = COUNT(*) FROM (SELECT BalanceAfter, RunSum = SUM(Amount) OVER (PARTITION BY WalletId ORDER BY CreatedAt, WalletTransactionId ROWS UNBOUNDED PRECEDING) FROM WalletTransactions) z WHERE BalanceAfter <> RunSum;
SELECT SoDuAm = COUNT(*) FROM WalletTransactions WHERE BalanceAfter < 0;
SELECT MismatchVi = COUNT(*) FROM Wallets w CROSS APPLY (SELECT TOP 1 BalanceAfter FROM WalletTransactions t WHERE t.WalletId = w.WalletId ORDER BY t.CreatedAt DESC, t.WalletTransactionId DESC) x WHERE w.Balance <> x.BalanceAfter;
SELECT MismatchDiem = COUNT(*) FROM Users u LEFT JOIN (SELECT UserId, S = SUM(Points), L = SUM(CASE WHEN Points > 0 AND Type <> 'Revoke' THEN Points ELSE 0 END) FROM PointTransactions GROUP BY UserId) p ON p.UserId = u.UserId WHERE u.RoleId = 3 AND (u.Points <> ISNULL(p.S, 0) OR u.LifetimePoints <> ISNULL(p.L, 0));

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT CONCAT(N'LOI: ', ERROR_MESSAGE(), N' (dong ', ERROR_LINE(), N')');
    THROW;
END CATCH
