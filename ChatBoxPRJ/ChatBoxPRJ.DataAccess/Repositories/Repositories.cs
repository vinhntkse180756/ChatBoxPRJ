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
    public async Task<IReadOnlyList<AppUser>> ListStudentsAsync(CancellationToken ct = default)
        => await db.Users.AsNoTracking().Where(x => x.Role == UserRole.Student).OrderBy(x => x.Code).ToListAsync(ct);
    public async Task UpdateAsync(AppUser user, CancellationToken ct = default) { db.Users.Update(user); await db.SaveChangesAsync(ct); }
    public async Task<IReadOnlyList<string>> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) return [];
        var docs = await db.Documents.Where(x => x.UploadedById == id).ToListAsync(ct);
        var chatSessions = await db.ChatSessions.Where(x => x.StudentId == id).ToListAsync(ct);
        var paths = docs.Select(x => x.StoragePath).ToList();
        db.Documents.RemoveRange(docs);
        db.ChatSessions.RemoveRange(chatSessions);
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return paths;
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
    public async Task<IReadOnlyDictionary<Guid, LecturerAccessLevel>> GetLecturerAssignmentsAsync(Guid lecturerId, CancellationToken ct = default)
        => await db.LecturerCourses.AsNoTracking()
            .Where(x => x.LecturerId == lecturerId)
            .ToDictionaryAsync(x => x.CourseId, x => x.AccessLevel, ct);

    public async Task<IReadOnlyList<Guid>> ListCourseHeadCourseIdsAsync(
        IReadOnlyCollection<Guid> courseIds,
        Guid excludedLecturerId,
        CancellationToken ct = default)
        => await db.LecturerCourses.AsNoTracking()
            .Where(x => courseIds.Contains(x.CourseId)
                && x.LecturerId != excludedLecturerId
                && x.AccessLevel == LecturerAccessLevel.CourseHead)
            .Select(x => x.CourseId)
            .ToListAsync(ct);

    public Task<LecturerAccessLevel?> GetLecturerAccessLevelAsync(Guid lecturerId, Guid courseId, CancellationToken ct = default)
        => db.LecturerCourses.AsNoTracking()
            .Where(x => x.LecturerId == lecturerId && x.CourseId == courseId)
            .Select(x => (LecturerAccessLevel?)x.AccessLevel)
            .FirstOrDefaultAsync(ct);

    public async Task ReplaceLecturerCoursesAsync(Guid lecturerId, IReadOnlyDictionary<Guid, LecturerAccessLevel> assignments, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var assignedCourseIds = assignments.Keys.ToHashSet();
            var old = await db.LecturerCourses
                .Where(x => x.LecturerId == lecturerId)
                .ToListAsync(ct);
            var oldIds = old.Select(x => x.CourseId).ToHashSet();

            db.LecturerCourses.RemoveRange(old.Where(x => !assignedCourseIds.Contains(x.CourseId)));
            foreach (var current in old.Where(x => assignments.ContainsKey(x.CourseId)))
                current.AccessLevel = assignments[current.CourseId];
            db.LecturerCourses.AddRange(assignedCourseIds
                .Where(courseId => !oldIds.Contains(courseId))
                .Select(courseId => new LecturerCourse
                {
                    LecturerId = lecturerId,
                    CourseId = courseId,
                    AccessLevel = assignments[courseId]
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
    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid sessionId, Guid? conversationId = null, CancellationToken ct = default)
    {
        var query = db.ChatMessages.AsNoTracking().Where(x => x.SessionId == sessionId);
        if (conversationId.HasValue) query = query.Where(x => x.ConversationId == conversationId);
        return await query.OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        db.ChatMessages.Add(message);
        var session = await db.ChatSessions.FindAsync([message.SessionId], ct);
        if (session is not null) session.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteMessagesAsync(Guid sessionId, Guid? conversationId = null, CancellationToken ct = default)
    {
        var query = db.ChatMessages.Where(x => x.SessionId == sessionId);
        if (conversationId.HasValue) query = query.Where(x => x.ConversationId == conversationId);
        var messages = await query.ToListAsync(ct);
        if (messages.Count == 0) return 0;
        db.ChatMessages.RemoveRange(messages);
        var session = await db.ChatSessions.FindAsync([sessionId], ct);
        if (session is not null) session.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return messages.Count;
    }
}

public sealed class ReportRepository(ChatBoxDbContext db) : IReportRepository
{
    public async Task<ReportSnapshot> GetSnapshotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var users = await db.Users.AsNoTracking()
            .GroupBy(x => x.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountRole(UserRole role) => users.FirstOrDefault(x => x.Role == role)?.Count ?? 0;

        var courseCount = await db.Courses.AsNoTracking().CountAsync(ct);
        var documentCount = await db.Documents.AsNoTracking().CountAsync(ct);
        var chunkCount = await db.DocumentChunks.AsNoTracking().CountAsync(ct);
        var sessionCount = await db.ChatSessions.AsNoTracking().CountAsync(ct);
        var messageCount = await db.ChatMessages.AsNoTracking().CountAsync(ct);

        var statusCounts = await db.Documents.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int StatusCount(DocumentStatus status) => statusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        var periodMessages = db.ChatMessages.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc);

        var uploadsInRange = await db.Documents.AsNoTracking()
            .CountAsync(x => x.UploadedAtUtc >= fromUtc && x.UploadedAtUtc < toUtc, ct);
        var messagesInRange = await periodMessages.CountAsync(ct);
        var questionsInRange = await periodMessages.CountAsync(x => x.Role == MessageRole.User, ct);
        var answersInRange = await periodMessages.CountAsync(x => x.Role == MessageRole.Assistant, ct);

        // Reject/error answers: assistant replies without citations (RAG reject hoặc lỗi AI).
        var rejectedAnswersInRange = await periodMessages.CountAsync(x =>
            x.Role == MessageRole.Assistant &&
            (x.CitationsJson == null || x.CitationsJson == "" || x.CitationsJson == "[]"), ct);

        var activeStudentsInRange = await periodMessages
            .Select(x => x.Session.StudentId)
            .Distinct()
            .CountAsync(ct);

        var activeCoursesInRange = await periodMessages
            .Select(x => x.Session.CourseId)
            .Distinct()
            .CountAsync(ct);

        var conversationsInRange = await periodMessages
            .Select(x => x.ConversationId)
            .Distinct()
            .CountAsync(ct);

        var newStudentsInRange = await db.Users.AsNoTracking()
            .CountAsync(x => x.Role == UserRole.Student && x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtc, ct);

        var fromDate = DateOnly.FromDateTime(fromUtc);
        var toDateExclusive = DateOnly.FromDateTime(toUtc);
        var tokenRows = await db.StudentDailyTokenUsages.AsNoTracking()
            .Where(x => x.UsageDate >= fromDate && x.UsageDate < toDateExclusive)
            .Select(x => new { x.UserId, x.UsageDate, x.TokensUsed })
            .ToListAsync(ct);
        var tokensUsedInRange = tokenRows.Sum(x => (long)x.TokensUsed);
        var studentsUsingTokensInRange = tokenRows.Select(x => x.UserId).Distinct().Count();

        var documentsByCourseRaw = await db.Documents.AsNoTracking()
            .GroupBy(x => x.Course.Code)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var messagesByCourseRaw = await periodMessages
            .GroupBy(x => x.Session.Course.Code)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var topDocumentsRaw = await periodMessages
            .Where(x => x.DocumentId != null && x.Role == MessageRole.User)
            .GroupBy(x => x.DocumentId!.Value)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync(ct);

        var topDocumentIds = topDocumentsRaw.Select(x => x.DocumentId).ToList();
        var documentNames = await db.Documents.AsNoTracking()
            .Where(x => topDocumentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.OriginalFileName, CourseCode = x.Course.Code })
            .ToListAsync(ct);
        var nameMap = documentNames.ToDictionary(x => x.Id, x => $"{x.CourseCode} · {x.OriginalFileName}");
        var topDocuments = topDocumentsRaw
            .Select(x => new NamedCountRow(nameMap.GetValueOrDefault(x.DocumentId, "Tài liệu đã xóa"), x.Count))
            .ToList();

        var failureReasonsRaw = await db.Documents.AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Failed)
            .GroupBy(x => x.FailureReason ?? "Không rõ nguyên nhân")
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync(ct);

        var periodMessageMeta = await periodMessages
            .Select(x => new { x.CreatedAtUtc, x.Role })
            .ToListAsync(ct);

        var messageDays = periodMessageMeta
            .GroupBy(x => x.CreatedAtUtc.Date)
            .Select(g => (g.Key, g.Count()))
            .ToList();
        var questionDays = periodMessageMeta
            .Where(x => x.Role == MessageRole.User)
            .GroupBy(x => x.CreatedAtUtc.Date)
            .Select(g => (g.Key, g.Count()))
            .ToList();

        var uploadDaysRaw = await db.Documents.AsNoTracking()
            .Where(x => x.UploadedAtUtc >= fromUtc && x.UploadedAtUtc < toUtc)
            .Select(x => x.UploadedAtUtc)
            .ToListAsync(ct);
        var uploadDays = uploadDaysRaw
            .GroupBy(x => x.Date)
            .Select(g => (g.Key, g.Count()))
            .ToList();

        var tokenDays = tokenRows
            .GroupBy(x => x.UsageDate.ToDateTime(TimeOnly.MinValue))
            .Select(g => (g.Key, g.Sum(x => x.TokensUsed)))
            .ToList();

        return new ReportSnapshot(
            CountRole(UserRole.Student),
            CountRole(UserRole.Lecturer),
            CountRole(UserRole.Admin),
            courseCount,
            documentCount,
            chunkCount,
            sessionCount,
            messageCount,
            StatusCount(DocumentStatus.Completed),
            StatusCount(DocumentStatus.Processing),
            StatusCount(DocumentStatus.Failed),
            uploadsInRange,
            messagesInRange,
            questionsInRange,
            answersInRange,
            rejectedAnswersInRange,
            activeStudentsInRange,
            activeCoursesInRange,
            conversationsInRange,
            newStudentsInRange,
            tokensUsedInRange,
            studentsUsingTokensInRange,
            documentsByCourseRaw.Select(x => new NamedCountRow(x.Name, x.Count)).ToList(),
            messagesByCourseRaw.Select(x => new NamedCountRow(x.Name, x.Count)).ToList(),
            topDocuments,
            failureReasonsRaw.Select(x => new NamedCountRow(Truncate(x.Name, 80), x.Count)).ToList(),
            FillDays(fromUtc, toUtc, messageDays),
            FillDays(fromUtc, toUtc, questionDays),
            FillDays(fromUtc, toUtc, uploadDays),
            FillDays(fromUtc, toUtc, tokenDays));
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max].TrimEnd() + "…";

    private static IReadOnlyList<DateCountRow> FillDays(DateTime fromUtc, DateTime toUtc, IEnumerable<(DateTime Day, int Count)> raw)
    {
        var map = raw.ToDictionary(x => DateOnly.FromDateTime(x.Day), x => x.Count);
        var start = DateOnly.FromDateTime(fromUtc);
        var endExclusive = DateOnly.FromDateTime(toUtc);
        var rows = new List<DateCountRow>();
        for (var day = start; day < endExclusive; day = day.AddDays(1))
            rows.Add(new DateCountRow(day, map.GetValueOrDefault(day)));
        return rows;
    }
}

