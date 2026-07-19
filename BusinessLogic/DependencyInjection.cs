using BusinessLogic.External;
using BusinessLogic.Interfaces;
using BusinessLogic.Mapping;
using BusinessLogic.Options;
using BusinessLogic.Services;
using DataAcessLayer;
using AIService.Services;
using AIService.Options;
using Microsoft.Extensions.DependencyInjection;
using DataOptions = DataAcessLayer.Options.DataAccessOptions;

namespace BusinessLogic;

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
        services.AddSingleton(new AIService.Options.AiOptions
        {
            Provider = options.Ai.Provider,
            GeminiApiKey = options.Ai.GeminiApiKey,
            GitHubToken = options.Ai.GitHubToken,
            EmbeddingModel = options.Ai.EmbeddingModel,
            ChatModel = options.Ai.ChatModel
        });
        services.AddSingleton(options.StudentUsage);
        services.AddSingleton(options.VnPay);
        services.AddSingleton<VnPayGateway>();
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
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();
        services.AddScoped<IApplicationInitializer, ApplicationInitializer>();
        return services;
    }
}

