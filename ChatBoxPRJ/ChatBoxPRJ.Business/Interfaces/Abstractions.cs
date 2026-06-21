using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.Business.Interfaces;

public enum EmbeddingTask { Document, Query }

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, EmbeddingTask task = EmbeddingTask.Document, CancellationToken ct = default);
}

public interface IDocumentWorkQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default);
}

public interface IDocumentStatusNotifier
{
    Task NotifyAsync(Guid documentId, DocumentStatus status, int progress, string? message, CancellationToken ct = default);
}

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
    Task<(bool Success, string Message)> UpdateLecturerAsync(Guid id, string code, string fullName, string email, string? password, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteLecturerAsync(Guid id, CancellationToken ct = default);
}

public interface ICourseService
{
    Task<(bool Success, string Message)> CreateAsync(string code, string name, int credits, string description, CancellationToken ct = default);
    Task<(bool Success, string Message)> UpdateAsync(Guid id, string code, string name, int credits, string description, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CourseDto>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CourseDto>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, LecturerAccessLevel>> GetAssignmentsAsync(Guid lecturerId, CancellationToken ct = default);
    Task<(bool Success, string Message)> SaveAssignmentsAsync(Guid lecturerId, IReadOnlyDictionary<Guid, LecturerAccessLevel> assignments, CancellationToken ct = default);
    Task<LecturerAccessLevel?> GetLecturerAccessLevelAsync(Guid lecturerId, Guid courseId, CancellationToken ct = default);
    Task<bool> CanAccessAsync(Guid userId, UserRole role, Guid courseId, CancellationToken ct = default);
    Task<bool> CanManageAsync(Guid userId, UserRole role, Guid courseId, CancellationToken ct = default);
}

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(UploadRequest request, CancellationToken ct = default);
    Task ProcessAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> ListAsync(Guid? courseId, bool completedOnly, CancellationToken ct = default);
    Task<DocumentChunksDto?> GetChunksAsync(Guid documentId, Guid actorId, UserRole role, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteAsync(Guid documentId, Guid actorId, UserRole role, CancellationToken ct = default);
    Task<(string Path, string FileName, string ContentType)?> GetFileAsync(Guid documentId, Guid courseId, Guid actorId, UserRole role, CancellationToken ct = default);
}

public interface IChatService
{
    Task<ChatWorkspaceDto?> OpenWorkspaceAsync(Guid userId, UserRole role, Guid courseId, Guid? documentId = null, CancellationToken ct = default);
    Task<ChatAnswer> AskAsync(Guid userId, UserRole role, Guid courseId, Guid documentId, string question, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteHistoryAsync(Guid userId, UserRole role, Guid courseId, Guid? documentId = null, CancellationToken ct = default);
}
