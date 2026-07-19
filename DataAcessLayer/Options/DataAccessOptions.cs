namespace DataAcessLayer.Options;

public sealed class DataAccessOptions
{
    public required string ConnectionString { get; init; }
    public string VectorStoreProvider { get; init; } = "SqlServer";
    public string VectorStoreUrl { get; init; } = "http://localhost:6333";
    public string? VectorStoreApiKey { get; init; }
    public string VectorStoreCollection { get; init; } = "student_documents";
}
