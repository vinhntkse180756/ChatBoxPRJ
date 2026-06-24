using System.ComponentModel.DataAnnotations;

namespace ChatBoxPRJ.DataAccess.Models;

public enum UserRole { Student, Lecturer, Admin }
public enum DocumentStatus { Processing, Completed, Failed }
public enum MessageRole { User, Assistant }
public enum LecturerAccessLevel { Lecturer, CourseHead }

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(120)] public string FullName { get; set; } = "";
    [MaxLength(160)] public string Email { get; set; } = "";
    [MaxLength(500)] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<LecturerCourse> LecturerCourses { get; set; } = [];
}

public sealed class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(160)] public string Name { get; set; } = "";
    public int Credits { get; set; }
    [MaxLength(1000)] public string Description { get; set; } = "";
    public ICollection<LecturerCourse> LecturerCourses { get; set; } = [];
    public ICollection<LearningDocument> Documents { get; set; } = [];
}

public sealed class LecturerCourse
{
    public Guid LecturerId { get; set; }
    public AppUser Lecturer { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public LecturerAccessLevel AccessLevel { get; set; } = LecturerAccessLevel.Lecturer;
}

public sealed class LearningDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid UploadedById { get; set; }
    public AppUser UploadedBy { get; set; } = null!;
    [MaxLength(260)] public string OriginalFileName { get; set; } = "";
    [MaxLength(500)] public string StoragePath { get; set; } = "";
    [MaxLength(64)] public string Sha256 { get; set; } = "";
    public DocumentStatus Status { get; set; } = DocumentStatus.Processing;
    [MaxLength(1000)] public string? FailureReason { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public LearningDocument Document { get; set; } = null!;
    public Guid CourseId { get; set; }
    public int PageNumber { get; set; }
    public int ChunkNumber { get; set; }
    public string Content { get; set; } = "";
    public string VectorJson { get; set; } = "[]";
}

public sealed class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public AppUser Student { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ChatSession Session { get; set; } = null!;
    public Guid ConversationId { get; set; } = Guid.NewGuid();
    public MessageRole Role { get; set; }
    public Guid? DocumentId { get; set; }
    public string Content { get; set; } = "";
    public string? CitationsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string FileName,
    int PageNumber,
    int ChunkNumber,
    string Content,
    double Score);