public sealed class BenchmarkRepository(ChatBoxDbContext db) : IBenchmarkRepository
{
    public async Task AddRunAsync(BenchmarkRun run, CancellationToken ct = default)
    {
        db.BenchmarkRuns.Add(run);
        await db.SaveChangesAsync(ct);
    }

    public Task<BenchmarkRun?> FindRunAsync(Guid runId, CancellationToken ct = default)
        => db.BenchmarkRuns.AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.Document)
            .Include(x => x.StartedBy)
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == runId, ct);

    public async Task<IReadOnlyList<BenchmarkRun>> ListRecentRunsAsync(int take = 20, CancellationToken ct = default)
        => await db.BenchmarkRuns.AsNoTracking()
            .Include(x => x.Course)
            .Include(x => x.Document)
            .Include(x => x.StartedBy)
            .Include(x => x.Results)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(take)
            .ToListAsync(ct);
}

public sealed class StudentTokenUsageRepository(ChatBoxDbContext db) : IStudentTokenUsageRepository
{
    public async Task<int> GetUsedTokensAsync(Guid userId, DateOnly usageDate, CancellationToken ct = default)
    {
        var row = await db.StudentDailyTokenUsages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.UsageDate == usageDate, ct);
        return row?.TokensUsed ?? 0;
    }

    public async Task AddTokensAsync(Guid userId, DateOnly usageDate, int tokens, CancellationToken ct = default)
    {
        if (tokens <= 0) return;
        var row = await db.StudentDailyTokenUsages
            .FirstOrDefaultAsync(x => x.UserId == userId && x.UsageDate == usageDate, ct);
        if (row is null)
        {
            db.StudentDailyTokenUsages.Add(new StudentDailyTokenUsage
            {
                UserId = userId,
                UsageDate = usageDate,
                TokensUsed = tokens
            });
        }
        else
        {
            row.TokensUsed += tokens;
        }
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SubscriptionRepository(ChatBoxDbContext db) : ISubscriptionRepository
{
    public async Task<IReadOnlyList<SubscriptionPackage>> ListActivePackagesAsync(CancellationToken ct = default)
        => await db.SubscriptionPackages.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public Task<SubscriptionPackage?> FindPackageByIdAsync(Guid id, CancellationToken ct = default)
        => db.SubscriptionPackages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SubscriptionPackage?> FindPackageByCodeAsync(string code, CancellationToken ct = default)
        => db.SubscriptionPackages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task EnsurePackagesSeededAsync(IEnumerable<SubscriptionPackage> packages, CancellationToken ct = default)
    {
        foreach (var package in packages)
        {
            if (await db.SubscriptionPackages.AnyAsync(x => x.Code == package.Code, ct))
                continue;
            db.SubscriptionPackages.Add(package);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserSubscription?> GetActiveSubscriptionAsync(Guid userId, DateTime utcNow, CancellationToken ct = default)
        => await db.UserSubscriptions.AsNoTracking()
            .Include(x => x.Package)
            .Where(x => x.UserId == userId && x.Status == SubscriptionStatus.Active)
            .Where(x => x.EndsAtUtc == null || x.EndsAtUtc > utcNow)
            .OrderByDescending(x => x.StartsAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task AddPaymentOrderAsync(PaymentOrder order, CancellationToken ct = default)
    {
        db.PaymentOrders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    public Task<PaymentOrder?> FindPaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default)
        => db.PaymentOrders
            .Include(x => x.Package)
            .FirstOrDefaultAsync(x => x.OrderCode == orderCode, ct);

    public async Task UpdatePaymentOrderAsync(PaymentOrder order, CancellationToken ct = default)
    {
        db.PaymentOrders.Update(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task ActivateSubscriptionAsync(UserSubscription subscription, CancellationToken ct = default)
    {
        db.UserSubscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
    }

    public async Task ExpireActiveSubscriptionsAsync(Guid userId, DateTime utcNow, CancellationToken ct = default)
    {
        var active = await db.UserSubscriptions
            .Where(x => x.UserId == userId && x.Status == SubscriptionStatus.Active)
            .ToListAsync(ct);
        foreach (var item in active)
        {
            item.Status = SubscriptionStatus.Expired;
            item.EndsAtUtc ??= utcNow;
        }
        if (active.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AdminStudentAccountRow>> ListStudentAccountsAsync(
        DateOnly usageDate,
        DateTime utcNow,
        CancellationToken ct = default)
    {
        var students = await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Student)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

        var free = await db.SubscriptionPackages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == "FREE", ct);

        var subscriptions = await db.UserSubscriptions.AsNoTracking()
            .Include(x => x.Package)
            .Where(x => x.Status == SubscriptionStatus.Active)
            .Where(x => x.EndsAtUtc == null || x.EndsAtUtc > utcNow)
            .ToListAsync(ct);

        var usage = await db.StudentDailyTokenUsages.AsNoTracking()
            .Where(x => x.UsageDate == usageDate)
            .ToListAsync(ct);

        var paidOrders = await db.PaymentOrders.AsNoTracking()
            .Where(x => x.Status == PaymentOrderStatus.Paid)
            .GroupBy(x => x.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Total = g.Sum(x => x.AmountVnd) })
            .ToListAsync(ct);

        var subByUser = subscriptions
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StartsAtUtc).First());
        var usageByUser = usage.ToDictionary(x => x.UserId, x => x.TokensUsed);
        var paidByUser = paidOrders.ToDictionary(x => x.UserId, x => (x.Count, x.Total));

        return students.Select(s =>
        {
            subByUser.TryGetValue(s.Id, out var sub);
            var packageCode = sub?.Package.Code ?? free?.Code ?? "FREE";
            var packageName = sub?.Package.Name ?? free?.Name ?? "Free";
            var questions = sub?.Package.ChatQuestionsPerDay ?? free?.ChatQuestionsPerDay ?? 10;
            paidByUser.TryGetValue(s.Id, out var paid);
            return new AdminStudentAccountRow(
                s.Id,
                s.Code,
                s.FullName,
                s.Email,
                s.CreatedAtUtc,
                packageCode,
                packageName,
                questions,
                sub?.EndsAtUtc,
                usageByUser.GetValueOrDefault(s.Id),
                paid.Count,
                paid.Total);
        }).ToList();
    }

    public async Task<IReadOnlyList<PaymentOrder>> ListRecentPaymentsAsync(int take = 30, CancellationToken ct = default)
        => await db.PaymentOrders.AsNoTracking()
            .Include(x => x.Package)
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<(decimal ProRevenue, decimal PreRevenue, decimal TotalRevenue)> GetPaidRevenueAsync(CancellationToken ct = default)
    {
        var paid = await db.PaymentOrders.AsNoTracking()
            .Include(x => x.Package)
            .Where(x => x.Status == PaymentOrderStatus.Paid)
            .ToListAsync(ct);

        var pro = paid.Where(x => x.Package.Code == "PRO").Sum(x => x.AmountVnd);
        var pre = paid.Where(x => x.Package.Code == "PRE").Sum(x => x.AmountVnd);
        return (pro, pre, paid.Sum(x => x.AmountVnd));
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
