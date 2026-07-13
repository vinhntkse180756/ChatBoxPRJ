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

        await db.SaveChangesAsync(ct);
    }
}
