/*
    StudySpace - complete SQL Server database script

    Cach chay:
    1. Mo SQL Server Management Studio hoac Azure Data Studio.
    2. Ket noi bang Windows Authentication hoac tai khoan co quyen CREATE DATABASE.
    3. Execute toan bo file nay. Khong can bat SQLCMD Mode.

    Script co the chay lai, khong xoa du lieu dang co.	
    Tai khoan admin mac dinh: admin / Admin@123

    Enum values:
    - Users.Role: 0 = Student, 1 = Lecturer, 2 = Admin
    - LecturerCourses.AccessLevel: 0 = Lecturer, 1 = CourseHead
    - Documents.Status: 0 = Processing, 1 = Completed, 2 = Failed
    - ChatMessages.Role: 0 = User, 1 = Assistant
*/ 

USE [master];
GO

IF DB_ID(N'StudentChatBoxDb') IS NULL
BEGIN
    CREATE DATABASE [StudentChatBoxDb];
END;
GO

USE [StudentChatBoxDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* =========================
   Core tables
   ========================= */

IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_Users_Id] DEFAULT (NEWID()),
        [Code] nvarchar(30) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL
            CONSTRAINT [DF_Users_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Courses]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Courses]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_Courses_Id] DEFAULT (NEWID()),
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Credits] int NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[LecturerCourses]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LecturerCourses]
    (
        [LecturerId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [AccessLevel] int NOT NULL
            CONSTRAINT [DF_LecturerCourses_AccessLevel] DEFAULT (0),
        CONSTRAINT [PK_LecturerCourses]
            PRIMARY KEY ([LecturerId], [CourseId]),
        CONSTRAINT [FK_LecturerCourses_Users_LecturerId]
            FOREIGN KEY ([LecturerId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LecturerCourses_Courses_CourseId]
            FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[Documents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Documents]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_Documents_Id] DEFAULT (NEWID()),
        [CourseId] uniqueidentifier NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [StoragePath] nvarchar(500) NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [Status] int NOT NULL
            CONSTRAINT [DF_Documents_Status] DEFAULT (0),
        [FailureReason] nvarchar(1000) NULL,
        [UploadedAtUtc] datetime2 NOT NULL
            CONSTRAINT [DF_Documents_UploadedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Documents_Courses_CourseId]
            FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Documents_Users_UploadedById]
            FOREIGN KEY ([UploadedById]) REFERENCES [dbo].[Users] ([Id])
    );
END;
GO

IF OBJECT_ID(N'[dbo].[DocumentChunks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DocumentChunks]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_DocumentChunks_Id] DEFAULT (NEWID()),
        [DocumentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [PageNumber] int NOT NULL,
        [ChunkNumber] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [VectorJson] nvarchar(max) NOT NULL
            CONSTRAINT [DF_DocumentChunks_VectorJson] DEFAULT (N'[]'),
        CONSTRAINT [PK_DocumentChunks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentChunks_Documents_DocumentId]
            FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[ChatSessions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatSessions]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_ChatSessions_Id] DEFAULT (NEWID()),
        -- StudentId stores the current chat user (student or lecturer).
        [StudentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL
            CONSTRAINT [DF_ChatSessions_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ChatSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatSessions_Users_StudentId]
            FOREIGN KEY ([StudentId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_ChatSessions_Courses_CourseId]
            FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages]
    (
        [Id] uniqueidentifier NOT NULL
            CONSTRAINT [DF_ChatMessages_Id] DEFAULT (NEWID()),
        [SessionId] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [Role] int NOT NULL,
        [DocumentId] uniqueidentifier NULL,
        [Content] nvarchar(max) NOT NULL,
        [CitationsJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL
            CONSTRAINT [DF_ChatMessages_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_ChatSessions_SessionId]
            FOREIGN KEY ([SessionId]) REFERENCES [dbo].[ChatSessions] ([Id]) ON DELETE CASCADE
    );
END;
GO

/* =========================
   Upgrades for older schemas
   ========================= */

IF COL_LENGTH(N'dbo.LecturerCourses', N'AccessLevel') IS NULL
BEGIN
    -- Old assignments represented course heads, so existing rows receive value 1.
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[LecturerCourses]
            ADD [AccessLevel] int NOT NULL
                CONSTRAINT [DF_LecturerCourses_AccessLevel] DEFAULT (1);';
END;
GO

IF COL_LENGTH(N'dbo.ChatMessages', N'DocumentId') IS NULL
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[ChatMessages]
            ADD [DocumentId] uniqueidentifier NULL;';
END;
GO

IF COL_LENGTH(N'dbo.ChatMessages', N'ConversationId') IS NULL
BEGIN
    -- Preserve old history as one conversation per session and document.
    EXEC sys.sp_executesql N'
        ALTER TABLE [dbo].[ChatMessages]
            ADD [ConversationId] uniqueidentifier NULL;

        SELECT
            [SessionId],
            ISNULL([DocumentId], CAST(''00000000-0000-0000-0000-000000000000'' AS uniqueidentifier)) AS [DocumentKey],
            NEWID() AS [ConversationId]
        INTO [#ConversationMap]
        FROM [dbo].[ChatMessages]
        GROUP BY
            [SessionId],
            ISNULL([DocumentId], CAST(''00000000-0000-0000-0000-000000000000'' AS uniqueidentifier));

        UPDATE [message]
        SET [message].[ConversationId] = [map].[ConversationId]
        FROM [dbo].[ChatMessages] AS [message]
        INNER JOIN [#ConversationMap] AS [map]
            ON [map].[SessionId] = [message].[SessionId]
           AND [map].[DocumentKey] = ISNULL(
                [message].[DocumentId],
                CAST(''00000000-0000-0000-0000-000000000000'' AS uniqueidentifier));

        DROP TABLE [#ConversationMap];

        ALTER TABLE [dbo].[ChatMessages]
            ALTER COLUMN [ConversationId] uniqueidentifier NOT NULL;';
END;
GO

/* =========================
   Indexes
   ========================= */

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[Users]')
      AND [name] = N'IX_Users_Code'
)
    CREATE UNIQUE INDEX [IX_Users_Code] ON [dbo].[Users] ([Code]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[Users]')
      AND [name] = N'IX_Users_Email'
)
    CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[Courses]')
      AND [name] = N'IX_Courses_Code'
)
    CREATE UNIQUE INDEX [IX_Courses_Code] ON [dbo].[Courses] ([Code]);
GO

-- Replace the old one-lecturer-per-course unique index.
IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[LecturerCourses]')
      AND [name] = N'IX_LecturerCourses_CourseId'
      AND [is_unique] = 1
)
BEGIN
    DROP INDEX [IX_LecturerCourses_CourseId] ON [dbo].[LecturerCourses];
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[LecturerCourses]')
      AND [name] = N'IX_LecturerCourses_CourseId'
)
    CREATE INDEX [IX_LecturerCourses_CourseId]
        ON [dbo].[LecturerCourses] ([CourseId]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[LecturerCourses]')
      AND [name] = N'IX_LecturerCourses_CourseId_AccessLevel'
)
BEGIN
    EXEC sys.sp_executesql N'
        CREATE UNIQUE INDEX [IX_LecturerCourses_CourseId_AccessLevel]
        ON [dbo].[LecturerCourses] ([CourseId], [AccessLevel])
        WHERE [AccessLevel] = 1;';
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[Documents]')
      AND [name] = N'IX_Documents_CourseId_Sha256'
)
    CREATE UNIQUE INDEX [IX_Documents_CourseId_Sha256]
        ON [dbo].[Documents] ([CourseId], [Sha256]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[Documents]')
      AND [name] = N'IX_Documents_UploadedById'
)
    CREATE INDEX [IX_Documents_UploadedById]
        ON [dbo].[Documents] ([UploadedById]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[DocumentChunks]')
      AND [name] = N'IX_DocumentChunks_DocumentId'
)
    CREATE INDEX [IX_DocumentChunks_DocumentId]
        ON [dbo].[DocumentChunks] ([DocumentId]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[ChatSessions]')
      AND [name] = N'IX_ChatSessions_StudentId_CourseId'
)
    CREATE UNIQUE INDEX [IX_ChatSessions_StudentId_CourseId]
        ON [dbo].[ChatSessions] ([StudentId], [CourseId]);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[ChatSessions]')
      AND [name] = N'IX_ChatSessions_CourseId'
)
    CREATE INDEX [IX_ChatSessions_CourseId]
        ON [dbo].[ChatSessions] ([CourseId]);
