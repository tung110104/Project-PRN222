/* =============================================================
   SportsFieldBookingDB - He thong dat san the thao
   Chay script nay trong SSMS (SQL Server 2019/2022) truoc khi mo project
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
    RoleName    NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Users (
    UserId       INT IDENTITY(1,1) PRIMARY KEY,
    FullName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,
    Phone        NVARCHAR(20)  NULL,
    RoleId       INT NOT NULL REFERENCES Roles(RoleId),
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME NOT NULL DEFAULT GETDATE()
);

/* ---------------- Fields ---------------- */
CREATE TABLE FieldTypes (
    FieldTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TypeName    NVARCHAR(50) NOT NULL UNIQUE
);

CREATE TABLE Fields (
    FieldId          INT IDENTITY(1,1) PRIMARY KEY,
    FieldName        NVARCHAR(100) NOT NULL,
    FieldTypeId      INT NOT NULL REFERENCES FieldTypes(FieldTypeId),
    OwnerId          INT NOT NULL REFERENCES Users(UserId),
    Address          NVARCHAR(200) NOT NULL,
    District         NVARCHAR(50)  NOT NULL,
    City             NVARCHAR(50)  NOT NULL,
    PricePerHour     DECIMAL(12,0) NOT NULL,           -- gia gio thuong (VND)
    PeakPricePerHour DECIMAL(12,0) NOT NULL,           -- gia gio cao diem (17h-21h)
    Description      NVARCHAR(1000) NULL,
    Status           NVARCHAR(20) NOT NULL DEFAULT 'Active',  -- Active / Maintenance / Closed
    CreatedAt        DATETIME NOT NULL DEFAULT GETDATE()
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
    IsActive        BIT NOT NULL DEFAULT 1
);

/* ---------------- Bookings ---------------- */
CREATE TABLE Bookings (
    BookingId   INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT NOT NULL REFERENCES Users(UserId),
    PromotionId INT NULL REFERENCES Promotions(PromotionId),
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending / Confirmed / Cancelled / Completed
    TotalAmount DECIMAL(12,0) NOT NULL DEFAULT 0,
    Note        NVARCHAR(500) NULL,
    CreatedAt   DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE BookingDetails (
    BookingDetailId INT IDENTITY(1,1) PRIMARY KEY,
    BookingId   INT NOT NULL REFERENCES Bookings(BookingId) ON DELETE CASCADE,
    FieldId     INT NOT NULL REFERENCES Fields(FieldId),
    TimeSlotId  INT NOT NULL REFERENCES TimeSlots(TimeSlotId),
    BookingDate DATE NOT NULL,
    Price       DECIMAL(12,0) NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Active'   -- Active / Cancelled
);

/* Chong trung lich: 1 san + 1 khung gio + 1 ngay chi co 1 detail Active */
CREATE UNIQUE INDEX UX_BookingDetails_NoOverlap
    ON BookingDetails (FieldId, TimeSlotId, BookingDate)
    WHERE Status = 'Active';

/* ---------------- Payments ---------------- */
CREATE TABLE Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    BookingId       INT NOT NULL REFERENCES Bookings(BookingId),
    Amount          DECIMAL(12,0) NOT NULL,
    Method          NVARCHAR(20) NOT NULL,               -- VNPay / Momo / Cash
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
GO

/* =============================================================
   SEED DATA  (mat khau tat ca tai khoan: 123456)
   ============================================================= */
INSERT INTO Roles (RoleName) VALUES (N'Admin'), (N'Staff'), (N'Customer');

-- SHA256('123456')
DECLARE @pw NVARCHAR(256) = N'8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92';
INSERT INTO Users (FullName, Email, PasswordHash, Phone, RoleId) VALUES
(N'Quản trị viên',  N'admin@sfb.com',    @pw, N'0900000001', 1),
(N'Nguyễn Chủ Sân', N'staff@sfb.com',    @pw, N'0900000002', 2),
(N'Trần Văn Khách', N'customer@sfb.com', @pw, N'0900000003', 3),
(N'Lê Thị Hoa',     N'hoa@gmail.com',    @pw, N'0900000004', 3);

INSERT INTO FieldTypes (TypeName) VALUES (N'Bóng đá'), (N'Cầu lông'), (N'Tennis'), (N'Bóng rổ');

INSERT INTO Fields (FieldName, FieldTypeId, OwnerId, Address, District, City, PricePerHour, PeakPricePerHour, Description) VALUES
(N'Sân bóng Thống Nhất', 1, 2, N'123 Lê Lợi',       N'Quận 1',     N'TP.HCM', 300000, 450000, N'Sân cỏ nhân tạo 7 người, có đèn chiếu sáng'),
(N'Sân cầu lông Victory', 2, 2, N'45 Nguyễn Huệ',    N'Quận 3',     N'TP.HCM', 80000,  120000, N'4 sân thi đấu chuẩn, sàn gỗ'),
(N'Sân tennis Sao Mai',   3, 2, N'78 Trần Hưng Đạo', N'Cầu Giấy',   N'Hà Nội', 200000, 300000, N'Sân cứng ngoài trời, có mái che'),
(N'Sân bóng rổ Phoenix',  4, 2, N'12 Hai Bà Trưng',  N'Hoàn Kiếm',  N'Hà Nội', 150000, 220000, N'Sân trong nhà, điều hòa');

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

INSERT INTO Promotions (Code, Description, DiscountPercent, MaxDiscount, StartDate, EndDate, Quantity) VALUES
(N'SUMMER26', N'Giảm 20% mùa hè',            20, 100000, '2026-06-01', '2026-08-31', 100),
(N'NEWBIE10', N'Giảm 10% khách hàng mới',    10, 50000,  '2026-01-01', '2026-12-31', 500);
GO

PRINT N'Tao database SportsFieldBookingDB thanh cong!';
