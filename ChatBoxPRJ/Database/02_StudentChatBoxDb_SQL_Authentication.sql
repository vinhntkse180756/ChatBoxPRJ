/*
    StudySpace - SQL Server Authentication setup

    Chạy toàn bộ file này trong SSMS bằng tài khoản sysadmin.
    SQL Server phải bật chế độ "SQL Server and Windows Authentication mode".

    Tài khoản ứng dụng được tạo bởi script:
      Login:    StudySpaceApp
      Password: StudySpace@2026!

    Connection string mẫu:
      Server=localhost;Database=StudentChatBoxDb;User Id=StudySpaceApp;Password=StudySpace@2026!;TrustServerCertificate=True;MultipleActiveResultSets=True

    Có thể chạy lại script mà không xóa dữ liệu hiện có.
*/

USE [master];
GO

IF DB_ID(N'StudentChatBoxDb') IS NULL
    CREATE DATABASE [StudentChatBoxDb];
GO

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE [name] = N'StudySpaceApp')
BEGIN
    CREATE LOGIN [StudySpaceApp]
        WITH PASSWORD = N'StudySpace@2026!',
             CHECK_POLICY = ON,
             CHECK_EXPIRATION = OFF;
END;
GO

USE [StudentChatBoxDb];
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE [name] = N'StudySpaceApp')
    CREATE USER [StudySpaceApp] FOR LOGIN [StudySpaceApp];
GO

-- Ứng dụng hiện tự cập nhật một số index khi khởi động nên cần quyền db_owner.
IF IS_ROLEMEMBER(N'db_owner', N'StudySpaceApp') <> 1
    ALTER ROLE [db_owner] ADD MEMBER [StudySpaceApp];
GO

IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Users]
    (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Email] nvarchar(160) NOT NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Users_Code] ON [dbo].[Users] ([Code]);
    CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
END;
GO

IF OBJECT_ID(N'[dbo].[Courses]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Courses]
    (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Credits] int NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Courses_Code] ON [dbo].[Courses] ([Code]);
END;
GO

IF OBJECT_ID(N'[dbo].[LecturerCourses]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LecturerCourses]
    (
        [LecturerId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_LecturerCourses] PRIMARY KEY ([LecturerId], [CourseId]),
        CONSTRAINT [FK_LecturerCourses_Users_LecturerId]
            FOREIGN KEY ([LecturerId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_LecturerCourses_Courses_CourseId]
            FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_LecturerCourses_CourseId] ON [dbo].[LecturerCourses] ([CourseId]);
END;
GO

IF OBJECT_ID(N'[dbo].[Documents]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Documents]
    (
        [Id] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [UploadedById] uniqueidentifier NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [StoragePath] nvarchar(500) NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [Status] int NOT NULL,
        [FailureReason] nvarchar(1000) NULL,
        [UploadedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Documents_Courses_CourseId]
            FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Documents_Users_UploadedById]
            FOREIGN KEY ([UploadedById]) REFERENCES [dbo].[Users] ([Id])
    );
    CREATE UNIQUE INDEX [IX_Documents_CourseId_Sha256]
        ON [dbo].[Documents] ([CourseId], [Sha256]);
    CREATE INDEX [IX_Documents_UploadedById] ON [dbo].[Documents] ([UploadedById]);
END;
GO

IF OBJECT_ID(N'[dbo].[DocumentChunks]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DocumentChunks]
    (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [PageNumber] int NOT NULL,
        [ChunkNumber] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [VectorJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_DocumentChunks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentChunks_Documents_DocumentId]
            FOREIGN KEY ([DocumentId]) REFERENCES [dbo].[Documents] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_DocumentChunks_DocumentId] ON [dbo].[DocumentChunks] ([DocumentId]);
    CREATE INDEX [IX_DocumentChunks_CourseId] ON [dbo].[DocumentChunks] ([CourseId]);
END;
GO

IF OBJECT_ID(N'[dbo].[ChatSessions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatSessions]
    (
        [Id] uniqueidentifier NOT NULL,
        [StudentId] uniqueidentifier NOT NULL,
        [CourseId] uniqueidentifier NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatSessions_Users_StudentId]
            FOREIGN KEY ([StudentId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_ChatSessions_Courses_CourseId]
            FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_ChatSessions_StudentId_CourseId]
        ON [dbo].[ChatSessions] ([StudentId], [CourseId]);
    CREATE INDEX [IX_ChatSessions_CourseId] ON [dbo].[ChatSessions] ([CourseId]);
END;
GO

IF OBJECT_ID(N'[dbo].[ChatMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ChatMessages]
    (
        [Id] uniqueidentifier NOT NULL,
        [SessionId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NULL,
        [Role] int NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [CitationsJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_ChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChatMessages_ChatSessions_SessionId]
            FOREIGN KEY ([SessionId]) REFERENCES [dbo].[ChatSessions] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ChatMessages_SessionId] ON [dbo].[ChatMessages] ([SessionId]);
    CREATE INDEX [IX_ChatMessages_SessionId_DocumentId_CreatedAtUtc]
        ON [dbo].[ChatMessages] ([SessionId], [DocumentId], [CreatedAtUtc]);
END;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Courses] WHERE [Code] = N'PRN222')
BEGIN
    INSERT INTO [dbo].[Courses] ([Id], [Code], [Name], [Credits], [Description])
    VALUES
    (
        NEWID(), N'PRN222', N'Advanced Programming with .NET', 3,
        N'Không gian tài liệu và hỏi đáp mẫu cho môn PRN222.'
    );
END;
GO

SELECT
    N'StudentChatBoxDb đã sẵn sàng.' AS [Result],
    N'StudySpaceApp' AS [LoginName],
    N'Server=localhost;Database=StudentChatBoxDb;User Id=StudySpaceApp;Password=StudySpace@2026!;TrustServerCertificate=True;MultipleActiveResultSets=True' AS [ConnectionString];
GO
