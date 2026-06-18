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
