using ChatBoxPRJ.Business.External;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Mapping;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.Business.Services;
using ChatBoxPRJ.DataAccess;
using Microsoft.Extensions.DependencyInjection;
using DataOptions = ChatBoxPRJ.DataAccess.Options.DataAccessOptions;

namespace ChatBoxPRJ.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(
        this IServiceCollection services,
        ApplicationLayerOptions options)
    {
        services.AddDataAccess(new DataOptions
        {
            ConnectionString = options.ConnectionString,
            VectorStoreProvider = options.VectorStore.Provider,
            VectorStoreUrl = options.VectorStore.Url,
            VectorStoreApiKey = options.VectorStore.ApiKey,
            VectorStoreCollection = options.VectorStore.Collection
        });

        services.AddAutoMapper(_ => { }, typeof(MappingProfile).Assembly);
        services.AddSingleton(options.Rag);
        services.AddSingleton(options.Storage);
        services.AddSingleton(options.Ai);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        if (options.Ai.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingService, GeminiEmbeddingService>();
            services.AddSingleton<IAnswerGenerator, GeminiAnswerGenerator>();
        }
        else
        {
            services.AddSingleton<IEmbeddingService, HashEmbeddingService>();
            services.AddSingleton<IAnswerGenerator, ExtractiveAnswerGenerator>();
        }

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICourseService, CourseService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IApplicationInitializer, ApplicationInitializer>();
        return services;
    }
}
