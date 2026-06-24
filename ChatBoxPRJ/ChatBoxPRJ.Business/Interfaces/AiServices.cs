using ChatBoxPRJ.Business.DTOs;

namespace ChatBoxPRJ.Business.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(
        string text,
        EmbeddingTask task = EmbeddingTask.Document,
        CancellationToken ct = default);
}

public interface IAnswerGenerator
{
    Task<string> GenerateAsync(
        string question,
        IReadOnlyList<AnswerChunkContext> context,
        IReadOnlyList<AnswerMessageContext> history,
        CancellationToken ct = default);
}
