-- MêTruyệnPlus: chạy trong SQL Server Management Studio trên database WebDocTruyenDb
-- Chỉ cần chạy một lần đối với DB cũ; không xóa bảng hoặc dữ liệu hiện có.
USE [WebDocTruyenDb];
GO
IF COL_LENGTH(N'dbo.Chapters', N'ImageUrlsJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[Chapters] ADD [ImageUrlsJson] nvarchar(max) NULL;
END;
GO
IF OBJECT_ID(N'dbo.StoryRatings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[StoryRatings] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [StoryId] int NOT NULL,
        [UserId] int NOT NULL,
        [Score] int NOT NULL,
        [Review] nvarchar(1500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StoryRatings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StoryRatings_Score] CHECK ([Score] BETWEEN 1 AND 5),
        CONSTRAINT [FK_StoryRatings_Stories_StoryId]
            FOREIGN KEY ([StoryId]) REFERENCES [dbo].[Stories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StoryRatings_Users_UserId]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_StoryRatings_UserId_StoryId]
        ON [dbo].[StoryRatings] ([UserId], [StoryId]);
    CREATE INDEX [IX_StoryRatings_StoryId]
        ON [dbo].[StoryRatings] ([StoryId]);
END;
GO
