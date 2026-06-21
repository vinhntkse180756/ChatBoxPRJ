using System.Text.Json;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.DataAccess.Repositories;

public sealed class UserRepository(ChatBoxDbContext db) : IUserRepository
{
    public Task<AppUser?> FindByLoginAsync(string login, CancellationToken ct = default)
    {
        var normalized = login.ToLower();
        return db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Code.ToLower() == normalized || x.Email.ToLower() == normalized, ct);
    }
    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default) => db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CodeOrEmailExistsAsync(string code, string email, CancellationToken ct = default) => db.Users.AnyAsync(x => x.Code == code || x.Email == email, ct);
    public async Task AddAsync(AppUser user, CancellationToken ct = default) { db.Users.Add(user); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<AppUser>> ListLecturersAsync(CancellationToken ct = default) => await db.Users.AsNoTracking().Where(x => x.Role == UserRole.Lecturer).OrderBy(x => x.FullName).ToListAsync(ct);
    public async Task UpdateAsync(AppUser user, CancellationToken ct = default) { db.Users.Update(user); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<string>> DeleteLecturerAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.Role == UserRole.Lecturer, ct);
        if (user is null) return [];
        var docs = await db.Documents.Where(x => x.UploadedById == id).ToListAsync(ct);
        var paths = docs.Select(x => x.StoragePath).ToList();
        db.Documents.RemoveRange(docs); db.Users.Remove(user); await db.SaveChangesAsync(ct); return paths;
    }
}

public sealed class CourseRepository(ChatBoxDbContext db) : ICourseRepository
{
    public async Task<IReadOnlyList<Course>> ListAsync(CancellationToken ct = default) => await db.Courses.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<IReadOnlyList<Course>> ListForLecturerAsync(Guid lecturerId, CancellationToken ct = default) => await db.LecturerCourses.AsNoTracking().Where(x => x.LecturerId == lecturerId).Select(x => x.Course).OrderBy(x => x.Name).ToListAsync(ct);
    public Task<Course?> FindAsync(Guid id, CancellationToken ct = default) => db.Courses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddAsync(Course course, CancellationToken ct = default) { db.Courses.Add(course); await db.SaveChangesAsync(ct); }
    public async Task UpdateAsync(Course course, CancellationToken ct = default) { db.Courses.Update(course); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<string>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var course = await db.Courses.Include(x => x.Documents).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (course is null) return [];
        var paths = course.Documents.Select(x => x.StoragePath).ToList(); db.Courses.Remove(course); await db.SaveChangesAsync(ct); return paths;
    }
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => db.Courses.AnyAsync(x => x.Code == code, ct);
    public async Task<IReadOnlySet<Guid>> GetLecturerCourseIdsAsync(Guid lecturerId, CancellationToken ct = default)
        => (await db.LecturerCourses.AsNoTracking().Where(x => x.LecturerId == lecturerId).Select(x => x.CourseId).ToListAsync(ct)).ToHashSet();
    public Task<Guid?> GetCourseHeadAsync(Guid courseId, CancellationToken ct = default)
        => db.LecturerCourses.Where(x => x.CourseId == courseId).Select(x => (Guid?)x.LecturerId).FirstOrDefaultAsync(ct);

