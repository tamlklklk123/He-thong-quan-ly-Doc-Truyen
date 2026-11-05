-- Run once on WebDocTruyenDb. Existing data is preserved.
IF COL_LENGTH('dbo.Users','CoinBalance') IS NULL ALTER TABLE dbo.Users ADD CoinBalance INT NOT NULL CONSTRAINT DF_Users_CoinBalance DEFAULT(0);
IF COL_LENGTH('dbo.Users','WeeklyChapterGoal') IS NULL ALTER TABLE dbo.Users ADD WeeklyChapterGoal INT NOT NULL CONSTRAINT DF_Users_WeeklyChapterGoal DEFAULT(24);
IF COL_LENGTH('dbo.Users','ShowBookwormBadge') IS NULL ALTER TABLE dbo.Users ADD ShowBookwormBadge BIT NOT NULL CONSTRAINT DF_Users_ShowBookwormBadge DEFAULT(1);
IF COL_LENGTH('dbo.PaymentTransactions','CoinAmount') IS NULL ALTER TABLE dbo.PaymentTransactions ADD CoinAmount INT NULL;
IF OBJECT_ID('dbo.WeeklyChapterReads','U') IS NULL BEGIN
 CREATE TABLE dbo.WeeklyChapterReads (Id INT IDENTITY(1,1) PRIMARY KEY, UserId INT NOT NULL, ChapterId INT NOT NULL, WeekStartDate DATETIME2 NOT NULL, ReadAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_WCR_User FOREIGN KEY(UserId) REFERENCES dbo.Users(Id), CONSTRAINT FK_WCR_Chapter FOREIGN KEY(ChapterId) REFERENCES dbo.Chapters(Id));
 CREATE UNIQUE INDEX UX_WCR ON dbo.WeeklyChapterReads(UserId,ChapterId,WeekStartDate);
END;
IF OBJECT_ID('dbo.BookwormBadges','U') IS NULL BEGIN
 CREATE TABLE dbo.BookwormBadges (Id INT IDENTITY(1,1) PRIMARY KEY, UserId INT NOT NULL, WeekStartDate DATETIME2 NOT NULL, GoalAtClaim INT NOT NULL, ChaptersRead INT NOT NULL, CoinsAwarded INT NOT NULL DEFAULT(50), AwardedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_BB_User FOREIGN KEY(UserId) REFERENCES dbo.Users(Id));
 CREATE UNIQUE INDEX UX_BB ON dbo.BookwormBadges(UserId,WeekStartDate);
END;


-- FIX: giao dịch nạp Xu dùng Type='coins', cần thêm vào CHECK constraint.
-- Trước bản sửa, cột Type chỉ cho phép 'plan' và 'story'.
GO
IF OBJECT_ID(N'dbo.PaymentTransactions', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1 FROM sys.check_constraints
       WHERE parent_object_id = OBJECT_ID(N'dbo.PaymentTransactions')
         AND name = N'CK_PaymentTransactions_Type'
         AND definition NOT LIKE '%coins%'
   )
BEGIN
    ALTER TABLE dbo.PaymentTransactions DROP CONSTRAINT CK_PaymentTransactions_Type;
    ALTER TABLE dbo.PaymentTransactions WITH CHECK ADD CONSTRAINT CK_PaymentTransactions_Type
        CHECK ([Type] IN ('plan','story','coins'));
END;
GO
