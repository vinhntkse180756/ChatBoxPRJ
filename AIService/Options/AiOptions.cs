namespace AIService.Options;

public sealed class AiOptions
{
    public string Provider { get; set; } = "Local";
    public string? GeminiApiKey { get; set; }
    public string? GitHubToken { get; set; }
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public string ChatModel { get; set; } = "gemini-3.1-flash-lite";
}

public sealed class VectorStoreOptions
{
    public string Provider { get; set; } = "SqlServer";
    public string Url { get; set; } = "http://localhost:6333";
    public string? ApiKey { get; set; }
    public string Collection { get; set; } = "student_documents";
}

public sealed class QdrantOptions
{
    public string Url { get; init; } = "http://localhost:6333";
    public string? ApiKey { get; init; }
    public string Collection { get; init; } = "student_documents";
}
