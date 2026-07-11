namespace ChatBoxPRJ.Business.DTOs;

public enum UserRole { Student, Lecturer, Admin }
public enum DocumentStatus { Processing, Completed, Failed }
public enum MessageRole { User, Assistant }
public enum LecturerAccessLevel { Lecturer, CourseHead }

public sealed record UserDto(Guid Id, string Code, string FullName, string Email, UserRole Role);
public sealed record CourseDto(Guid Id, string Code, string Name, int Credits, string Description);
public sealed record DocumentDto(Guid Id, Guid CourseId, string CourseName, string FileName, DocumentStatus Status, string? FailureReason, DateTime UploadedAtUtc);
public sealed record DocumentChunkDto(Guid Id, int PageNumber, int ChunkNumber, int WordCount, string Content);
public sealed record DocumentChunksDto(Guid DocumentId, string FileName, string CourseName, DocumentStatus Status, IReadOnlyList<DocumentChunkDto> Chunks);
public sealed record CitationDto(Guid DocumentId, string FileName, int PageNumber, int ChunkNumber, string Excerpt);
public sealed record ChatMessageDto(Guid Id, MessageRole Role, string Content, IReadOnlyList<CitationDto> Citations, DateTime CreatedAtUtc);
public sealed record ChatHistoryDto(Guid ConversationId, Guid DocumentId, string FileName, string Title, int MessageCount, DateTime UpdatedAtUtc);
public sealed record ChatWorkspaceDto(CourseDto Course, Guid SessionId, IReadOnlyList<DocumentDto> Documents, IReadOnlyList<ChatHistoryDto> Histories, IReadOnlyList<ChatMessageDto> Messages);
public sealed record UploadRequest(Guid CourseId, Guid UploadedById, string FileName, string ContentType, Stream Content, bool Overwrite);
public sealed record UploadResult(bool Success, string Message, Guid? DocumentId = null);
public sealed record ChatAnswer(string Answer, IReadOnlyList<CitationDto> Citations, bool Rejected = false, Guid? ConversationId = null);

public sealed record NamedCountDto(string Name, int Count);
public sealed record DateCountDto(string Date, int Count);
public sealed record ReportDashboardDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int StudentCount,
    int LecturerCount,
    int AdminCount,
    int CourseCount,
    int DocumentCount,
    int ChunkCount,
    int ChatSessionCount,
    int ChatMessageCount,
    int DocumentsCompleted,
    int DocumentsProcessing,
    int DocumentsFailed,
    int UploadsInRange,
    int MessagesInRange,
    IReadOnlyList<NamedCountDto> DocumentsByCourse,
    IReadOnlyList<NamedCountDto> MessagesByCourse,
    IReadOnlyList<DateCountDto> MessagesByDay,
    IReadOnlyList<DateCountDto> UploadsByDay);
