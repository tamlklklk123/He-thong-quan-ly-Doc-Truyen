-- MêTruyệnPlus - Quản lý thảo luận / thành viên
-- BACKUP WebDocTruyenDb trước khi chạy. Có thể chạy lại an toàn (idempotent).
-- ÁP DỤNG sau các SQL nâng cấp Xu / Điểm / VIP trước đó.
USE [WebDocTruyenDb];
GO
IF COL_LENGTH('dbo.Users','StatusUntil') IS NULL
    ALTER TABLE dbo.Users ADD StatusUntil DATETIME2 NULL;
IF COL_LENGTH('dbo.Users','ModerationReason') IS NULL
    ALTER TABLE dbo.Users ADD ModerationReason NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Users','StatusChangedAt') IS NULL
    ALTER TABLE dbo.Users ADD StatusChangedAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Users','StatusChangedByAdminId') IS NULL
    ALTER TABLE dbo.Users ADD StatusChangedByAdminId INT NULL;
GO
-- Legacy CHECK (if any) only accepted active/banned: allow restricted/suspended too.
-- Do not remove any other table constraints or any existing data.
DECLARE @oldConstraint SYSNAME;
SELECT TOP 1 @oldConstraint = cc.name FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.Users')
  AND cc.name IN ('CK_Users_Status', 'CK_User_Status');
IF @oldConstraint IS NOT NULL
    EXEC(N'ALTER TABLE dbo.Users DROP CONSTRAINT ' + QUOTENAME(@oldConstraint));
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = 'CK_Users_ModerationStatus' AND parent_object_id = OBJECT_ID('dbo.Users'))
BEGIN
    ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT CK_Users_ModerationStatus
    CHECK ([Status] IN ('active', 'restricted', 'suspended', 'banned'));
END;
GO
IF COL_LENGTH('dbo.Comments','IsHidden') IS NULL
    ALTER TABLE dbo.Comments ADD IsHidden BIT NOT NULL CONSTRAINT DF_Comments_IsHidden DEFAULT (0);
IF COL_LENGTH('dbo.Comments','HiddenReason') IS NULL
    ALTER TABLE dbo.Comments ADD HiddenReason NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Comments','HiddenAt') IS NULL
    ALTER TABLE dbo.Comments ADD HiddenAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Comments','HiddenByAdminId') IS NULL
    ALTER TABLE dbo.Comments ADD HiddenByAdminId INT NULL;
GO
IF COL_LENGTH('dbo.Reports','ResolvedByAdminId') IS NULL
    ALTER TABLE dbo.Reports ADD ResolvedByAdminId INT NULL;
IF COL_LENGTH('dbo.Reports','ResolvedAt') IS NULL
    ALTER TABLE dbo.Reports ADD ResolvedAt DATETIME2 NULL;
GO
IF OBJECT_ID('dbo.ModerationLogs','U') IS NULL
BEGIN
    CREATE TABLE dbo.ModerationLogs
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ModerationLogs PRIMARY KEY,
        AdminId INT NOT NULL,
        TargetType NVARCHAR(30) NOT NULL,
        TargetId INT NOT NULL,
        [Action] NVARCHAR(40) NOT NULL,
        Reason NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ModerationLogs_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ModerationLogs_TargetType_TargetId_CreatedAt' AND object_id = OBJECT_ID('dbo.ModerationLogs'))
    CREATE INDEX IX_ModerationLogs_TargetType_TargetId_CreatedAt ON dbo.ModerationLogs (TargetType, TargetId, CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Comments_IsHidden_CreatedAt' AND object_id = OBJECT_ID('dbo.Comments'))
    CREATE INDEX IX_Comments_IsHidden_CreatedAt ON dbo.Comments (IsHidden, CreatedAt DESC);
GO
PRINT N'Hoàn tất: kiểm duyệt bình luận, báo cáo và trạng thái thành viên.';
