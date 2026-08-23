-- Chay tren database SportsFieldBookingDB DA CO SAN de them bang Notifications
-- (khong can chay lai script tao DB day du - khong mat du lieu).
USE SportsFieldBookingDB;
GO

IF OBJECT_ID('dbo.Notifications', 'U') IS NULL
BEGIN
    CREATE TABLE Notifications (
        NotificationId INT IDENTITY(1,1) PRIMARY KEY,
        UserId    INT NOT NULL REFERENCES Users(UserId) ON DELETE CASCADE,  -- nguoi NHAN
        Title     NVARCHAR(200) NOT NULL,
        Message   NVARCHAR(500) NOT NULL,
        Url       NVARCHAR(300) NULL,           -- link mo khi bam vao thong bao
        IsRead    BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_Notifications_User ON Notifications(UserId, IsRead);
    PRINT N'Da tao bang Notifications.';
END
ELSE
    PRINT N'Bang Notifications da ton tai - khong lam gi.';
GO
