namespace ChatBoxPRJ.Business.DTOs;

public enum EmbeddingTask { Document, Query }

public sealed record AnswerChunkContext(
    Guid ChunkId,
    Guid DocumentId,
    string FileName,
    int PageNumber,
    int ChunkNumber,
    string Content,
    double Score);

public sealed record AnswerMessageContext(MessageRole Role, string Content);
