using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> FindByLoginAsync(string login, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeOrEmailExistsAsync(string code, string email, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> ListLecturersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> ListStudentsAsync(CancellationToken ct = default);
    Task UpdateAsync(AppUser user, CancellationToken ct = default);
    Task<IReadOnlyList<string>> DeleteUserAsync(Guid id, CancellationToken ct = default);
}

public interface ICourseRepository
{
    Task<IReadOnlyList<Course>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Course>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default);
    Task<Course?> FindAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Course course, CancellationToken ct = default);
    Task UpdateAsync(Course course, CancellationToken ct = default);
    Task<IReadOnlyList<string>> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, LecturerAccessLevel>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> ListCourseHeadCourseIdsAsync(IReadOnlyCollection<Guid> courseIds, Guid excludedLecturerId, CancellationToken ct = default);
    Task ReplaceLecturerCoursesAsync(Guid lecturerId, IReadOnlyDictionary<Guid, LecturerAccessLevel> assignments, CancellationToken ct = default);
    Task<LecturerAccessLevel?> GetLecturerAccessLevelAsync(Guid lecturerId, Guid courseId, CancellationToken ct = default);
}

public interface IDocumentRepository
{
    Task<bool> HashExistsAsync(Guid courseId, string sha256, CancellationToken ct = default);
    Task<LearningDocument?> FindAsync(Guid id, CancellationToken ct = default);
    Task<LearningDocument?> FindByNameAsync(Guid courseId, string fileName, CancellationToken ct = default);
    Task AddAsync(LearningDocument document, CancellationToken ct = default);
    Task UpdateAsync(LearningDocument document, CancellationToken ct = default);
    Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<LearningDocument>> ListAsync(Guid? courseId, bool completedOnly, CancellationToken ct = default);
    Task DeleteAsync(LearningDocument document, CancellationToken ct = default);
}

public interface IChatRepository
{
    Task<ChatSession> GetOrCreateSessionAsync(Guid studentId, Guid courseId, CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid sessionId, Guid? conversationId = null, CancellationToken ct = default);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<int> DeleteMessagesAsync(Guid sessionId, Guid? conversationId = null, CancellationToken ct = default);
}

public interface IVectorStore
{
    Task UpsertAsync(LearningDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid courseId, Guid documentId, float[] queryVector, int limit, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);
}

public sealed record NamedCountRow(string Name, int Count);
public sealed record DateCountRow(DateOnly Date, int Count);

public sealed record ReportSnapshot(
    // Inventory (all-time)
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
    // Period activity
    int UploadsInRange,
    int MessagesInRange,
    int QuestionsInRange,
    int AnswersInRange,
    int RejectedAnswersInRange,
    int ActiveStudentsInRange,
    int ActiveCoursesInRange,
    int ConversationsInRange,
    int NewStudentsInRange,
    long TokensUsedInRange,
    int StudentsUsingTokensInRange,
    // Series & breakdowns
    IReadOnlyList<NamedCountRow> DocumentsByCourse,
    IReadOnlyList<NamedCountRow> MessagesByCourse,
    IReadOnlyList<NamedCountRow> TopDocuments,
    IReadOnlyList<NamedCountRow> FailureReasons,
    IReadOnlyList<DateCountRow> MessagesByDay,
    IReadOnlyList<DateCountRow> QuestionsByDay,
    IReadOnlyList<DateCountRow> UploadsByDay,
    IReadOnlyList<DateCountRow> TokensByDay);

public interface IReportRepository
{
    Task<ReportSnapshot> GetSnapshotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public interface IBenchmarkRepository
{
    Task AddRunAsync(BenchmarkRun run, CancellationToken ct = default);
    Task<BenchmarkRun?> FindRunAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<BenchmarkRun>> ListRecentRunsAsync(int take = 20, CancellationToken ct = default);
}

public interface IStudentTokenUsageRepository
{
    Task<int> GetUsedTokensAsync(Guid userId, DateOnly usageDate, CancellationToken ct = default);
    Task AddTokensAsync(Guid userId, DateOnly usageDate, int tokens, CancellationToken ct = default);
}

public interface ISubscriptionRepository
{
    Task<IReadOnlyList<SubscriptionPackage>> ListActivePackagesAsync(CancellationToken ct = default);
    Task<SubscriptionPackage?> FindPackageByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionPackage?> FindPackageByCodeAsync(string code, CancellationToken ct = default);
    Task EnsurePackagesSeededAsync(IEnumerable<SubscriptionPackage> packages, CancellationToken ct = default);
    Task<UserSubscription?> GetActiveSubscriptionAsync(Guid userId, DateTime utcNow, CancellationToken ct = default);
    Task AddPaymentOrderAsync(PaymentOrder order, CancellationToken ct = default);
    Task<PaymentOrder?> FindPaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default);
    Task UpdatePaymentOrderAsync(PaymentOrder order, CancellationToken ct = default);
    Task ActivateSubscriptionAsync(UserSubscription subscription, CancellationToken ct = default);
    Task ExpireActiveSubscriptionsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default);
    Task<IReadOnlyList<AdminStudentAccountRow>> ListStudentAccountsAsync(DateOnly usageDate, DateTime utcNow, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentOrder>> ListRecentPaymentsAsync(int take = 30, CancellationToken ct = default);
    Task<(decimal ProRevenue, decimal PreRevenue, decimal TotalRevenue)> GetPaidRevenueAsync(CancellationToken ct = default);
}

public sealed record AdminStudentAccountRow(
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
    int PaidOrderCount,
    decimal PaidAmountTotal);
