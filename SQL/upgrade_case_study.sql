-- MêTruyệnPlus: áp dụng một lần cho WebDocTruyenDb ĐÃ TỒN TẠI.
-- Chạy trên SQL Server Management Studio trước khi mở lại Backend/Frontend.
-- BACKUP database trước khi chạy. Không DROP TABLE, không xóa dữ liệu.
USE [WebDocTruyenDb];
GO
-- Sửa lỗi "String or binary data would be truncated" khi lưu nhiều URL ảnh.
IF COL_LENGTH('dbo.Chapters','ImageUrlsJson') IS NULL
    ALTER TABLE dbo.Chapters ADD ImageUrlsJson nvarchar(max) NULL;
ELSE
    ALTER TABLE dbo.Chapters ALTER COLUMN ImageUrlsJson nvarchar(max) NULL;
GO
IF COL_LENGTH('dbo.Stories','AccessType') IS NULL
    ALTER TABLE dbo.Stories ADD AccessType nvarchar(10) NOT NULL CONSTRAINT DF_Stories_AccessType DEFAULT ('free');
IF COL_LENGTH('dbo.Stories','Price') IS NULL
    ALTER TABLE dbo.Stories ADD Price decimal(18,2) NOT NULL CONSTRAINT DF_Stories_Price DEFAULT (0);
IF COL_LENGTH('dbo.Stories','AllowSubscription') IS NULL
    ALTER TABLE dbo.Stories ADD AllowSubscription bit NOT NULL CONSTRAINT DF_Stories_AllowSubscription DEFAULT (1);
IF COL_LENGTH('dbo.Chapters','IsFree') IS NULL
    ALTER TABLE dbo.Chapters ADD IsFree bit NOT NULL CONSTRAINT DF_Chapters_IsFree DEFAULT (1);
IF COL_LENGTH('dbo.Chapters','IsPublished') IS NULL
    ALTER TABLE dbo.Chapters ADD IsPublished bit NOT NULL CONSTRAINT DF_Chapters_IsPublished DEFAULT (1);
IF COL_LENGTH('dbo.Chapters','PublishAt') IS NULL
    ALTER TABLE dbo.Chapters ADD PublishAt datetime2 NULL;
GO
IF OBJECT_ID('dbo.SubscriptionPlans','U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionPlans(
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubscriptionPlans PRIMARY KEY,
        Name nvarchar(120) NOT NULL,
        Price decimal(18,2) NOT NULL,
        DurationDays int NOT NULL,
        IsActive bit NOT NULL,
        CoversAllPaidStories bit NOT NULL
    );
