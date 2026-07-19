using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using DataAcessLayer.Interfaces;

namespace BusinessLogic.Services;

public sealed class ReportService(IReportRepository reports) : IReportService
{
    public async Task<ReportDashboardDto> GetAdminDashboardAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var snap = await reports.GetSnapshotAsync(fromUtc, toUtc, ct);

        var indexSuccessRate = snap.DocumentCount == 0
            ? 0
            : Math.Round(100.0 * snap.DocumentsCompleted / snap.DocumentCount, 1);

        var rejectRate = snap.AnswersInRange == 0
            ? 0
            : Math.Round(100.0 * snap.RejectedAnswersInRange / snap.AnswersInRange, 1);

        var studentActivationRate = snap.StudentCount == 0
            ? 0
            : Math.Round(100.0 * snap.ActiveStudentsInRange / snap.StudentCount, 1);

        var avgMessagesPerConversation = snap.ConversationsInRange == 0
            ? 0
            : Math.Round((double)snap.MessagesInRange / snap.ConversationsInRange, 1);

        static List<NamedCountDto> MapNamed(IReadOnlyList<NamedCountRow> rows)
            => rows.Select(x => new NamedCountDto(x.Name, x.Count)).ToList();

        static List<DateCountDto> MapDays(IReadOnlyList<DateCountRow> rows)
            => rows.Select(x => new DateCountDto(x.Date.ToString("yyyy-MM-dd"), x.Count)).ToList();

        return new ReportDashboardDto(
            fromDate,
            toDate,
            snap.StudentCount,
            snap.LecturerCount,
            snap.AdminCount,
            snap.CourseCount,
            snap.DocumentCount,
            snap.ChunkCount,
            snap.ChatSessionCount,
            snap.ChatMessageCount,
            snap.DocumentsCompleted,
            snap.DocumentsProcessing,
            snap.DocumentsFailed,
            indexSuccessRate,
            snap.UploadsInRange,
            snap.MessagesInRange,
            snap.QuestionsInRange,
            snap.AnswersInRange,
            snap.RejectedAnswersInRange,
            rejectRate,
            snap.ActiveStudentsInRange,
            studentActivationRate,
            snap.ActiveCoursesInRange,
            snap.ConversationsInRange,
            avgMessagesPerConversation,
            snap.NewStudentsInRange,
            snap.TokensUsedInRange,
            snap.StudentsUsingTokensInRange,
            MapNamed(snap.DocumentsByCourse),
            MapNamed(snap.MessagesByCourse),
            MapNamed(snap.TopDocuments),
            MapNamed(snap.FailureReasons),
            MapDays(snap.MessagesByDay),
            MapDays(snap.QuestionsByDay),
            MapDays(snap.UploadsByDay),
            MapDays(snap.TokensByDay));
    }
}
