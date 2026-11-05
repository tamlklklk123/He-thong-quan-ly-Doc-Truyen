USE WebDocTruyenDb;
GO
IF COL_LENGTH('dbo.Users','LoyaltyPoints') IS NULL ALTER TABLE dbo.Users ADD LoyaltyPoints INT NOT NULL CONSTRAINT DF_Users_LoyaltyPoints DEFAULT(0);
IF COL_LENGTH('dbo.Users','ChapterTickets') IS NULL ALTER TABLE dbo.Users ADD ChapterTickets INT NOT NULL CONSTRAINT DF_Users_ChapterTickets DEFAULT(0);
GO
IF OBJECT_ID('dbo.ChapterReadCredits','U') IS NULL
BEGIN
CREATE TABLE dbo.ChapterReadCredits(Id INT IDENTITY(1,1) PRIMARY KEY, UserId INT NOT NULL REFERENCES dbo.Users(Id), ChapterId INT NOT NULL REFERENCES dbo.Chapters(Id), PointsAwarded INT NOT NULL DEFAULT(10), FirstReadAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
CREATE UNIQUE INDEX UX_ChapterReadCredits ON dbo.ChapterReadCredits(UserId,ChapterId);
END;
IF OBJECT_ID('dbo.CoinChapterUnlocks','U') IS NULL
BEGIN
CREATE TABLE dbo.CoinChapterUnlocks(Id INT IDENTITY(1,1) PRIMARY KEY, UserId INT NOT NULL REFERENCES dbo.Users(Id), ChapterId INT NOT NULL REFERENCES dbo.Chapters(Id), CoinsSpent INT NOT NULL, UsedTicket BIT NOT NULL DEFAULT(0), UnlockedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
CREATE UNIQUE INDEX UX_CoinChapterUnlocks ON dbo.CoinChapterUnlocks(UserId,ChapterId);
END;
IF OBJECT_ID('dbo.CoinLedgerEntries','U') IS NULL
BEGIN
CREATE TABLE dbo.CoinLedgerEntries(Id INT IDENTITY(1,1) PRIMARY KEY, UserId INT NOT NULL REFERENCES dbo.Users(Id), Delta INT NOT NULL, BalanceAfter INT NOT NULL, Reason NVARCHAR(250) NOT NULL, Reference NVARCHAR(150) NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
CREATE INDEX IX_CoinLedgerEntries_UserId_CreatedAt ON dbo.CoinLedgerEntries(UserId,CreatedAt);
END;
GO
