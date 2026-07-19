using BusinessObjects.Entities;
using DataAcessLayer.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer.Migrations;

public sealed class DatabaseSeeder(AppDbContext db)
{
    public async Task SeedAsync(
        string adminCode,
        string adminEmail,
        string adminPasswordHash,
        CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(user => user.Role == UserRole.Admin, ct))
        {
            db.Users.Add(new AppUser
            {
                Code = adminCode,
                FullName = "Quản trị hệ thống",
                Email = adminEmail,
                PasswordHash = adminPasswordHash,
                Role = UserRole.Admin
            });
        }

        if (!await db.Courses.AnyAsync(ct))
        {
            db.Courses.Add(new Course
            {
                Code = "PRN222",
                Name = "Advanced Programming with .NET",
                Credits = 3,
                Description = "Không gian tài liệu và hỏi đáp mẫu cho môn PRN222."
            });
        }

        await SeedPackagesAsync(ct);

        // Đổi từ đếm token sang đếm câu: reset usage cũ (giá trị token rất lớn).
        var inflated = await db.StudentDailyTokenUsages.Where(x => x.TokensUsed > 100).ToListAsync(ct);
        foreach (var row in inflated)
            row.TokensUsed = 0;

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPackagesAsync(CancellationToken ct)
    {
        var legacyPro = await db.SubscriptionPackages
            .FirstOrDefaultAsync(x => x.Code == "PRO" && x.PriceVnd >= 90_000, ct);
        if (legacyPro is not null)
            legacyPro.Code = "PRE";

        var legacyBasic = await db.SubscriptionPackages
            .FirstOrDefaultAsync(x => x.Code == "BASIC" || (x.Code == "PRO" && x.PriceVnd >= 40_000 && x.PriceVnd <= 60_000), ct);
        if (legacyBasic is not null)
            legacyBasic.Code = "PRO";

        await db.SaveChangesAsync(ct);

        await UpsertPackageAsync("FREE", "Free",
            "Gói miễn phí — đủ dùng để trải nghiệm hỏi đáp từ tài liệu môn học.",
            price: 0, questions: 10, maxChars: 500, durationDays: 0, sort: 1, active: true, ct);

        await UpsertPackageAsync("PRO", "Pro",
            "Gói Pro — nhiều câu hỏi hơn mỗi ngày, phù hợp học tập thường xuyên.",
            price: 49_000, questions: 100, maxChars: 1_500, durationDays: 30, sort: 2, active: true, ct);

        // Ngừng bán gói Pre: ẩn khỏi trang nâng cấp và hết hạn subscription đang dùng Pre.
        await UpsertPackageAsync("PRE", "Pre",
            "Gói Pre đã ngừng cung cấp.",
            price: 99_000, questions: 1000, maxChars: 3_000, durationDays: 30, sort: 3, active: false, ct);

        var pre = await db.SubscriptionPackages.FirstOrDefaultAsync(x => x.Code == "PRE", ct);
        if (pre is not null)
        {
            var activePreSubs = await db.UserSubscriptions
                .Where(x => x.PackageId == pre.Id && x.Status == SubscriptionStatus.Active)
                .ToListAsync(ct);
            var now = DateTime.UtcNow;
            foreach (var sub in activePreSubs)
            {
                sub.Status = SubscriptionStatus.Expired;
                sub.EndsAtUtc = now;
            }
        }

        var obsolete = await db.SubscriptionPackages
            .Where(x => x.Code != "FREE" && x.Code != "PRO" && x.IsActive)
            .ToListAsync(ct);
        foreach (var package in obsolete)
            package.IsActive = false;
    }

    private async Task UpsertPackageAsync(
        string code,
        string name,
        string description,
        decimal price,
        int questions,
        int maxChars,
        int durationDays,
        int sort,
        bool active,
        CancellationToken ct)
    {
        var package = await db.SubscriptionPackages.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (package is null)
        {
            db.SubscriptionPackages.Add(new SubscriptionPackage
            {
                Code = code,
                Name = name,
                Description = description,
                PriceVnd = price,
                DailyTokenLimit = questions,
                MaxQuestionChars = maxChars,
                DurationDays = durationDays,
                ChatQuestionsPerDay = questions,
                SortOrder = sort,
                IsActive = active
            });
            return;
        }

        package.Name = name;
        package.Description = description;
        package.PriceVnd = price;
        package.DailyTokenLimit = questions;
        package.MaxQuestionChars = maxChars;
        package.DurationDays = durationDays;
        package.ChatQuestionsPerDay = questions;
        package.SortOrder = sort;
        package.IsActive = active;
    }
}
