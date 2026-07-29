/* =============================================================
   SportsFieldBookingDB - He thong dat san the thao (ban chinh sua)
   Chay script nay trong SSMS (SQL Server 2019/2022) truoc khi mo project

   Thay doi lon so voi ban cu:
   - BO bang BookingDetails: 1 booking = 1 san + 1 khung gio + 1 ngay
   - Them FieldPricingRules (gia theo gio/ngay/thang-mua), GoldenDays (ngay vang)
   - Them Wallets + WalletTransactions (vi tien ao, chu san bat/tat theo san)
   - Them PointTransactions + Users.Points/LifetimePoints (tich diem, hang thanh vien)
   - Them MaintenanceRequests (chu san xin bao tri, admin duyet)
   - Fields: dia chi chi tiet Province/Ward/Address (chon tu API hanh chinh VN)
   - Promotions.OwnerId: ma khuyen mai cua chu san (gui qua email)
   - Them role Owner (chu san - dong thoi quan ly dat lich, khong con role Staff rieng)
   - Super account cuu ho nam trong appsettings.json (KHONG co trong DB)
   ============================================================= */
-- Bat buoc cho filtered index (sqlcmd mac dinh QUOTED_IDENTIFIER OFF se loi; SSMS thi mac dinh ON)
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
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
    RoleName    NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Users (
    UserId         INT IDENTITY(1,1) PRIMARY KEY,
    FullName       NVARCHAR(100) NOT NULL,
    Email          NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash   NVARCHAR(256) NOT NULL,
    Phone          NVARCHAR(20)  NULL,
    RoleId         INT NOT NULL REFERENCES Roles(RoleId),
    IsActive       BIT NOT NULL DEFAULT 1,
    Points         INT NOT NULL DEFAULT 0,   -- so du diem hien tai (tieu duoc)
    LifetimePoints INT NOT NULL DEFAULT 0,   -- diem tich luy tron doi -> xet hang thanh vien
    -- STK nhan tien hoan khi san khong nhan vi tien ao
    BankAccountNumber NVARCHAR(50)  NULL,
    BankName          NVARCHAR(100) NULL,
    BankAccountHolder NVARCHAR(150) NULL,
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
    Address             NVARCHAR(200) NOT NULL,  -- so nha, ten duong
    Ward                NVARCHAR(100) NOT NULL,  -- Phuong/Xa (tu API hanh chinh)
    Province            NVARCHAR(100) NOT NULL,  -- Tinh/Thanh pho (tu API hanh chinh)
    PricePerHour        DECIMAL(12,0) NOT NULL,  -- gia co ban (fallback khi khong co rule)
    AcceptWalletPayment BIT NOT NULL DEFAULT 0,  -- chu san bat/tat nhan vi tien ao
    CashbackPercent     INT NOT NULL DEFAULT 0,  -- % hoan vi khi tra bang vi
    Description         NVARCHAR(1000) NULL,
    Status              NVARCHAR(20) NOT NULL DEFAULT 'Active',  -- Active / Maintenance / Closed
    CreatedAt           DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE FieldImages (
    ImageId   INT IDENTITY(1,1) PRIMARY KEY,
    FieldId   INT NOT NULL REFERENCES Fields(FieldId) ON DELETE CASCADE,
    ImageUrl  NVARCHAR(300) NOT NULL,
    IsPrimary BIT NOT NULL DEFAULT 0
);

/* ---------------- TimeSlots ---------------- */
CREATE TABLE TimeSlots (
    TimeSlotId INT IDENTITY(1,1) PRIMARY KEY,
    FieldId    INT NOT NULL REFERENCES Fields(FieldId) ON DELETE CASCADE,
    StartTime  TIME NOT NULL,
    EndTime    TIME NOT NULL,
    IsActive   BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_TimeSlot UNIQUE (FieldId, StartTime)
);

/* ---------------- Bang gia da cap (moi san moi gio mot gia) ---------------- */
CREATE TABLE FieldPricingRules (
    PricingRuleId INT IDENTITY(1,1) PRIMARY KEY,
    FieldId       INT NOT NULL REFERENCES Fields(FieldId) ON DELETE CASCADE,
    RuleName      NVARCHAR(100) NULL,
    StartTime     TIME NOT NULL,               -- khung gio ap dung
    EndTime       TIME NOT NULL,
    DayType       NVARCHAR(10) NOT NULL DEFAULT 'All',  -- All / Weekday / Weekend
    StartMonth    INT NULL,                    -- khoang thang (mua), NULL = quanh nam
    EndMonth      INT NULL,                    -- ho tro vat qua nam (11 -> 2)
    Price         DECIMAL(12,0) NOT NULL,
    Priority      INT NOT NULL DEFAULT 1,      -- rule khop co Priority cao nhat thang
    IsActive      BIT NOT NULL DEFAULT 1
);

/* ---------------- Ngay vang ---------------- */
CREATE TABLE GoldenDays (
    GoldenDayId      INT IDENTITY(1,1) PRIMARY KEY,
    FieldId          INT NULL REFERENCES Fields(FieldId),  -- NULL = toan he thong (Admin tao)
    [Date]           DATE NOT NULL,
    Name             NVARCHAR(100) NOT NULL,
    PriceMultiplier  DECIMAL(4,2) NOT NULL DEFAULT 1,      -- he so gia
    PointsMultiplier INT NOT NULL DEFAULT 2,               -- he so tich diem
    IsActive         BIT NOT NULL DEFAULT 1
);

/* ---------------- Promotions ---------------- */
CREATE TABLE Promotions (
    PromotionId     INT IDENTITY(1,1) PRIMARY KEY,
    Code            NVARCHAR(30) NOT NULL UNIQUE,
    Description     NVARCHAR(200) NULL,
    DiscountPercent INT NOT NULL CHECK (DiscountPercent BETWEEN 1 AND 100),
    MaxDiscount     DECIMAL(12,0) NOT NULL DEFAULT 0,   -- 0 = khong gioi han
    StartDate       DATE NOT NULL,
    EndDate         DATE NOT NULL,
    Quantity        INT NOT NULL DEFAULT 0,             -- so luot con lai
    IsActive        BIT NOT NULL DEFAULT 1,
    OwnerId         INT NULL REFERENCES Users(UserId)   -- NULL = ma he thong; co gia tri = ma cua chu san
);

/* ---------------- Bookings (da bo BookingDetails) ---------------- */
CREATE TABLE Bookings (
    BookingId      INT IDENTITY(1,1) PRIMARY KEY,
    UserId         INT NOT NULL REFERENCES Users(UserId),
    FieldId        INT NOT NULL REFERENCES Fields(FieldId),
    TimeSlotId     INT NOT NULL REFERENCES TimeSlots(TimeSlotId),
    BookingDate    DATE NOT NULL,
    PromotionId    INT NULL REFERENCES Promotions(PromotionId),
    Status         NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending / Confirmed / Completed / Cancelled
    UnitPrice      DECIMAL(12,0) NOT NULL DEFAULT 0,  -- gia goc theo bang gia
    DiscountAmount DECIMAL(12,0) NOT NULL DEFAULT 0,  -- tong giam (KM + hang + diem)
    PointsUsed     INT NOT NULL DEFAULT 0,            -- so diem da dung
    TotalAmount    DECIMAL(12,0) NOT NULL DEFAULT 0,  -- so tien phai tra cuoi cung
    Note           NVARCHAR(500) NULL,
    CreatedById    INT NULL REFERENCES Users(UserId), -- Owner/Admin dat ho khach
    CreatedAt      DATETIME NOT NULL DEFAULT GETDATE()
);

/* Chong trung lich: 1 san + 1 khung gio + 1 ngay chi co 1 booking chua huy */
CREATE UNIQUE INDEX UX_Bookings_NoOverlap
    ON Bookings (FieldId, TimeSlotId, BookingDate)
    WHERE Status <> 'Cancelled';

/* ---------------- Payments ---------------- */
CREATE TABLE Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    BookingId       INT NOT NULL REFERENCES Bookings(BookingId),
    Amount          DECIMAL(12,0) NOT NULL,
    Method          NVARCHAR(20) NOT NULL,               -- VNPay / Momo / Cash / Wallet
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending / Paid / Refunded / Failed
    TransactionCode NVARCHAR(50) NULL,
    PaidAt          DATETIME NULL
);

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

