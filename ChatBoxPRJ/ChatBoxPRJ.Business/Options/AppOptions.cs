namespace ChatBoxPRJ.Business.Options;

public sealed class RagOptions
{
    public double SimilarityThreshold { get; set; } = 0.7;
    public int TopK { get; set; } = 5;
    public int ChunkWords { get; set; } = 350;
    public int OverlapWords { get; set; } = 50;
}

public sealed class StorageOptions
{
    public string RootPath { get; set; } = "App_Data/Uploads";
}

public sealed class AiOptions
{
    public string Provider { get; set; } = "Local";
    public string? GeminiApiKey { get; set; }
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

public sealed class ApplicationLayerOptions
{
    public required string ConnectionString { get; init; }
    public required RagOptions Rag { get; init; }
    public required StorageOptions Storage { get; init; }
    public required AiOptions Ai { get; init; }
    public required VectorStoreOptions VectorStore { get; init; }
}
