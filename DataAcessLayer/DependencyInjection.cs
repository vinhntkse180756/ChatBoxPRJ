using DataAcessLayer.Interfaces;
using DataAcessLayer.Migrations;
using DataAcessLayer.Options;
using DataAcessLayer.Persistence;
using DataAcessLayer.Repositories;
using AIService.Services;
using AIService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DataAcessLayer;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, DataAccessOptions options)
    {
        services.AddDbContext<AppDbContext>(builder =>
            builder.UseSqlServer(options.ConnectionString, sql => sql.EnableRetryOnFailure(3)));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
        services.AddScoped<IStudentTokenUsageRepository, StudentTokenUsageRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        if (options.VectorStoreProvider.Equals("Qdrant", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(new QdrantOptions
            {
                Url = options.VectorStoreUrl,
                ApiKey = options.VectorStoreApiKey,
                Collection = options.VectorStoreCollection
            });
            services.AddSingleton<IVectorStore, AIService.Services.QdrantVectorStore>();
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
