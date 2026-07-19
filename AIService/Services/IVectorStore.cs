using BusinessObjects.Entities;

namespace AIService.Services;

public interface IVectorStore
{
    Task UpsertAsync(LearningDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid courseId, Guid? documentId, float[] queryVector, int limit, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);
}
