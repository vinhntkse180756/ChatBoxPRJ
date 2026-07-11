using ChatBoxPRJ.DataAccess.External;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Migrations;
using ChatBoxPRJ.DataAccess.Options;
using ChatBoxPRJ.DataAccess.Persistence;
using ChatBoxPRJ.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChatBoxPRJ.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, DataAccessOptions options)
    {
        services.AddDbContext<ChatBoxDbContext>(builder =>
            builder.UseSqlServer(options.ConnectionString, sql => sql.EnableRetryOnFailure(3)));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();

        if (options.VectorStoreProvider.Equals("Qdrant", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(new QdrantOptions
            {
                Url = options.VectorStoreUrl,
                ApiKey = options.VectorStoreApiKey,
                Collection = options.VectorStoreCollection
            });
            services.AddSingleton<IVectorStore, QdrantVectorStore>();
        }
        else
        {
            services.AddScoped<IVectorStore, EfVectorStore>();
        }

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DatabaseMigrationManager>();
        return services;
    }

}
