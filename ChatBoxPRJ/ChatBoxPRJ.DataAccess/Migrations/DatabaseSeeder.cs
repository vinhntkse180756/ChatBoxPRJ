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

        await db.SaveChangesAsync(ct);
    }
}