GO

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[ChatMessages]')
      AND [name] = N'IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc'
)
    DROP INDEX [IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc]
        ON [dbo].[ChatMessages];
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[ChatMessages]')
      AND [name] = N'IX_ChatMessages_SessionId_ConversationId_CreatedAtUtc'
)
BEGIN
    EXEC sys.sp_executesql N'
        CREATE INDEX [IX_ChatMessages_SessionId_ConversationId_CreatedAtUtc]
        ON [dbo].[ChatMessages] ([SessionId], [ConversationId], [CreatedAtUtc]);';
END;
GO

/* =========================
   EF Core migration history
   ========================= */

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory]
    (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621074357_InitialCreate'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621074357_InitialCreate', N'8.0.0');
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621152114_AddChatConversations'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621152114_AddChatConversations', N'8.0.0');
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260621084415_AddLecturerCourseAccessLevel'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260621084415_AddLecturerCourseAccessLevel', N'8.0.0');
END;
GO

/* =========================
   Seed data
   ========================= */

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Role] = 2)
BEGIN
    INSERT INTO [dbo].[Users]
    (
        [Id], [Code], [FullName], [Email], [PasswordHash], [Role], [CreatedAtUtc]
    )
    VALUES
    (
        NEWID(),
        N'admin',
        N'Quản trị hệ thống',
        N'admin@chatbox.local',
        N'100000.lJuKDY+VlTf45NOxBRNoXQ==.XAB2yZcw2xWUZicI2dg7+3Zw6Efy5oWpWSWfAn5JTNg=',
        2,
        SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Courses] WHERE [Code] = N'PRN222')
BEGIN
    INSERT INTO [dbo].[Courses]
    (
        [Id], [Code], [Name], [Credits], [Description]
    )
    VALUES
    (
        NEWID(),
        N'PRN222',
        N'Advanced Programming with .NET',
        3,
        N'Không gian tài liệu và hỏi đáp mẫu cho môn PRN222.'
    );
END;
GO

/* =========================
   Final verification
   ========================= */

SELECT
    DB_NAME() AS [DatabaseName],
    (SELECT COUNT(*) FROM sys.tables WHERE [is_ms_shipped] = 0) AS [TableCount],
    (SELECT COUNT(*) FROM [dbo].[Users] WHERE [Role] = 2) AS [AdminCount],
    COL_LENGTH(N'dbo.LecturerCourses', N'AccessLevel') AS [AccessLevelColumnSize],
    N'Database is ready. Default login: admin / Admin@123' AS [Result];
GO
