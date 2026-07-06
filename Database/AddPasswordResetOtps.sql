USE SportsFieldBookingDB;
GO

IF OBJECT_ID(N'dbo.PasswordResetOtps', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetOtps (
        PasswordResetOtpId INT IDENTITY(1,1) PRIMARY KEY,
        UserId              INT NOT NULL,
        OtpHash             NVARCHAR(64) NOT NULL,
        ExpiresAt           DATETIME2 NOT NULL,
        AttemptCount        INT NOT NULL
            CONSTRAINT DF_PasswordResetOtps_AttemptCount DEFAULT 0,
        IsUsed              BIT NOT NULL
            CONSTRAINT DF_PasswordResetOtps_IsUsed DEFAULT 0,
        CreatedAt           DATETIME2 NOT NULL
            CONSTRAINT DF_PasswordResetOtps_CreatedAt DEFAULT GETUTCDATE(),
        ResetTokenHash      NVARCHAR(64) NULL,
        ResetTokenExpiresAt DATETIME2 NULL,
        CONSTRAINT FK_PasswordResetOtps_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE INDEX IX_PasswordResetOtps_UserId_CreatedAt
        ON dbo.PasswordResetOtps (UserId, CreatedAt);
END
GO
