using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.DataAccess.Migrations;

public sealed class DatabaseMigrationManager(ChatBoxDbContext db)
{
    private const string InitialMigration       = "20260621074357_InitialCreate";
    private const string AccessLevelMigration   = "20260621084415_AddLecturerCourseAccessLevel";
    private const string ConversationsMigration = "20260621152114_AddChatConversations";
    private const string BenchmarksMigration    = "20260711114057_AddBenchmarks";

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        if (await db.Database.CanConnectAsync(ct))
        {
            // Các bản cũ dùng EnsureCreated nên không có lịch sử migration.
            // Ghi nhận baseline chỉ khi schema ứng dụng đã tồn tại, không xóa dữ liệu.
            await db.Database.ExecuteSqlRawAsync($$"""
                IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[__EFMigrationsHistory]
                    (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    );
                    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (N'{{InitialMigration}}', N'8.0.0');
                END;
                """, ct);

            // Nếu index IX_LecturerCourses_CourseId_AccessLevel đã tồn tại (nghĩa là migration
            // AddLecturerCourseAccessLevel đã được áp dụng thủ công hoặc qua EnsureCreated với
            // model mới), ghi nhận nó là đã chạy để tránh lỗi "column already exists".
            await db.Database.ExecuteSqlRawAsync($$"""
                IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
                       WHERE [MigrationId] = N'{{AccessLevelMigration}}')
                   AND EXISTS (
                       SELECT 1 FROM [sys].[indexes] i
                       INNER JOIN [sys].[objects] o ON i.[object_id] = o.[object_id]
                       WHERE o.[name] = N'LecturerCourses'
                         AND i.[name] = N'IX_LecturerCourses_CourseId_AccessLevel')
                BEGIN
                    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (N'{{AccessLevelMigration}}', N'8.0.0');
                END;
                """, ct);

            // Nếu cột ConversationId đã tồn tại trong ChatMessages (DB đã được nâng cấp thủ công
            // hoặc migration này đã chạy rồi restart), ghi nhận để tránh lỗi khi khởi động lại.
            await db.Database.ExecuteSqlRawAsync($$"""
                IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
                       WHERE [MigrationId] = N'{{ConversationsMigration}}')
                   AND EXISTS (
                       SELECT 1 FROM [INFORMATION_SCHEMA].[COLUMNS]
                       WHERE TABLE_NAME = N'ChatMessages' AND COLUMN_NAME = N'ConversationId')
                BEGIN
                    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (N'{{ConversationsMigration}}', N'8.0.0');
                END;
                """, ct);

            // Nếu bảng BenchmarkRuns đã tồn tại, ghi nhận migration AddBenchmarks.
            await db.Database.ExecuteSqlRawAsync($$"""
                IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM [dbo].[__EFMigrationsHistory]
                       WHERE [MigrationId] = N'{{BenchmarksMigration}}')
                   AND OBJECT_ID(N'[dbo].[BenchmarkRuns]', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                    VALUES (N'{{BenchmarksMigration}}', N'8.0.0');
                END;
                """, ct);
        }

        await db.Database.MigrateAsync(ct);
    }
}
