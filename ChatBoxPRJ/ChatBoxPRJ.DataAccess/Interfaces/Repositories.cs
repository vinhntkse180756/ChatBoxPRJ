using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.DataAccess.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> FindByLoginAsync(string login, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeOrEmailExistsAsync(string code, string email, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> ListLecturersAsync(CancellationToken ct = default);
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