/* ---------------- Vi tien ao ---------------- */
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
    [Type]       NVARCHAR(20) NOT NULL,   -- Deposit / Payment / Refund / Cashback / Bonus / Adjust
    Amount       DECIMAL(12,0) NOT NULL,  -- duong = cong, am = tru
    BalanceAfter DECIMAL(12,0) NOT NULL,  -- so du sau giao dich (doi soat)
    Description  NVARCHAR(300) NULL,
    BookingId    INT NULL,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Tich diem ---------------- */
CREATE TABLE PointTransactions (
    PointTransactionId INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT NOT NULL REFERENCES Users(UserId),
    [Type]      NVARCHAR(20) NOT NULL,   -- Earn / Redeem / ReviewBonus / Revoke / Adjust
    Points      INT NOT NULL,            -- duong = cong, am = tru
    Description NVARCHAR(300) NULL,
    BookingId   INT NULL,
    CreatedAt   DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Yeu cau bao tri ---------------- */
CREATE TABLE MaintenanceRequests (
    MaintenanceRequestId INT IDENTITY(1,1) PRIMARY KEY,
    FieldId     INT NOT NULL REFERENCES Fields(FieldId),
    OwnerId     INT NOT NULL REFERENCES Users(UserId),
    Reason      NVARCHAR(500) NOT NULL,
    StartDate   DATE NOT NULL,
    EndDate     DATE NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending / Approved / Rejected
    AdminNote   NVARCHAR(500) NULL,
    CreatedAt   DATETIME NOT NULL DEFAULT GETDATE(),
    ProcessedAt DATETIME NULL
);
GO

/* Yeu cau hoan tien ve STK - khi booking cua san KHONG nhan vi tien ao bi huy */
CREATE TABLE RefundRequests (
    RefundRequestId   INT IDENTITY(1,1) PRIMARY KEY,
    BookingId         INT NOT NULL REFERENCES Bookings(BookingId),
    UserId            INT NOT NULL REFERENCES Users(UserId),
    Amount            DECIMAL(12,0) NOT NULL,
    Reason            NVARCHAR(300) NOT NULL,
    Status            NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending / Completed / Rejected
    BankAccountNumber NVARCHAR(50) NULL,
    BankName          NVARCHAR(100) NULL,
    BankAccountHolder NVARCHAR(150) NULL,
    ProcessedNote     NVARCHAR(300) NULL,
    ProcessedById     INT NULL,
    ProcessedAt       DATETIME NULL,
    CreatedAt         DATETIME NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_RefundRequests_Status ON RefundRequests(Status);
GO

/* Ma OTP dat lai mat khau (chi Customer/Owner - Admin dung Super Account de cuu ho) */
CREATE TABLE PasswordResetOtps (
    PasswordResetOtpId INT IDENTITY(1,1) PRIMARY KEY,
    UserId       INT NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,
    OtpCode      NVARCHAR(10) NOT NULL,      -- 6 chu so
    ExpiresAt    DATETIME NOT NULL,          -- het han sau 10 phut
    IsUsed       BIT NOT NULL DEFAULT 0,
    AttemptCount INT NOT NULL DEFAULT 0,     -- nhap sai qua 5 lan thi vo hieu
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);
GO
CREATE INDEX IX_PasswordResetOtps_User ON PasswordResetOtps(UserId, IsUsed);
GO

/* =============================================================
   SEED DATA  (mat khau tat ca tai khoan: 123456)
   Super account cuu ho: khong nam trong DB/appsettings - chi luu SHA-256 hash trong AccountController
   ============================================================= */
INSERT INTO Roles (RoleName) VALUES (N'Admin'), (N'Owner'), (N'Customer');

-- SHA256('123456')
DECLARE @pw NVARCHAR(256) = N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92';
INSERT INTO Users (FullName, Email, PasswordHash, Phone, RoleId) VALUES
(N'Quản trị viên',   N'admin@sfb.com',    @pw, N'0900000001', 1),  -- 1 Admin
(N'Nguyễn Chủ Sân',  N'owner@sfb.com',    @pw, N'0900000002', 2),  -- 2 Owner (chu san)
(N'Phạm Chủ Sân',    N'staff@sfb.com',    @pw, N'0900000003', 2),  -- 3 Owner (chu san thu 2, chua co san)
(N'Trần Văn Khách',  N'customer@sfb.com', @pw, N'0900000004', 3),  -- 4 Customer
(N'Lê Thị Hoa',      N'hoa@gmail.com',    @pw, N'0900000005', 3);  -- 5 Customer

INSERT INTO FieldTypes (TypeName) VALUES (N'Bóng đá'), (N'Cầu lông'), (N'Tennis'), (N'Bóng rổ');

-- Dia chi theo don vi hanh chinh 2 cap (Tinh/Thanh -> Phuong/Xa) khop ten API provinces.open-api.vn v2
INSERT INTO Fields (FieldName, FieldTypeId, OwnerId, Address, Ward, Province, PricePerHour, AcceptWalletPayment, CashbackPercent, Description) VALUES
(N'Sân bóng Thống Nhất', 1, 2, N'123 Lê Lợi',       N'Phường Sài Gòn',    N'Thành phố Hồ Chí Minh', 300000, 1, 5, N'Sân cỏ nhân tạo 7 người, có đèn chiếu sáng'),
(N'Sân cầu lông Victory', 2, 2, N'45 Nguyễn Huệ',    N'Phường Bến Thành',  N'Thành phố Hồ Chí Minh', 80000,  1, 0, N'4 sân thi đấu chuẩn, sàn gỗ'),
(N'Sân tennis Sao Mai',   3, 2, N'78 Trần Hưng Đạo', N'Phường Cầu Giấy',   N'Thành phố Hà Nội',      200000, 0, 0, N'Sân cứng ngoài trời, có mái che'),
(N'Sân bóng rổ Phoenix',  4, 2, N'12 Hai Bà Trưng',  N'Phường Hoàn Kiếm',  N'Thành phố Hà Nội',      150000, 0, 0, N'Sân trong nhà, điều hòa');

INSERT INTO FieldImages (FieldId, ImageUrl, IsPrimary) VALUES
(1, N'/images/field-football.jpg', 1),
(2, N'/images/field-badminton.jpg', 1),
(3, N'/images/field-tennis.jpg', 1),
(4, N'/images/field-basketball.jpg', 1);

-- Khung gio 06:00 - 22:00, moi slot 1 gio, cho tat ca cac san
DECLARE @f INT = 1;
WHILE @f <= 4
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

/* Bang gia mau: gio cao diem toi (17-21h), cuoi tuan, mua he - moi san mot gia khac nhau */
INSERT INTO FieldPricingRules (FieldId, RuleName, StartTime, EndTime, DayType, StartMonth, EndMonth, Price, Priority) VALUES
-- San bong Thong Nhat (gia co ban 300k)
(1, N'Giờ cao điểm tối',      '17:00', '21:00', 'All',     NULL, NULL, 450000, 1),
(1, N'Cuối tuần cả ngày',     '06:00', '22:00', 'Weekend', NULL, NULL, 400000, 2),
(1, N'Cao điểm tối cuối tuần','17:00', '21:00', 'Weekend', NULL, NULL, 500000, 3),
(1, N'Mùa hè sáng sớm',       '06:00', '09:00', 'All',     5,    8,    350000, 4),
-- San cau long Victory (gia co ban 80k)
(2, N'Giờ cao điểm tối',      '18:00', '21:00', 'All',     NULL, NULL, 120000, 1),
(2, N'Cuối tuần',             '06:00', '22:00', 'Weekend', NULL, NULL, 100000, 2),
-- San tennis Sao Mai (gia co ban 200k)
(3, N'Giờ cao điểm tối',      '17:00', '21:00', 'All',     NULL, NULL, 300000, 1),
(3, N'Mùa đông tối lạnh',     '17:00', '21:00', 'All',     11,   2,    260000, 2),
-- San bong ro Phoenix (gia co ban 150k)
(4, N'Giờ cao điểm tối',      '17:00', '21:00', 'All',     NULL, NULL, 220000, 1);

/* Ngay vang mau: he thong (admin) + rieng cua san */
INSERT INTO GoldenDays (FieldId, [Date], Name, PriceMultiplier, PointsMultiplier) VALUES
(NULL, '2026-09-02', N'Quốc khánh 2/9',        1.5, 3),
(NULL, '2027-01-01', N'Tết Dương lịch',        1.5, 2),
(1,    '2026-08-15', N'Sinh nhật sân Thống Nhất', 1.2, 5);

/* Khuyen mai: ma he thong (OwnerId NULL) + ma cua chu san (OwnerId = 2, gui qua email) */
INSERT INTO Promotions (Code, Description, DiscountPercent, MaxDiscount, StartDate, EndDate, Quantity, OwnerId) VALUES
(N'SUMMER26', N'Giảm 20% mùa hè',                 20, 100000, '2026-06-01', '2026-08-31', 100, NULL),
(N'NEWBIE10', N'Giảm 10% khách hàng mới',         10, 50000,  '2026-01-01', '2026-12-31', 500, NULL),
(N'OWNER15',  N'Tri ân khách quen sân Thống Nhất',15, 80000,  '2026-07-01', '2026-12-31', 50,  2);

/* Vi mau cho khach demo (500k) - kem giao dich nap de doi soat */
INSERT INTO Wallets (UserId, Balance) VALUES (4, 500000), (5, 200000);
INSERT INTO WalletTransactions (WalletId, [Type], Amount, BalanceAfter, Description) VALUES
(1, N'Deposit', 500000, 500000, N'Nạp tiền vào ví (seed demo)'),
(2, N'Deposit', 200000, 200000, N'Nạp tiền vào ví (seed demo)');
GO

PRINT N'Tao database SportsFieldBookingDB (ban chinh sua) thanh cong!';
PRINT N'Tai khoan: admin@sfb.com / owner@sfb.com / staff@sfb.com / customer@sfb.com - mat khau: 123456';
PRINT N'Super account cuu ho: an hoan toan - chi ton tai duoi dang SHA-256 hash trong AccountController (dang nhap tai /Account/AdminLogin)';
