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
public sealed record StudentTokenQuotaDto(
    bool Enabled,
    int DailyLimit,
    int UsedToday,
    int Remaining,
    int MaxQuestionChars,
    string PackageCode = "FREE",
    string PackageName = "Free");

public sealed record SubscriptionPackageDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    decimal PriceVnd,
    int QuestionsPerDay,
    int MaxQuestionChars,
    int DurationDays,
    bool IsCurrent);

public sealed record StudentSubscriptionDto(
    string PackageCode,
    string PackageName,
    int QuestionsPerDay,
    int MaxQuestionChars,
    DateTime? EndsAtUtc);

public sealed record CreatePaymentResult(bool Success, string Message, string? PaymentUrl = null);
public sealed record PaymentCallbackResult(bool Success, string Message, string? OrderCode = null, string? PackageName = null);
public sealed record PaymentResultDto(
    bool Success,
    string Message,
    string OrderCode,
    string PackageCode,
    string PackageName,
    decimal AmountVnd,
    int QuestionsPerDay,
    DateTime? PaidAtUtc,
    DateTime? EndsAtUtc);

public sealed record AdminStudentAccountDto(
    Guid UserId,
    string Code,
    string FullName,
    string Email,
    DateTime CreatedAtUtc,
    string PackageCode,
    string PackageName,
    int QuestionsPerDay,
    DateTime? EndsAtUtc,
    int QuestionsUsedToday,
    int QuestionsRemainingToday,
    int PaidOrderCount,
    decimal PaidAmountTotal);

public sealed record AdminPaymentDto(
    string OrderCode,
    string StudentCode,
    string StudentName,
    string PackageName,
    decimal AmountVnd,
    string Status,
    string? ProviderResponseCode,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc);

public sealed record AdminStudentsDashboardDto(
    int StudentCount,
    int FreeCount,
    int ProCount,
    int PreCount,
    int PaidTodayCount,
    decimal RevenuePro,
    decimal RevenuePre,
    decimal RevenueTotal,
    IReadOnlyList<AdminStudentAccountDto> Students,
    IReadOnlyList<AdminPaymentDto> RecentPayments);

public sealed record ChatWorkspaceDto(
    CourseDto Course,
    Guid SessionId,
    IReadOnlyList<DocumentDto> Documents,
    IReadOnlyList<ChatHistoryDto> Histories,
    IReadOnlyList<ChatMessageDto> Messages,
    StudentTokenQuotaDto? TokenQuota = null);
public sealed record UploadRequest(Guid CourseId, Guid UploadedById, string FileName, string ContentType, Stream Content, bool Overwrite);
public sealed record UploadResult(bool Success, string Message, Guid? DocumentId = null);
public sealed record ChatAnswer(string Answer, IReadOnlyList<CitationDto> Citations, bool Rejected = false, Guid? ConversationId = null);

public sealed record NamedCountDto(string Name, int Count);
public sealed record DateCountDto(string Date, int Count);
public sealed record ReportDashboardDto(
    DateOnly FromDate,
    DateOnly ToDate,
    // Inventory
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
    double IndexSuccessRate,
    // Period
    int UploadsInRange,
    int MessagesInRange,
    int QuestionsInRange,
    int AnswersInRange,
    int RejectedAnswersInRange,
    double RejectRate,
    int ActiveStudentsInRange,
    double StudentActivationRate,
    int ActiveCoursesInRange,
    int ConversationsInRange,
    double AvgMessagesPerConversation,
    int NewStudentsInRange,
    long TokensUsedInRange,
    int StudentsUsingTokensInRange,
    // Breakdowns
    IReadOnlyList<NamedCountDto> DocumentsByCourse,
    IReadOnlyList<NamedCountDto> MessagesByCourse,
    IReadOnlyList<NamedCountDto> TopDocuments,
    IReadOnlyList<NamedCountDto> FailureReasons,
    IReadOnlyList<DateCountDto> MessagesByDay,
    IReadOnlyList<DateCountDto> QuestionsByDay,
    IReadOnlyList<DateCountDto> UploadsByDay,
    IReadOnlyList<DateCountDto> TokensByDay);

public sealed record BenchmarkResultDto(
    string QuestionId,
    string Question,
    bool ExpectReject,
    bool Rejected,
    bool Hit,
    bool ExpectationMet,
    long LatencyMs,
    double TopScore,
    int CitationCount,
    string Answer);

public sealed record BenchmarkRunDto(
    Guid Id,
    Guid CourseId,
    string CourseCode,
    Guid DocumentId,
    string DocumentFileName,
    string StartedByCode,
    DateTime StartedAtUtc,
    DateTime? FinishedAtUtc,
    int QuestionCount,
    int HitCount,
    int RejectCount,
    double AverageLatencyMs,
    double AverageTopScore,
    int ExpectationMetCount,
    IReadOnlyList<BenchmarkResultDto> Results);

public sealed record BenchmarkRunSummaryDto(
    Guid Id,
    string CourseCode,
    string DocumentFileName,
    DateTime StartedAtUtc,
    int QuestionCount,
    int HitCount,
    int RejectCount,
    double AverageLatencyMs,
    double AverageTopScore,
    int ExpectationMetCount);
