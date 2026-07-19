using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAcessLayer.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<LecturerCourse> LecturerCourses => Set<LecturerCourse>();
    public DbSet<LearningDocument> Documents => Set<LearningDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<BenchmarkRun> BenchmarkRuns => Set<BenchmarkRun>();
    public DbSet<BenchmarkResult> BenchmarkResults => Set<BenchmarkResult>();
    public DbSet<StudentDailyTokenUsage> StudentDailyTokenUsages => Set<StudentDailyTokenUsage>();
    public DbSet<SubscriptionPackage> SubscriptionPackages => Set<SubscriptionPackage>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AppUser>().HasIndex(x => x.Code).IsUnique();
        model.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        model.Entity<Course>().HasIndex(x => x.Code).IsUnique();
        model.Entity<LearningDocument>().HasIndex(x => new { x.CourseId, x.Sha256 }).IsUnique();
        model.Entity<LecturerCourse>().HasKey(x => new { x.LecturerId, x.CourseId });
        model.Entity<LecturerCourse>().HasIndex(x => x.CourseId);
        model.Entity<LecturerCourse>().HasIndex(x => new { x.CourseId, x.AccessLevel })
            .IsUnique()
            .HasFilter("[AccessLevel] = 1");
        model.Entity<ChatSession>().HasIndex(x => new { x.StudentId, x.CourseId }).IsUnique();
        model.Entity<LecturerCourse>().HasOne(x => x.Lecturer).WithMany(x => x.LecturerCourses).HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<LecturerCourse>().HasOne(x => x.Course).WithMany(x => x.LecturerCourses).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<LearningDocument>().HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DocumentChunk>().HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<ChatSession>().HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ChatMessage>().HasOne(x => x.Session).WithMany(x => x.Messages).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<ChatMessage>().HasIndex(x => new { x.SessionId, x.ConversationId, x.CreatedAtUtc });

        model.Entity<StudentDailyTokenUsage>().HasIndex(x => new { x.UserId, x.UsageDate }).IsUnique();
        model.Entity<StudentDailyTokenUsage>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        model.Entity<SubscriptionPackage>(e =>
        {
            e.ToTable("Packages");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.PriceVnd).HasPrecision(18, 2);
        });

        model.Entity<PaymentOrder>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.OrderCode).HasMaxLength(40);
            e.Property(x => x.ProviderTransactionNo).HasColumnName("ProviderReference").HasMaxLength(100);
            e.Property(x => x.AmountVnd).HasPrecision(18, 2);
            e.HasIndex(x => x.OrderCode).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<UserSubscription>(e =>
        {
            e.ToTable("UserSubscriptions");
            e.Property(x => x.PaymentOrderId).HasColumnName("PaymentId");
            e.Property(x => x.StartsAtUtc).HasColumnName("StartedAtUtc");
            e.Property(x => x.EndsAtUtc).HasColumnName("ExpiresAtUtc");
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PaymentOrder).WithMany().HasForeignKey(x => x.PaymentOrderId).OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<BenchmarkRun>().HasIndex(x => x.StartedAtUtc);
        model.Entity<BenchmarkRun>().HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BenchmarkRun>().HasOne(x => x.Document).WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<BenchmarkRun>().HasOne(x => x.StartedBy).WithMany().HasForeignKey(x => x.StartedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BenchmarkResult>().HasOne(x => x.Run).WithMany(x => x.Results).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<BenchmarkResult>().HasIndex(x => new { x.RunId, x.QuestionId });
    }
}
