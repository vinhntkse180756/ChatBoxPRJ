using ChatBoxPRJ.DataAccess.Models;
using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.DataAccess.Migrations;

public sealed class DatabaseSeeder(ChatBoxDbContext db)
{
    public async Task SeedAsync(
        string adminCode,
        string adminEmail,
        string adminPasswordHash,
        CancellationToken ct = default)
    {
        var admin = await db.Users.FirstOrDefaultAsync(user => user.Role == UserRole.Admin, ct);
        if (admin == null)
        {
            db.Users.Add(new AppUser
            {
                Code = adminCode,
                FullName = "Qu\u1ea3n tr\u1ecb h\u1ec7 th\u1ed1ng",
                Email = adminEmail,
                PasswordHash = adminPasswordHash,
                Role = UserRole.Admin
            });
        }
        else
        {
            admin.FullName = "Qu\u1ea3n tr\u1ecb h\u1ec7 th\u1ed1ng";
        }

        var course = await db.Courses.FirstOrDefaultAsync(c => c.Code == "PRN222", ct);
        if (course == null)
        {
            db.Courses.Add(new Course
            {
                Code = "PRN222",
                Name = "Advanced Programming with .NET",
                Credits = 3,
                Description = "Kh\u00f4ng gian t\u00e0i li\u1ec7u v\u00e0 h\u1ecfi \u0111\u00e1p m\u1eabu cho m\u00f4n PRN222."
            });
        }
        else
        {
            course.Description = "Kh\u00f4ng gian t\u00e0i li\u1ec7u v\u00e0 h\u1ecfi \u0111\u00e1p m\u1eabu cho m\u00f4n PRN222.";
        }

        // Seed Billing Packages
        var p1 = await db.BillingPackages.FindAsync(new object[] { Guid.Parse("8F8BE968-3E2A-4D78-BC86-3AD5B4B27C11") }, ct);
        if (p1 == null)
        {
            db.BillingPackages.Add(new BillingPackage
            {
                Id = Guid.Parse("8F8BE968-3E2A-4D78-BC86-3AD5B4B27C11"),
                Name = "Gói Tiêu Chuẩn",
                Price = 10000.00m,
                Credits = 20,
                Description = "Cung cấp thêm 20 lượt hỏi chatbot RAG để học tập."
            });
        }
        else
        {
            p1.Name = "Gói Tiêu Chuẩn";
            p1.Description = "Cung cấp thêm 20 lượt hỏi chatbot RAG để học tập.";
        }

        var p2 = await db.BillingPackages.FindAsync(new object[] { Guid.Parse("8F8BE968-3E2A-4D78-BC86-3AD5B4B27C22") }, ct);
        if (p2 == null)
        {
            db.BillingPackages.Add(new BillingPackage
            {
                Id = Guid.Parse("8F8BE968-3E2A-4D78-BC86-3AD5B4B27C22"),
                Name = "Gói Nâng Cao",
                Price = 20000.00m,
                Credits = 50,
                Description = "Cung cấp thêm 50 lượt hỏi chatbot RAG kèm ưu tiên xử lý."
            });
        }
        else
        {
            p2.Name = "Gói Nâng Cao";
            p2.Description = "Cung cấp thêm 50 lượt hỏi chatbot RAG kèm ưu tiên xử lý.";
        }

        var p3 = await db.BillingPackages.FindAsync(new object[] { Guid.Parse("8F8BE968-3E2A-4D78-BC86-3AD5B4B27C33") }, ct);
        if (p3 == null)
        {
            db.BillingPackages.Add(new BillingPackage
            {
                Id = Guid.Parse("8F8BE968-3E2A-4D78-BC86-3AD5B4B27C33"),
                Name = "Gói Chuyên Gia",
                Price = 50000.00m,
                Credits = 150,
                Description = "Cung cấp thêm 150 lượt hỏi chatbot RAG. Phù hợp ôn thi cuối kỳ."
            });
        }
        else
        {
            p3.Name = "Gói Chuyên Gia";
            p3.Description = "Cung cấp thêm 150 lượt hỏi chatbot RAG. Phù hợp ôn thi cuối kỳ.";
        }

        await db.SaveChangesAsync(ct);
    }
}
