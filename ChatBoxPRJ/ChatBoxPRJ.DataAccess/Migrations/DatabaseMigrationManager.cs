using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.DataAccess.Migrations;

public sealed class DatabaseMigrationManager(ChatBoxDbContext db)
{
    private const string InitialMigration = "20260621074357_InitialCreate";

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
        }

        await db.Database.MigrateAsync(ct);
    }
}
