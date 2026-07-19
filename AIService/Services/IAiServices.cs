using AIService.Models;

namespace AIService.Services;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(
        string text,
        EmbeddingTask task = EmbeddingTask.Document,
        string? modelOverride = null,
        CancellationToken ct = default);
}

public sealed record GenerateChunkResult(string ChunkText, long ElapsedMsSinceStart);

public interface IAnswerGenerator
{
    Task<string> GenerateAsync(
        string question,
        IReadOnlyList<AnswerChunkContext> context,
        IReadOnlyList<AnswerMessageContext> history,
        CancellationToken ct = default);

    IAsyncEnumerable<GenerateChunkResult> GenerateStreamAsync(
        string question,
        IReadOnlyList<AnswerChunkContext> context,
        IReadOnlyList<AnswerMessageContext> history,
        string? modelOverride = null,
        CancellationToken ct = default);
}
