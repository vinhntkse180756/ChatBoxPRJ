using System.ComponentModel.DataAnnotations;

namespace ChatBoxPRJ.DataAccess.Models;

public enum UserRole { Student, Lecturer, Admin }
public enum DocumentStatus { Processing, Completed, Failed }
public enum MessageRole { User, Assistant }
public enum LecturerAccessLevel { Lecturer, CourseHead }
public enum PaymentOrderStatus { Pending, Paid, Failed, Cancelled }
public enum SubscriptionStatus { Active, Expired, Cancelled }

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

/// <summary>Theo dõi token AI đã dùng trong ngày của sinh viên.</summary>
public sealed class StudentDailyTokenUsage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateOnly UsageDate { get; set; }
    public int TokensUsed { get; set; }
}

/// <summary>Gói đăng ký (Free / Pro / Pre). Map bảng Packages có sẵn.</summary>
public sealed class SubscriptionPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(30)] public string Code { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(1000)] public string Description { get; set; } = "";
    public decimal PriceVnd { get; set; }
    public int DailyTokenLimit { get; set; }
    public int MaxQuestionChars { get; set; }
    /// <summary>0 = không hết hạn (Free).</summary>
    public int DurationDays { get; set; }
    /// <summary>Cột cũ (số câu/ngày); giữ để tương thích DB hiện có.</summary>
    public int ChatQuestionsPerDay { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PaymentOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(40)] public string OrderCode { get; set; } = "";
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid PackageId { get; set; }
    public SubscriptionPackage Package { get; set; } = null!;
    public decimal AmountVnd { get; set; }
    [MaxLength(10)] public string Currency { get; set; } = "VND";
    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.Pending;
    [MaxLength(30)] public string Provider { get; set; } = "VNPay";
    [MaxLength(100)] public string? ProviderTransactionNo { get; set; }
    [MaxLength(10)] public string? ProviderResponseCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
}

public sealed class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid PackageId { get; set; }
    public SubscriptionPackage Package { get; set; } = null!;
    public Guid? PaymentOrderId { get; set; }
    public PaymentOrder? PaymentOrder { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndsAtUtc { get; set; }
}

/// <summary>Một lần chạy toàn bộ test set benchmark (Admin).</summary>
public sealed class BenchmarkRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public LearningDocument Document { get; set; } = null!;
    public Guid StartedById { get; set; }
    public AppUser StartedBy { get; set; } = null!;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
    public int QuestionCount { get; set; }
    public int HitCount { get; set; }
    public int RejectCount { get; set; }
    public double AverageLatencyMs { get; set; }
    public double AverageTopScore { get; set; }
    public ICollection<BenchmarkResult> Results { get; set; } = [];
}

/// <summary>Kết quả 1 câu trong test set — lưu 4 metric đã chốt ở BenchmarkScope.</summary>
public sealed class BenchmarkResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public BenchmarkRun Run { get; set; } = null!;
    [MaxLength(20)] public string QuestionId { get; set; } = "";
    [MaxLength(1000)] public string Question { get; set; } = "";
    public bool ExpectReject { get; set; }
    public bool Rejected { get; set; }
    public bool Hit { get; set; }
    public bool ExpectationMet { get; set; }
    public long LatencyMs { get; set; }
    public double TopScore { get; set; }
    public int CitationCount { get; set; }
    public string Answer { get; set; } = "";
}

public sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string FileName,
    int PageNumber,
    int ChunkNumber,
    string Content,
    double Score);
