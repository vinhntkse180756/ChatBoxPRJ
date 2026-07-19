using AIService.Services;
using AIService.Options;
using Microsoft.Extensions.DependencyInjection;

namespace AIService;

public static class DependencyInjection
{
    public static IServiceCollection AddAiService(
        this IServiceCollection services,
        AiOptions ai,
        QdrantOptions qdrant)
    {
        services.AddSingleton(ai);
        services.AddSingleton(qdrant);

        if (ai.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingService, GeminiEmbeddingService>();
            services.AddSingleton<IAnswerGenerator, GeminiAnswerGenerator>();
        }
        else
        {
            services.AddSingleton<IEmbeddingService, HashEmbeddingService>();
            services.AddSingleton<IAnswerGenerator, ExtractiveAnswerGenerator>();
        }

        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        return services;
    }
}
