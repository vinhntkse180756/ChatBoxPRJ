using ChatBoxPRJ.Business.Domain;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.Infrastructure;

public sealed class DatabaseSeeder(ChatBoxDbContext db, IPasswordHasher hasher, IConfiguration config)
{
    public async Task SeedAsync()
    {
        if (!await db.Users.AnyAsync(x => x.Role == UserRole.Admin))
        {
            db.Users.Add(new AppUser
            {
                Code = config["SeedAdmin:Code"] ?? "admin", FullName = "Quản trị hệ thống",
                Email = config["SeedAdmin:Email"] ?? "admin@chatbox.local",
                PasswordHash = hasher.Hash(config["SeedAdmin:Password"] ?? "Admin@123"), Role = UserRole.Admin
            });
        }
        if (!await db.Courses.AnyAsync())
        {
            db.Courses.Add(new Course { Code = "PRN222", Name = "Advanced Programming with .NET", Credits = 3, Description = "Không gian tài liệu và hỏi đáp mẫu cho môn PRN222." });
        }
        await db.SaveChangesAsync();
    }
}