END;
GO
IF OBJECT_ID('dbo.PlanStoryAccesses','U') IS NULL
BEGIN
    CREATE TABLE dbo.PlanStoryAccesses(
        PlanId int NOT NULL,
        StoryId int NOT NULL,
        CONSTRAINT PK_PlanStoryAccesses PRIMARY KEY (PlanId, StoryId),
        CONSTRAINT FK_PlanStoryAccesses_SubscriptionPlans_PlanId FOREIGN KEY (PlanId) REFERENCES dbo.SubscriptionPlans(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PlanStoryAccesses_Stories_StoryId FOREIGN KEY (StoryId) REFERENCES dbo.Stories(Id)
    );
    CREATE INDEX IX_PlanStoryAccesses_StoryId ON dbo.PlanStoryAccesses(StoryId);
END;
GO
IF OBJECT_ID('dbo.PaymentTransactions','U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentTransactions(
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentTransactions PRIMARY KEY,
        UserId int NOT NULL,
        OrderCode nvarchar(48) NOT NULL,
        Type nvarchar(12) NOT NULL,
        PlanId int NULL,
        StoryId int NULL,
        Amount decimal(18,2) NOT NULL,
        Status nvarchar(12) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        PaidAt datetime2 NULL,
        ConfirmedByAdminId int NULL,
        AdminNote nvarchar(500) NULL,
        CONSTRAINT FK_PaymentTransactions_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_PaymentTransactions_Amount CHECK (Amount >= 0),
        CONSTRAINT CK_PaymentTransactions_Status CHECK (Status IN ('pending','paid','failed')),
        CONSTRAINT CK_PaymentTransactions_Type CHECK (Type IN ('plan','story','coins'))
    );
    CREATE UNIQUE INDEX IX_PaymentTransactions_OrderCode ON dbo.PaymentTransactions(OrderCode);
    CREATE INDEX IX_PaymentTransactions_UserId_Status ON dbo.PaymentTransactions(UserId,Status);
END;
GO
-- Nâng cấp constraint cho database đã có bảng PaymentTransactions trước đây.
-- Với DB mới có constraint đúng, bỏ qua thao tác để tránh khóa bảng không cần thiết.
IF EXISTS (
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
IF OBJECT_ID('dbo.UserSubscriptions','U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSubscriptions(
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserSubscriptions PRIMARY KEY,
        UserId int NOT NULL,
        PlanId int NOT NULL,
        PaymentTransactionId int NOT NULL,
        StartsAt datetime2 NOT NULL,
        ExpiresAt datetime2 NOT NULL,
        CONSTRAINT FK_UserSubscriptions_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_UserSubscriptions_SubscriptionPlans_PlanId FOREIGN KEY (PlanId) REFERENCES dbo.SubscriptionPlans(Id)
    );
    CREATE INDEX IX_UserSubscriptions_PlanId ON dbo.UserSubscriptions(PlanId);
    CREATE INDEX IX_UserSubscriptions_UserId_ExpiresAt ON dbo.UserSubscriptions(UserId,ExpiresAt);
END;
GO
IF OBJECT_ID('dbo.StoryPurchases','U') IS NULL
BEGIN
    CREATE TABLE dbo.StoryPurchases(
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StoryPurchases PRIMARY KEY,
        UserId int NOT NULL,
        StoryId int NOT NULL,
        PaymentTransactionId int NOT NULL,
        PurchasedAt datetime2 NOT NULL,
        CONSTRAINT FK_StoryPurchases_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT FK_StoryPurchases_Stories_StoryId FOREIGN KEY (StoryId) REFERENCES dbo.Stories(Id)
    );
    CREATE UNIQUE INDEX IX_StoryPurchases_UserId_StoryId ON dbo.StoryPurchases(UserId,StoryId);
    CREATE INDEX IX_StoryPurchases_StoryId ON dbo.StoryPurchases(StoryId);
END;
GO
-- Tương thích bản bổ sung chức năng đánh giá cũ (file SQL/add_story_ratings.sql).
IF OBJECT_ID('dbo.StoryRatings','U') IS NULL
BEGIN
    CREATE TABLE dbo.StoryRatings(
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StoryRatings PRIMARY KEY,
        StoryId int NOT NULL,
        UserId int NOT NULL,
        Score int NOT NULL,
        Review nvarchar(1500) NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT FK_StoryRatings_Stories_StoryId FOREIGN KEY (StoryId) REFERENCES dbo.Stories(Id) ON DELETE CASCADE,
        CONSTRAINT FK_StoryRatings_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_StoryRatings_Score CHECK (Score BETWEEN 1 AND 5)
    );
    CREATE UNIQUE INDEX IX_StoryRatings_UserId_StoryId ON dbo.StoryRatings(UserId, StoryId);
    CREATE INDEX IX_StoryRatings_StoryId ON dbo.StoryRatings(StoryId);
END;
GO
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans)
    INSERT INTO dbo.SubscriptionPlans(Name,Price,DurationDays,IsActive,CoversAllPaidStories)
    VALUES (N'Gói đọc 30 ngày',49000,30,1,1);
GO
PRINT N'Đã hoàn tất nâng cấp schema. Không xóa dữ liệu cũ.';
GO
