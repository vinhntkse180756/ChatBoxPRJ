SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'[dbo].[LecturerCourses]', N'U') IS NULL
        THROW 50001, N'Khong tim thay bang dbo.LecturerCourses. Hay tao database goc truoc khi chay file nay.', 1;

    -- 0 = Lecturer, 1 = CourseHead. Existing assignments were course heads.
    IF COL_LENGTH(N'dbo.LecturerCourses', N'AccessLevel') IS NULL
    BEGIN
        -- Dynamic SQL forces SQL Server to compile this DDL after the IF check.
        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[LecturerCourses]
                ADD [AccessLevel] int NOT NULL
                    CONSTRAINT [DF_LecturerCourses_AccessLevel] DEFAULT (1);';
    END;

    -- Replace the old one-lecturer-per-course unique index.
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'[dbo].[LecturerCourses]')
          AND [name] = N'IX_LecturerCourses_CourseId'
          AND [is_unique] = 1
    )
    BEGIN
        EXEC sys.sp_executesql N'
            DROP INDEX [IX_LecturerCourses_CourseId]
            ON [dbo].[LecturerCourses];';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'[dbo].[LecturerCourses]')
          AND [name] = N'IX_LecturerCourses_CourseId'
    )
    BEGIN
        EXEC sys.sp_executesql N'
            CREATE INDEX [IX_LecturerCourses_CourseId]
            ON [dbo].[LecturerCourses] ([CourseId]);';
    END;

    -- Multiple lecturers may access a course, but it can have only one course head.
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE [object_id] = OBJECT_ID(N'[dbo].[LecturerCourses]')
          AND [name] = N'IX_LecturerCourses_CourseId_AccessLevel'
    )
    BEGIN
        -- AccessLevel is resolved only after ALTER TABLE has completed.
        EXEC sys.sp_executesql N'
            CREATE UNIQUE INDEX [IX_LecturerCourses_CourseId_AccessLevel]
            ON [dbo].[LecturerCourses] ([CourseId], [AccessLevel])
            WHERE [AccessLevel] = 1;';
    END;

    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[__EFMigrationsHistory]
        (
            [MigrationId] nvarchar(150) NOT NULL,
            [ProductVersion] nvarchar(32) NOT NULL,
            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260621074357_InitialCreate'
    )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260621074357_InitialCreate', N'8.0.0');
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM [dbo].[__EFMigrationsHistory]
        WHERE [MigrationId] = N'20260621084415_AddLecturerCourseAccessLevel'
    )
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260621084415_AddLecturerCourseAccessLevel', N'8.0.0');
    END;

    COMMIT TRANSACTION;
    PRINT N'Cap nhat phan quyen giang vien theo mon hoc thanh cong.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
