using ChatBoxPRJ.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.DataAccess.Persistence;

public sealed class ChatBoxDbContext(DbContextOptions<ChatBoxDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<LecturerCourse> LecturerCourses => Set<LecturerCourse>();
    public DbSet<LearningDocument> Documents => Set<LearningDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AppUser>().HasIndex(x => x.Code).IsUnique();
        model.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        model.Entity<Course>().HasIndex(x => x.Code).IsUnique();
        model.Entity<LearningDocument>().HasIndex(x => new { x.CourseId, x.Sha256 }).IsUnique();
        model.Entity<LecturerCourse>().HasKey(x => new { x.LecturerId, x.CourseId });
        model.Entity<LecturerCourse>().HasIndex(x => x.CourseId).IsUnique();
        model.Entity<ChatSession>().HasIndex(x => new { x.StudentId, x.CourseId }).IsUnique();
        model.Entity<LecturerCourse>().HasOne(x => x.Lecturer).WithMany(x => x.LecturerCourses).HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<LecturerCourse>().HasOne(x => x.Course).WithMany(x => x.LecturerCourses).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<LearningDocument>().HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DocumentChunk>().HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<ChatSession>().HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ChatMessage>().HasOne(x => x.Session).WithMany(x => x.Messages).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<ChatMessage>().HasIndex(x => new { x.SessionId, x.DocumentId, x.CreatedAtUtc });
    }
}
