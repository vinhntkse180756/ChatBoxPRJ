using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using DataAcessLayer.Persistence;

#nullable disable

namespace DataAcessLayer.Migrations
{
    /// <summary>
    /// DB đã có Packages/Payments/UserSubscriptions (migration 20260712112441).
    /// Migration này chỉ bổ sung cột token/VNPay, không tạo lại bảng.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260717100000_AddSubscriptionsAndPayments")]
    public partial class AddSubscriptionsAndPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tự động tạo các bảng nếu chưa tồn tại (tránh lỗi khi chạy trên DB mới hoàn toàn do thiếu file migration trung gian)
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Packages]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Packages] (
                        [Id] uniqueidentifier NOT NULL,
                        [Code] nvarchar(30) NOT NULL,
                        [Name] nvarchar(120) NOT NULL,
                        [Description] nvarchar(1000) NOT NULL,
                        [PriceVnd] decimal(18,2) NOT NULL,
                        [DailyTokenLimit] int NOT NULL DEFAULT 20000,
                        [MaxQuestionChars] int NOT NULL DEFAULT 2000,
                        [DurationDays] int NOT NULL DEFAULT 0,
                        [ChatQuestionsPerDay] int NOT NULL,
                        [SortOrder] int NOT NULL,
                        [IsActive] bit NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_Packages] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_Packages_Code] ON [dbo].[Packages] ([Code]);
                END;

                IF OBJECT_ID(N'[dbo].[Payments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Payments] (
                        [Id] uniqueidentifier NOT NULL,
                        [OrderCode] nvarchar(40) NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [PackageId] uniqueidentifier NOT NULL,
                        [AmountVnd] decimal(18,2) NOT NULL,
                        [Currency] nvarchar(10) NOT NULL,
                        [Status] int NOT NULL,
                        [Provider] nvarchar(30) NOT NULL,
                        [ProviderReference] nvarchar(100) NULL,
                        [ProviderResponseCode] nvarchar(10) NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [PaidAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_Payments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]),
                        CONSTRAINT [FK_Payments_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[Packages] ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_Payments_OrderCode] ON [dbo].[Payments] ([OrderCode]);
                    CREATE INDEX [IX_Payments_PackageId] ON [dbo].[Payments] ([PackageId]);
                    CREATE INDEX [IX_Payments_UserId_Status] ON [dbo].[Payments] ([UserId], [Status]);
                END;

                IF OBJECT_ID(N'[dbo].[UserSubscriptions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[UserSubscriptions] (
                        [Id] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        [PackageId] uniqueidentifier NOT NULL,
                        [PaymentId] uniqueidentifier NULL,
                        [Status] int NOT NULL,
                        [StartedAtUtc] datetime2 NOT NULL,
                        [ExpiresAtUtc] datetime2 NULL,
                        CONSTRAINT [PK_UserSubscriptions] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_UserSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_UserSubscriptions_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[Packages] ([Id]),
                        CONSTRAINT [FK_UserSubscriptions_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [dbo].[Payments] ([Id]) ON DELETE SET NULL
                    );
                    CREATE INDEX [IX_UserSubscriptions_PackageId] ON [dbo].[UserSubscriptions] ([PackageId]);
                    CREATE INDEX [IX_UserSubscriptions_PaymentId] ON [dbo].[UserSubscriptions] ([PaymentId]);
                    CREATE INDEX [IX_UserSubscriptions_UserId_Status] ON [dbo].[UserSubscriptions] ([UserId], [Status]);
                END;
            """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.Packages', N'DailyTokenLimit') IS NULL
                    ALTER TABLE [Packages] ADD [DailyTokenLimit] int NOT NULL CONSTRAINT [DF_Packages_DailyTokenLimit] DEFAULT 20000;
                IF COL_LENGTH(N'dbo.Packages', N'MaxQuestionChars') IS NULL
                    ALTER TABLE [Packages] ADD [MaxQuestionChars] int NOT NULL CONSTRAINT [DF_Packages_MaxQuestionChars] DEFAULT 2000;
                IF COL_LENGTH(N'dbo.Packages', N'DurationDays') IS NULL
                    ALTER TABLE [Packages] ADD [DurationDays] int NOT NULL CONSTRAINT [DF_Packages_DurationDays] DEFAULT 0;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.Payments', N'OrderCode') IS NULL
                    ALTER TABLE [Payments] ADD [OrderCode] nvarchar(40) NULL;
                IF COL_LENGTH(N'dbo.Payments', N'ProviderResponseCode') IS NULL
                    ALTER TABLE [Payments] ADD [ProviderResponseCode] nvarchar(10) NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [Payments]
                SET [OrderCode] = REPLACE(LOWER(CONVERT(nvarchar(36), [Id])), N'-', N'')
                WHERE [OrderCode] IS NULL OR LTRIM(RTRIM([OrderCode])) = N'';
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.Payments', N'OrderCode') IS NOT NULL
                BEGIN
                    ALTER TABLE [Payments] ALTER COLUMN [OrderCode] nvarchar(40) NOT NULL;
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = N'IX_Payments_OrderCode' AND object_id = OBJECT_ID(N'dbo.Payments'))
                        CREATE UNIQUE INDEX [IX_Payments_OrderCode] ON [Payments]([OrderCode]);
                END
                """);

            // Đồng bộ 3 gói Free / Pro / Pre theo yêu cầu mới.
            migrationBuilder.Sql("""
                UPDATE [Packages] SET
                    [Name] = N'Free',
                    [Description] = N'Gói miễn phí — đủ dùng để trải nghiệm hỏi đáp từ tài liệu môn học.',
                    [PriceVnd] = 0,
                    [DailyTokenLimit] = 20000,
                    [MaxQuestionChars] = 2000,
                    [DurationDays] = 0,
                    [ChatQuestionsPerDay] = 20,
                    [SortOrder] = 1,
                    [IsActive] = 1
                WHERE [Code] = N'FREE';

                UPDATE [Packages] SET
                    [Code] = N'PRE',
                    [Name] = N'Pre',
                    [Description] = N'Gói Pre — hạn mức lớn nhất cho nhu cầu học tập chuyên sâu.',
                    [PriceVnd] = 99000,
                    [DailyTokenLimit] = 300000,
                    [MaxQuestionChars] = 8000,
                    [DurationDays] = 30,
                    [ChatQuestionsPerDay] = 1000,
                    [SortOrder] = 3,
                    [IsActive] = 1
                WHERE [Code] = N'PRO' AND [PriceVnd] >= 90000;

                UPDATE [Packages] SET
                    [Code] = N'PRO',
                    [Name] = N'Pro',
                    [Description] = N'Gói Pro — hạn mức token cao hơn, phù hợp học tập thường xuyên.',
                    [PriceVnd] = 49000,
                    [DailyTokenLimit] = 100000,
                    [MaxQuestionChars] = 4000,
                    [DurationDays] = 30,
                    [ChatQuestionsPerDay] = 100,
                    [SortOrder] = 2,
                    [IsActive] = 1
                WHERE [Code] IN (N'BASIC', N'PRO') AND [PriceVnd] BETWEEN 40000 AND 60000;

                UPDATE [Packages] SET [IsActive] = 0
                WHERE [Code] NOT IN (N'FREE', N'PRO', N'PRE');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_OrderCode' AND object_id = OBJECT_ID(N'dbo.Payments'))
                    DROP INDEX [IX_Payments_OrderCode] ON [Payments];
                IF COL_LENGTH(N'dbo.Payments', N'OrderCode') IS NOT NULL
                    ALTER TABLE [Payments] DROP COLUMN [OrderCode];
                IF COL_LENGTH(N'dbo.Payments', N'ProviderResponseCode') IS NOT NULL
                    ALTER TABLE [Payments] DROP COLUMN [ProviderResponseCode];
                IF COL_LENGTH(N'dbo.Packages', N'DailyTokenLimit') IS NOT NULL
                    ALTER TABLE [Packages] DROP CONSTRAINT [DF_Packages_DailyTokenLimit];
                IF COL_LENGTH(N'dbo.Packages', N'DailyTokenLimit') IS NOT NULL
                    ALTER TABLE [Packages] DROP COLUMN [DailyTokenLimit];
                IF COL_LENGTH(N'dbo.Packages', N'MaxQuestionChars') IS NOT NULL
                    ALTER TABLE [Packages] DROP CONSTRAINT [DF_Packages_MaxQuestionChars];
                IF COL_LENGTH(N'dbo.Packages', N'MaxQuestionChars') IS NOT NULL
                    ALTER TABLE [Packages] DROP COLUMN [MaxQuestionChars];
                IF COL_LENGTH(N'dbo.Packages', N'DurationDays') IS NOT NULL
                    ALTER TABLE [Packages] DROP CONSTRAINT [DF_Packages_DurationDays];
                IF COL_LENGTH(N'dbo.Packages', N'DurationDays') IS NOT NULL
                    ALTER TABLE [Packages] DROP COLUMN [DurationDays];
                """);
        }
    }
}
