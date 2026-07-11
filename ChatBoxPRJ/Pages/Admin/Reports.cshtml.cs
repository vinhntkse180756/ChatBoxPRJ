using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Admin;

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

    public async Task OnGetAsync()
    {
        if (ToDate == default) ToDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (FromDate == default) FromDate = ToDate.AddDays(-29);
        if ((ToDate.DayNumber - FromDate.DayNumber) > 90)
            FromDate = ToDate.AddDays(-90);

        Dashboard = await reports.GetAdminDashboardAsync(FromDate, ToDate, HttpContext.RequestAborted);
        ChartJson = JsonSerializer.Serialize(new
        {
            messagesByDay = new
            {
                labels = Dashboard.MessagesByDay.Select(x => x.Date).ToArray(),
                values = Dashboard.MessagesByDay.Select(x => x.Count).ToArray()
            },
            uploadsByDay = new
            {
                labels = Dashboard.UploadsByDay.Select(x => x.Date).ToArray(),
                values = Dashboard.UploadsByDay.Select(x => x.Count).ToArray()
            },
            documentsByCourse = new
            {
                labels = Dashboard.DocumentsByCourse.Select(x => x.Name).ToArray(),
                values = Dashboard.DocumentsByCourse.Select(x => x.Count).ToArray()
            },
            messagesByCourse = new
            {
                labels = Dashboard.MessagesByCourse.Select(x => x.Name).ToArray(),
                values = Dashboard.MessagesByCourse.Select(x => x.Count).ToArray()
            },
            documentStatus = new
            {
                labels = new[] { "Completed", "Processing", "Failed" },
                values = new[] { Dashboard.DocumentsCompleted, Dashboard.DocumentsProcessing, Dashboard.DocumentsFailed }
            }
        });
    }
}
