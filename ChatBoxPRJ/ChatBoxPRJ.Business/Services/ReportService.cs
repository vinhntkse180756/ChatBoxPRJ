using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Interfaces;

namespace ChatBoxPRJ.Business.Services;

public sealed class ReportService(IReportRepository reports) : IReportService
{
    public async Task<ReportDashboardDto> GetAdminDashboardAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        if (toDate < fromDate)
            (fromDate, toDate) = (toDate, fromDate);

        // Inclusive end date → exclusive UTC upper bound.
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var snap = await reports.GetSnapshotAsync(fromUtc, toUtc, ct);

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
            snap.UploadsInRange,
            snap.MessagesInRange,
            snap.DocumentsByCourse.Select(x => new NamedCountDto(x.Name, x.Count)).ToList(),
            snap.MessagesByCourse.Select(x => new NamedCountDto(x.Name, x.Count)).ToList(),
            snap.MessagesByDay.Select(x => new DateCountDto(x.Date.ToString("yyyy-MM-dd"), x.Count)).ToList(),
            snap.UploadsByDay.Select(x => new DateCountDto(x.Date.ToString("yyyy-MM-dd"), x.Count)).ToList());
    }
}
