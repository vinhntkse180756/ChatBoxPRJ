using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Admin;

public sealed class ReportsModel(IReportService reports) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateOnly FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateOnly ToDate { get; set; }

    public ReportDashboardDto Dashboard { get; private set; } = null!;
    public string ChartJson { get; private set; } = "{}";
    public string PeriodLabel { get; private set; } = "";

    public async Task OnGetAsync()
    {
        NormalizeDates();
        Dashboard = await reports.GetAdminDashboardAsync(FromDate, ToDate, HttpContext.RequestAborted);
        PeriodLabel = $"{FromDate:dd/MM/yyyy} → {ToDate:dd/MM/yyyy}";
        ChartJson = BuildChartJson(Dashboard);
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        NormalizeDates();
        var d = await reports.GetAdminDashboardAsync(FromDate, ToDate, HttpContext.RequestAborted);
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value");
        sb.AppendLine(Csv("Kỳ báo cáo", $"{d.FromDate:yyyy-MM-dd} → {d.ToDate:yyyy-MM-dd}"));
        sb.AppendLine(Csv("SV active (kỳ)", d.ActiveStudentsInRange));
        sb.AppendLine(Csv("Tỷ lệ SV active (%)", d.StudentActivationRate));
        sb.AppendLine(Csv("Câu hỏi (kỳ)", d.QuestionsInRange));
        sb.AppendLine(Csv("Câu trả lời (kỳ)", d.AnswersInRange));
        sb.AppendLine(Csv("Reject/không có citation (kỳ)", d.RejectedAnswersInRange));
        sb.AppendLine(Csv("Reject rate (%)", d.RejectRate));
        sb.AppendLine(Csv("Cuộc hội thoại (kỳ)", d.ConversationsInRange));
        sb.AppendLine(Csv("TB tin nhắn / hội thoại", d.AvgMessagesPerConversation));
        sb.AppendLine(Csv("Token AI đã dùng (kỳ)", d.TokensUsedInRange));
        sb.AppendLine(Csv("SV dùng token (kỳ)", d.StudentsUsingTokensInRange));
        sb.AppendLine(Csv("Upload (kỳ)", d.UploadsInRange));
        sb.AppendLine(Csv("SV đăng ký mới (kỳ)", d.NewStudentsInRange));
        sb.AppendLine(Csv("Tài liệu Completed", d.DocumentsCompleted));
        sb.AppendLine(Csv("Tài liệu Processing", d.DocumentsProcessing));
        sb.AppendLine(Csv("Tài liệu Failed", d.DocumentsFailed));
        sb.AppendLine(Csv("Index success rate (%)", d.IndexSuccessRate));
        sb.AppendLine();
        sb.AppendLine("TopDocuments,Questions");
        foreach (var row in d.TopDocuments)
            sb.AppendLine(Csv(row.Name, row.Count));
        sb.AppendLine();
        sb.AppendLine("FailureReason,Count");
        foreach (var row in d.FailureReasons)
            sb.AppendLine(Csv(row.Name, row.Count));

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"report_{d.FromDate:yyyyMMdd}_{d.ToDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private void NormalizeDates()
    {
        if (ToDate == default) ToDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (FromDate == default) FromDate = ToDate.AddDays(-29);
        if (ToDate < FromDate) (FromDate, ToDate) = (ToDate, FromDate);
        if ((ToDate.DayNumber - FromDate.DayNumber) > 90)
            FromDate = ToDate.AddDays(-90);
    }

    private static string BuildChartJson(ReportDashboardDto d) => JsonSerializer.Serialize(new
    {
        questionsByDay = new
        {
            labels = d.QuestionsByDay.Select(x => x.Date).ToArray(),
            values = d.QuestionsByDay.Select(x => x.Count).ToArray()
        },
        messagesByDay = new
        {
            labels = d.MessagesByDay.Select(x => x.Date).ToArray(),
            values = d.MessagesByDay.Select(x => x.Count).ToArray()
        },
        uploadsByDay = new
        {
            labels = d.UploadsByDay.Select(x => x.Date).ToArray(),
            values = d.UploadsByDay.Select(x => x.Count).ToArray()
        },
        tokensByDay = new
        {
            labels = d.TokensByDay.Select(x => x.Date).ToArray(),
            values = d.TokensByDay.Select(x => x.Count).ToArray()
        },
        documentsByCourse = new
        {
            labels = d.DocumentsByCourse.Select(x => x.Name).ToArray(),
            values = d.DocumentsByCourse.Select(x => x.Count).ToArray()
        },
        messagesByCourse = new
        {
            labels = d.MessagesByCourse.Select(x => x.Name).ToArray(),
            values = d.MessagesByCourse.Select(x => x.Count).ToArray()
        },
        documentStatus = new
        {
            labels = new[] { "Completed", "Processing", "Failed" },
            values = new[] { d.DocumentsCompleted, d.DocumentsProcessing, d.DocumentsFailed }
        },
        topDocuments = new
        {
            labels = d.TopDocuments.Select(x => x.Name.Length > 36 ? x.Name[..36] + "…" : x.Name).ToArray(),
            values = d.TopDocuments.Select(x => x.Count).ToArray()
        }
    });

    private static string Csv(string name, object value)
    {
        var escaped = name.Replace("\"", "\"\"", StringComparison.Ordinal);
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return $"\"{escaped}\",{text}";
    }
}
