/* =============================================================
   SportsFieldBookingDB v2 - He thong dat san the thao (ban nang cap)
   - Bo BookingDetails: 1 booking = 1 san + 1 khung gio + 1 ngay (muc 9)
   - Bang gia nhieu cap FieldPricingRules + GoldenDays (muc 1, 2)
   - Dia chi tach Tinh/Xa/Duong theo API hanh chinh (muc 7)
   - Vi tien ao Wallets + WalletTransactions (muc 4)
   - Tich diem PointTransactions + hang thanh vien (muc 5)
   - Yeu cau bao tri MaintenanceRequests (muc 8)
   - Promotion theo chu san (muc 11), SystemSettings cau hinh
   Chay script nay trong SSMS truoc khi mo project.
   LUU Y: script DROP toan bo database cu.
   ============================================================= */
USE master;
GO
IF DB_ID('SportsFieldBookingDB') IS NOT NULL
BEGIN
    ALTER DATABASE SportsFieldBookingDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE SportsFieldBookingDB;
END
GO
CREATE DATABASE SportsFieldBookingDB;
GO
USE SportsFieldBookingDB;
GO

/* ---------------- Roles & Users ---------------- */
CREATE TABLE Roles (
    RoleId      INT IDENTITY(1,1) PRIMARY KEY,
    RoleName    NVARCHAR(50) NOT NULL UNIQUE       -- Admin / Owner / Staff / Customer
);

