using ChatBoxPRJ.Business.Domain;
using ChatBoxPRJ.Business.DTOs;

namespace ChatBoxPRJ.Business.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> FindByLoginAsync(string login, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeOrEmailExistsAsync(string code, string email, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> ListLecturersAsync(CancellationToken ct = default);
}

public interface ICourseRepository
{
    Task<IReadOnlyList<Course>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Course>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default);
    Task<Course?> FindAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Course course, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetLecturerCourseIdsAsync(Guid lecturerId, CancellationToken ct = default);
    Task ReplaceLecturerCoursesAsync(Guid lecturerId, IReadOnlySet<Guid> courseIds, CancellationToken ct = default);
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
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid sessionId, Guid? documentId = null, CancellationToken ct = default);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct = default);
}

public interface IVectorStore
{
    Task UpsertAsync(LearningDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default);
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid courseId, Guid documentId, float[] queryVector, int limit, CancellationToken ct = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);
}

public enum EmbeddingTask { Document, Query }
public interface IEmbeddingService { Task<float[]> EmbedAsync(string text, EmbeddingTask task = EmbeddingTask.Document, CancellationToken ct = default); }
public interface IDocumentWorkQueue { ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default); }
public interface IDocumentStatusNotifier { Task NotifyAsync(Guid documentId, DocumentStatus status, string? message, CancellationToken ct = default); }
public interface IAnswerGenerator
{
    Task<string> GenerateAsync(string question, IReadOnlyList<RetrievedChunk> context, IReadOnlyList<ChatMessage> history, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string encodedHash);
}

public interface IAccountService
{
    Task<(bool Success, string Message)> RegisterStudentAsync(string code, string fullName, string email, string password, CancellationToken ct = default);
    Task<(bool Success, string Message)> CreateLecturerAsync(string code, string fullName, string email, string password, CancellationToken ct = default);
    Task<UserDto?> AuthenticateAsync(string login, string password, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> ListLecturersAsync(CancellationToken ct = default);
}

public interface ICourseService
{
    Task<(bool Success, string Message)> CreateAsync(string code, string name, int credits, string description, CancellationToken ct = default);
    Task<IReadOnlyList<CourseDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CourseDto>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default);
    Task<IReadOnlySet<Guid>> GetAssignmentsAsync(Guid lecturerId, CancellationToken ct = default);
    Task<(bool Success, string Message)> SaveAssignmentsAsync(Guid lecturerId, IReadOnlySet<Guid> courseIds, CancellationToken ct = default);
    Task<bool> CanManageAsync(Guid userId, UserRole role, Guid courseId, CancellationToken ct = default);
}

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(UploadRequest request, CancellationToken ct = default);
    Task ProcessAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> ListAsync(Guid? courseId, bool completedOnly, CancellationToken ct = default);
    Task<DocumentChunksDto?> GetChunksAsync(Guid documentId, Guid actorId, UserRole role, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteAsync(Guid documentId, Guid actorId, UserRole role, CancellationToken ct = default);
}

public interface IChatService
{
    Task<ChatWorkspaceDto?> OpenWorkspaceAsync(Guid studentId, Guid courseId, Guid? documentId = null, CancellationToken ct = default);
    Task<ChatAnswer> AskAsync(Guid studentId, Guid courseId, Guid documentId, string question, CancellationToken ct = default);
}
