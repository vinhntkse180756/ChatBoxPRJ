using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChatBoxPRJ.DataAccess.Persistence;

public sealed class ChatBoxDbContextFactory : IDesignTimeDbContextFactory<ChatBoxDbContext>
{
    public ChatBoxDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ChatBoxDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=StudentChatBoxDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new ChatBoxDbContext(options);
    }
}