    public async Task ReplaceLecturerCoursesAsync(Guid lecturerId, IReadOnlySet<Guid> courseIds, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var old = await db.LecturerCourses
                .Where(x => x.LecturerId == lecturerId)
                .ToListAsync(ct);
            var oldIds = old.Select(x => x.CourseId).ToHashSet();

            var occupied = await db.LecturerCourses.AsNoTracking()
                .Where(x => courseIds.Contains(x.CourseId) && x.LecturerId != lecturerId)
                .Select(x => x.CourseId).ToListAsync(ct);
            if (occupied.Count > 0) throw new InvalidOperationException("Một hoặc nhiều môn đã có trưởng bộ môn.");

            db.LecturerCourses.RemoveRange(old.Where(x => !courseIds.Contains(x.CourseId)));
            db.LecturerCourses.AddRange(courseIds
                .Where(courseId => !oldIds.Contains(courseId))
                .Select(courseId => new LecturerCourse
                {
                    LecturerId = lecturerId,
                    CourseId = courseId
                }));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}

public sealed class DocumentRepository(ChatBoxDbContext db) : IDocumentRepository
{
    public Task<bool> HashExistsAsync(Guid courseId, string sha256, CancellationToken ct = default) => db.Documents.AnyAsync(x => x.CourseId == courseId && x.Sha256 == sha256, ct);
    public Task<LearningDocument?> FindAsync(Guid id, CancellationToken ct = default) => db.Documents.Include(x => x.Course).Include(x => x.Chunks).FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<LearningDocument?> FindByNameAsync(Guid courseId, string fileName, CancellationToken ct = default) => db.Documents.FirstOrDefaultAsync(x => x.CourseId == courseId && x.OriginalFileName == fileName, ct);
    public async Task AddAsync(LearningDocument document, CancellationToken ct = default) { db.Documents.Add(document); await db.SaveChangesAsync(ct); }
    public async Task UpdateAsync(LearningDocument document, CancellationToken ct = default) { await db.SaveChangesAsync(ct); }
    public async Task ReplaceChunksAsync(Guid documentId, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
    {
        var old = await db.DocumentChunks.Where(x => x.DocumentId == documentId).ToListAsync(ct);
        db.DocumentChunks.RemoveRange(old);
        db.DocumentChunks.AddRange(chunks);
        await db.SaveChangesAsync(ct);
    }
    public async Task<IReadOnlyList<LearningDocument>> ListAsync(Guid? courseId, bool completedOnly, CancellationToken ct = default)
    {
        var query = db.Documents.AsNoTracking().Include(x => x.Course).AsQueryable();
        if (courseId.HasValue) query = query.Where(x => x.CourseId == courseId);
        if (completedOnly) query = query.Where(x => x.Status == DocumentStatus.Completed);
        return await query.OrderByDescending(x => x.UploadedAtUtc).ToListAsync(ct);
    }
    public async Task DeleteAsync(LearningDocument document, CancellationToken ct = default) { db.Documents.Remove(document); await db.SaveChangesAsync(ct); }
}

public sealed class ChatRepository(ChatBoxDbContext db) : IChatRepository
{
    public async Task<ChatSession> GetOrCreateSessionAsync(Guid studentId, Guid courseId, CancellationToken ct = default)
    {
        var session = await db.ChatSessions.FirstOrDefaultAsync(x => x.StudentId == studentId && x.CourseId == courseId, ct);
        if (session is not null) return session;
        session = new ChatSession { StudentId = studentId, CourseId = courseId };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid sessionId, Guid? documentId = null, CancellationToken ct = default)
    {
        var query = db.ChatMessages.AsNoTracking().Where(x => x.SessionId == sessionId);
        if (documentId.HasValue) query = query.Where(x => x.DocumentId == documentId);
        return await query.OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
    }
    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        db.ChatMessages.Add(message);
        var session = await db.ChatSessions.FindAsync([message.SessionId], ct);
        if (session is not null) session.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}

public sealed class EfVectorStore(ChatBoxDbContext db) : IVectorStore
{
    // Các vector fallback được lưu cùng DocumentChunk bởi DocumentRepository.
    public Task UpsertAsync(LearningDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default) => Task.CompletedTask;

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid courseId, Guid documentId, float[] queryVector, int limit, CancellationToken ct = default)
    {
        var chunks = await db.DocumentChunks.AsNoTracking().Include(x => x.Document)
            .Where(x => x.CourseId == courseId && x.DocumentId == documentId && x.Document.Status == DocumentStatus.Completed).ToListAsync(ct);
        return chunks.Select(x => new RetrievedChunk(x.Id, x.DocumentId, x.Document.OriginalFileName, x.PageNumber, x.ChunkNumber, x.Content,
                Cosine(queryVector, JsonSerializer.Deserialize<float[]>(x.VectorJson) ?? [])))
            .OrderByDescending(x => x.Score).Take(limit).ToList();
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var chunks = await db.DocumentChunks.Where(x => x.DocumentId == documentId).ToListAsync(ct);
        db.DocumentChunks.RemoveRange(chunks);
        await db.SaveChangesAsync(ct);
    }

    private static double Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }
}