CREATE TABLE Users (
    UserId         INT IDENTITY(1,1) PRIMARY KEY,
    FullName       NVARCHAR(100) NOT NULL,
    Email          NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash   NVARCHAR(256) NOT NULL,
    Phone          NVARCHAR(20)  NULL,
    RoleId         INT NOT NULL REFERENCES Roles(RoleId),
    IsActive       BIT NOT NULL DEFAULT 1,
    PointBalance   INT NOT NULL DEFAULT 0,          -- diem hien co
    LifetimePoints INT NOT NULL DEFAULT 0,          -- diem tich luy tron doi (tinh hang)
    CreatedAt      DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Fields ---------------- */
CREATE TABLE FieldTypes (
    FieldTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TypeName    NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Fields (
    FieldId             INT IDENTITY(1,1) PRIMARY KEY,
    FieldName           NVARCHAR(100) NOT NULL,
    FieldTypeId         INT NOT NULL REFERENCES FieldTypes(FieldTypeId),
    OwnerId             INT NOT NULL REFERENCES Users(UserId),
    Street              NVARCHAR(200) NOT NULL,      -- duong, so nha
    WardCode            NVARCHAR(20)  NOT NULL,      -- ma phuong/xa (API hanh chinh)
    WardName            NVARCHAR(100) NOT NULL,
    ProvinceCode        NVARCHAR(20)  NOT NULL,      -- ma tinh/thanh pho
    ProvinceName        NVARCHAR(100) NOT NULL,
    PricePerHour        DECIMAL(12,0) NOT NULL,      -- gia mac dinh khi khong rule nao khop
    Description         NVARCHAR(1000) NULL,
    Status              NVARCHAR(20) NOT NULL DEFAULT 'Active',  -- Active / Maintenance / Closed
    AcceptWalletPayment BIT NOT NULL DEFAULT 0,      -- chu san bat/tat nhan tien ao (muc 4)
    CashbackPercent     INT NOT NULL DEFAULT 0,      -- % hoan vi khi tra bang vi
    CreatedAt           DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE FieldImages (
    ImageId   INT IDENTITY(1,1) PRIMARY KEY,
    FieldId   INT NOT NULL REFERENCES Fields(FieldId) ON DELETE CASCADE,
    ImageUrl  NVARCHAR(300) NOT NULL,
    IsPrimary BIT NOT NULL DEFAULT 0
);

CREATE TABLE TimeSlots (
    TimeSlotId INT IDENTITY(1,1) PRIMARY KEY,
    FieldId    INT NOT NULL REFERENCES Fields(FieldId) ON DELETE CASCADE,
    StartTime  TIME NOT NULL,
    EndTime    TIME NOT NULL,
    IsActive   BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_TimeSlot UNIQUE (FieldId, StartTime)
);

/* ---------------- Bang gia nhieu cap (muc 2) ----------------
   Dieu kien NULL = khong xet. Chon rule khop co Priority cao nhat;
   khong rule nao khop -> Fields.PricePerHour. */
CREATE TABLE FieldPricingRules (
    PricingRuleId INT IDENTITY(1,1) PRIMARY KEY,
    FieldId       INT NOT NULL REFERENCES Fields(FieldId) ON DELETE CASCADE,
    RuleName      NVARCHAR(100) NOT NULL,
    DayOfWeek     INT NULL,            -- 0=CN ... 6=Thu 7 (theo .NET DayOfWeek)
    StartDate     DATE NULL,           -- khoang ngay (mua / dip le)
    EndDate       DATE NULL,
    StartTime     TIME NULL,           -- khung gio (cao diem rieng tung san)
    EndTime       TIME NULL,
    Price         DECIMAL(12,0) NOT NULL,
    Priority      INT NOT NULL DEFAULT 0,
    IsActive      BIT NOT NULL DEFAULT 1
);

/* ---------------- Ngay vang (muc 1) ---------------- */
CREATE TABLE GoldenDays (
    GoldenDayId     INT IDENTITY(1,1) PRIMARY KEY,
    [Date]          DATE NOT NULL,
    Name            NVARCHAR(100) NOT NULL,
    PointMultiplier INT NOT NULL DEFAULT 2,          -- nhan diem khi dat ngay vang
    DiscountPercent INT NOT NULL DEFAULT 0,          -- uu dai gia (0 = chi nhan diem)
    FieldId         INT NULL REFERENCES Fields(FieldId),  -- NULL = toan he thong
    IsActive        BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_GoldenDay UNIQUE ([Date], FieldId)
);

/* ---------------- Promotions (muc 11: theo chu san) ---------------- */
CREATE TABLE Promotions (
    PromotionId     INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(30) NOT NULL UNIQUE,
    Description     NVARCHAR(200) NULL,
    DiscountPercent INT NOT NULL CHECK (DiscountPercent BETWEEN 1 AND 100),
    MaxDiscount     DECIMAL(12,0) NOT NULL DEFAULT 0,   -- 0 = khong gioi han
    StartDate       DATE NOT NULL,
    EndDate         DATE NOT NULL,
    Quantity        INT NOT NULL DEFAULT 0,
    IsActive        BIT NOT NULL DEFAULT 1,
    OwnerId         INT NULL REFERENCES Users(UserId),  -- NULL = ma toan he thong (Admin)
    FieldId         INT NULL REFERENCES Fields(FieldId) -- NULL = moi san (trong pham vi owner)
);

/* ---------------- Bookings (muc 9: 1 booking = 1 slot) ---------------- */
CREATE TABLE Bookings (
    BookingId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL REFERENCES Users(UserId),      -- khach su dung san
    CreatedById    INT NULL REFERENCES Users(UserId),          -- staff/admin dat ho (muc 10)
    GuestName      NVARCHAR(100) NULL,                         -- khach vang lai
    GuestPhone     NVARCHAR(20) NULL,
    FieldId        INT NOT NULL REFERENCES Fields(FieldId),
    TimeSlotId     INT NOT NULL REFERENCES TimeSlots(TimeSlotId),
    BookingDate    DATE NOT NULL,
    UnitPrice      DECIMAL(12,0) NOT NULL,                     -- gia goc theo rule
    PromotionId    INT NULL REFERENCES Promotions(PromotionId),
    PromoDiscount  DECIMAL(12,0) NOT NULL DEFAULT 0,
    TierDiscount   DECIMAL(12,0) NOT NULL DEFAULT 0,           -- giam theo hang (muc 5)
    PointsUsed     INT NOT NULL DEFAULT 0,
    PointsDiscount DECIMAL(12,0) NOT NULL DEFAULT 0,
    TotalAmount    DECIMAL(12,0) NOT NULL DEFAULT 0,
    PointsEarned   INT NOT NULL DEFAULT 0,                     -- de thu hoi khi huy
    Status         NVARCHAR(20) NOT NULL DEFAULT 'Pending',    -- Pending/Confirmed/Completed/Cancelled
    Note           NVARCHAR(500) NULL,
    CreatedAt      DATETIME NOT NULL DEFAULT GETDATE()
);

/* Chong dat trung: 1 san + 1 khung gio + 1 ngay chi co 1 booking con hieu luc */
CREATE UNIQUE INDEX UX_Bookings_NoOverlap
    ON Bookings (FieldId, TimeSlotId, BookingDate)
    WHERE Status IN ('Pending','Confirmed','Completed');

/* ---------------- Payments ---------------- */
CREATE TABLE Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    BookingId       INT NOT NULL REFERENCES Bookings(BookingId),
    Amount          DECIMAL(12,0) NOT NULL,
    Method          NVARCHAR(20) NOT NULL,               -- Momo / Cash / Wallet
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending/Paid/Refunded/RefundPending
    TransactionCode NVARCHAR(50) NULL,
    PaidAt          DATETIME NULL
);

/* Chan thanh toan trung: moi booking toi da 1 payment dang hieu luc */
CREATE UNIQUE INDEX UX_Payments_OneActivePerBooking
    ON Payments (BookingId)
    WHERE Status IN ('Pending','Paid');

/* ---------------- Reviews ---------------- */
CREATE TABLE Reviews (
    ReviewId  INT IDENTITY(1,1) PRIMARY KEY,
    FieldId   INT NOT NULL REFERENCES Fields(FieldId),
    UserId    INT NOT NULL REFERENCES Users(UserId),
    BookingId INT NOT NULL UNIQUE REFERENCES Bookings(BookingId), -- 1 booking chi review 1 lan
    Rating    INT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Comment   NVARCHAR(1000) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Vi tien ao (muc 4) ---------------- */
CREATE TABLE Wallets (
    WalletId  INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NOT NULL UNIQUE REFERENCES Users(UserId),
    Balance   DECIMAL(12,0) NOT NULL DEFAULT 0,
    IsLocked  BIT NOT NULL DEFAULT 0,
    UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE WalletTransactions (
    WalletTransactionId INT IDENTITY(1,1) PRIMARY KEY,
    WalletId     INT NOT NULL REFERENCES Wallets(WalletId),
    [Type]       NVARCHAR(20) NOT NULL,   -- Deposit/Payment/Refund/Cashback/Bonus/Adjust
    Amount       DECIMAL(12,0) NOT NULL,  -- duong = cong, am = tru
    BalanceAfter DECIMAL(12,0) NOT NULL,
    BookingId    INT NULL REFERENCES Bookings(BookingId),
    Note         NVARCHAR(300) NULL,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Tich diem (muc 5) ---------------- */
CREATE TABLE PointTransactions (
    PointTransactionId INT IDENTITY(1,1) PRIMARY KEY,
    UserId       INT NOT NULL REFERENCES Users(UserId),
    [Type]       NVARCHAR(30) NOT NULL,   -- Earn/Revoke/Redeem/ReviewBonus/FirstBookingBonus/VoucherRedeem/Adjust
    Points       INT NOT NULL,            -- duong = cong, am = tru
    BalanceAfter INT NOT NULL,
    BookingId    INT NULL REFERENCES Bookings(BookingId),
    Note         NVARCHAR(300) NULL,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Yeu cau bao tri (muc 8) ---------------- */
CREATE TABLE MaintenanceRequests (
    MaintenanceRequestId INT IDENTITY(1,1) PRIMARY KEY,
    FieldId       INT NOT NULL REFERENCES Fields(FieldId),
    RequestedById INT NOT NULL REFERENCES Users(UserId),
    FromDate      DATE NOT NULL,
    ToDate        DATE NOT NULL,
    Reason        NVARCHAR(500) NOT NULL,
    Status        NVARCHAR(20) NOT NULL DEFAULT 'Pending',  -- Pending/Approved/Rejected
    AdminNote     NVARCHAR(500) NULL,
    CreatedAt     DATETIME NOT NULL DEFAULT GETDATE(),
    DecidedAt     DATETIME NULL
);

/* ---------------- Cau hinh he thong ---------------- */
CREATE TABLE SystemSettings (
    SettingKey   NVARCHAR(50) NOT NULL PRIMARY KEY,
    SettingValue NVARCHAR(200) NOT NULL
);
GO

/* =============================================================
   SEED DATA  (mat khau cac tai khoan demo: 123456)
   Tai khoan Admin duoc dong bo tu appsettings.json khi chay app.
   ============================================================= */
INSERT INTO Roles (RoleName) VALUES (N'Admin'), (N'Owner'), (N'Staff'), (N'Customer');

-- SHA256('123456')
DECLARE @pw NVARCHAR(256) = N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92';
INSERT INTO Users (FullName, Email, PasswordHash, Phone, RoleId) VALUES
(N'Nguyễn Chủ Sân',   N'owner@sfb.com',    @pw, N'0900000002', 2),  -- UserId 1: Owner
(N'Lê Nhân Viên',     N'staff@sfb.com',    @pw, N'0900000005', 3),  -- UserId 2: Staff
(N'Trần Văn Khách',   N'customer@sfb.com', @pw, N'0900000003', 4),  -- UserId 3: Customer
(N'Lê Thị Hoa',       N'hoa@gmail.com',    @pw, N'0900000004', 4);  -- UserId 4: Customer

INSERT INTO FieldTypes (TypeName) VALUES (N'Bóng đá'), (N'Cầu lông'), (N'Tennis'), (N'Bóng rổ');

-- Dia chi theo don vi hanh chinh CHUAN API v2 provinces.open-api.vn
-- (ma tinh: Ha Noi = 1, TP.HCM = 79; ma phuong lay dung tu API sau sap nhap 2025)
INSERT INTO Fields (FieldName, FieldTypeId, OwnerId, Street, WardCode, WardName, ProvinceCode, ProvinceName, PricePerHour, Description, AcceptWalletPayment, CashbackPercent) VALUES
(N'Sân bóng Thống Nhất',    1, 1, N'123 Lê Lợi',        N'26740', N'Phường Sài Gòn',    N'79', N'Thành phố Hồ Chí Minh', 300000, N'Sân cỏ nhân tạo 7 người, có đèn chiếu sáng', 1, 5),
(N'Sân cầu lông Victory',   2, 1, N'45 Nguyễn Huệ',     N'26743', N'Phường Bến Thành',  N'79', N'Thành phố Hồ Chí Minh', 80000,  N'4 sân thi đấu chuẩn, sàn gỗ',               1, 0),
(N'Sân tennis Sao Mai',     3, 1, N'78 Trần Thái Tông', N'166',   N'Phường Cầu Giấy',   N'1',  N'Thành phố Hà Nội',      200000, N'Sân cứng ngoài trời, có mái che',           0, 0),
(N'Sân bóng rổ Phoenix',    4, 1, N'12 Đinh Tiên Hoàng',N'70',    N'Phường Hoàn Kiếm',  N'1',  N'Thành phố Hà Nội',      150000, N'Sân trong nhà, điều hòa',                   1, 3),
(N'Sân bóng Gò Vấp Arena',  1, 1, N'88 Quang Trung',    N'26884', N'Phường Gò Vấp',     N'79', N'Thành phố Hồ Chí Minh', 250000, N'Sân cỏ nhân tạo 5 người, 3 sân liền kề',    1, 2),
(N'Sân cầu lông Thủ Đức',   2, 1, N'22 Võ Văn Ngân',    N'26824', N'Phường Thủ Đức',    N'79', N'Thành phố Hồ Chí Minh', 70000,  N'6 sân thảm PVC, đèn LED chống chói',        1, 0),
(N'Sân tennis Hồ Tây',      3, 1, N'5 Lạc Long Quân',   N'103',   N'Phường Tây Hồ',     N'1',  N'Thành phố Hà Nội',      220000, N'View hồ, 2 sân đất nện',                    0, 0),
(N'Sân bóng rổ Hà Đông',    4, 1, N'99 Quang Trung',    N'9556',  N'Phường Hà Đông',    N'1',  N'Thành phố Hà Nội',      130000, N'Sân ngoài trời tiêu chuẩn',                 1, 0),
(N'Sân bóng Củ Chi Sport',  1, 1, N'12 Tỉnh lộ 8',      N'27553', N'Xã Củ Chi',         N'79', N'Thành phố Hồ Chí Minh', 180000, N'Sân cỏ tự nhiên 11 người',                  0, 0),
(N'Sân cầu lông Ba Đình',   2, 1, N'3 Đội Cấn',         N'4',     N'Phường Ba Đình',    N'1',  N'Thành phố Hà Nội',      90000,  N'Nhà thi đấu 8 sân, có khán đài',            1, 5);

-- Anh: san bong da dung anh chup that; cac mon khac dung anh minh hoa SVG dung mon
INSERT INTO FieldImages (FieldId, ImageUrl, IsPrimary) VALUES
(1, N'/uploads/fields/1992b28d51f44b4ba0acbfa82a5632c7.jpg', 1),
(2, N'/images/field-badminton.svg', 1),
(3, N'/images/field-tennis.svg', 1),
(4, N'/images/field-basketball.svg', 1),
(5, N'/uploads/fields/1992b28d51f44b4ba0acbfa82a5632c7.jpg', 1),
(6, N'/images/field-badminton.svg', 1),
(7, N'/images/field-tennis.svg', 1),
(8, N'/images/field-basketball.svg', 1),
(9, N'/uploads/fields/1992b28d51f44b4ba0acbfa82a5632c7.jpg', 1),
(10, N'/images/field-badminton.svg', 1);

-- Khung gio 06:00 - 22:00, moi slot 1 gio, cho tat ca cac san
DECLARE @f INT = 1;
WHILE @f <= 10
BEGIN
    DECLARE @h INT = 6;
    WHILE @h < 22
    BEGIN
        INSERT INTO TimeSlots (FieldId, StartTime, EndTime)
        VALUES (@f, TIMEFROMPARTS(@h,0,0,0,0), TIMEFROMPARTS(@h+1,0,0,0,0));
        SET @h += 1;
    END
    SET @f += 1;
END

-- Bang gia mau: gio cao diem 17-21h (moi san gia rieng), cuoi tuan, mua he
INSERT INTO FieldPricingRules (FieldId, RuleName, DayOfWeek, StartDate, EndDate, StartTime, EndTime, Price, Priority) VALUES
(1, N'Giờ cao điểm 17-21h', NULL, NULL, NULL, '17:00', '21:00', 450000, 10),
(1, N'Cuối tuần - Thứ 7',   6,    NULL, NULL, NULL,    NULL,    350000, 5),
(1, N'Cuối tuần - Chủ nhật',0,    NULL, NULL, NULL,    NULL,    350000, 5),
(2, N'Giờ cao điểm 18-21h', NULL, NULL, NULL, '18:00', '21:00', 120000, 10),
(3, N'Giờ cao điểm 17-20h', NULL, NULL, NULL, '17:00', '20:00', 300000, 10),
(3, N'Mùa hè 2026',         NULL, '2026-06-01', '2026-08-31', NULL, NULL, 250000, 3),
(4, N'Giờ cao điểm 17-21h', NULL, NULL, NULL, '17:00', '21:00', 220000, 10),
(5, N'Giờ cao điểm 18-22h', NULL, NULL, NULL, '18:00', '22:00', 380000, 10),
(6, N'Giờ cao điểm 18-21h', NULL, NULL, NULL, '18:00', '21:00', 100000, 10),
(10, N'Cuối tuần - Thứ 7',  6,    NULL, NULL, NULL,    NULL,    120000, 5),
(10, N'Cuối tuần - Chủ nhật', 0,  NULL, NULL, NULL,    NULL,    120000, 5);

-- Ngay vang mau
INSERT INTO GoldenDays ([Date], Name, PointMultiplier, DiscountPercent, FieldId) VALUES
('2026-09-02', N'Quốc khánh 2/9', 2, 10, NULL),
('2026-08-15', N'Sinh nhật sân Thống Nhất', 3, 15, 1);

-- Khuyen mai: 2 ma toan he thong + 1 ma cua chu san cho san Victory
INSERT INTO Promotions (Code, Description, DiscountPercent, MaxDiscount, StartDate, EndDate, Quantity, OwnerId, FieldId) VALUES
(N'SUMMER26',  N'Giảm 20% mùa hè',                 20, 100000, '2026-06-01', '2026-08-31', 100, NULL, NULL),
(N'NEWBIE10',  N'Giảm 10% khách hàng mới',         10, 50000,  '2026-01-01', '2026-12-31', 500, NULL, NULL),
(N'VICTORY20', N'Chủ sân Victory giảm 20%',        20, 80000,  '2026-07-01', '2026-09-30', 50,  1,    2);

-- Vi cho cac tai khoan demo
INSERT INTO Wallets (UserId, Balance) VALUES (1, 0), (2, 0), (3, 200000), (4, 0);
INSERT INTO WalletTransactions (WalletId, [Type], Amount, BalanceAfter, Note)
SELECT WalletId, N'Deposit', 200000, 200000, N'Nạp thử nghiệm (seed)' FROM Wallets WHERE UserId = 3;

-- Cau hinh he thong (Admin sua duoc)
INSERT INTO SystemSettings (SettingKey, SettingValue) VALUES
(N'PointsEarnRate',        N'10000'),   -- 10.000d = 1 diem
(N'RedeemUnitPoints',      N'100'),     -- moi 100 diem
(N'RedeemUnitValue',       N'10000'),   -- ... doi duoc 10.000d
(N'RedeemMaxPercent',      N'50'),      -- diem tru toi da 50% gia tri booking
(N'FirstBookingBonus',     N'20'),      -- thuong lan dat dau tien
(N'ReviewBonus',           N'5'),       -- thuong khi review
(N'DepositBonusThreshold', N'500000'),  -- nap tu 500k...
(N'DepositBonusAmount',    N'50000'),   -- ...tang 50k
(N'TierSilverPoints',      N'500'),
(N'TierGoldPoints',        N'2000'),
(N'TierDiamondPoints',     N'5000'),
(N'TierSilverDiscount',    N'3'),
(N'TierGoldDiscount',      N'5'),
(N'TierDiamondDiscount',   N'10'),
(N'VoucherCostPoints',     N'200'),     -- doi voucher: 200 diem
(N'VoucherPercent',        N'10'),      -- voucher giam 10%
(N'VoucherMaxDiscount',    N'50000'),
(N'MaintenanceCompensationPoints', N'20'); -- diem den bu khi huy do bao tri
GO

PRINT N'Tao database SportsFieldBookingDB v2 thanh cong!';
