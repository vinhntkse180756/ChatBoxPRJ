using System.Text.Json;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ChatBoxPRJ.Pages.Admin;

public sealed class BenchmarksModel(
    IBenchmarkService benchmarks,
    ICourseService courses,
    IDocumentService documents,
    IWebHostEnvironment env) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CourseId { get; set; }
    [BindProperty] public Guid? DocumentId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? RunId { get; set; }

    public IReadOnlyList<SelectListItem> CourseOptions { get; private set; } = [];
    public IReadOnlyList<SelectListItem> DocumentOptions { get; private set; } = [];
    public IReadOnlyList<BenchmarkRunSummaryDto> RecentRuns { get; private set; } = [];
    public BenchmarkRunDto? SelectedRun { get; private set; }
    public string ChartJson { get; private set; } = "{}";

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (RunId.HasValue)
            SelectedRun = await benchmarks.GetRunAsync(RunId.Value, HttpContext.RequestAborted);
        else if (RecentRuns.Count > 0)
            SelectedRun = await benchmarks.GetRunAsync(RecentRuns[0].Id, HttpContext.RequestAborted);
        ChartJson = BuildChartJson(SelectedRun);
    }

    public async Task<IActionResult> OnPostRunAsync()
    {
        var testSetPath = Path.GetFullPath(
            BenchmarkScope.TestSetRelativePath,
            env.ContentRootPath);

        var result = await benchmarks.RunAsync(
            User.UserId(),
            testSetPath,
            CourseId,
            DocumentId,
            HttpContext.RequestAborted);

        Flash = result.Message;
        FlashType = result.Success ? "success" : "danger";
        if (result.Success && result.Run is not null)
            return RedirectToPage(new { runId = result.Run.Id, courseId = CourseId });

        await LoadAsync();
        ChartJson = BuildChartJson(SelectedRun);
        return Page();
    }

    private async Task LoadAsync()
    {
        RecentRuns = await benchmarks.ListRecentAsync(20, HttpContext.RequestAborted);
        var allCourses = await courses.ListAsync(HttpContext.RequestAborted);

        if (!CourseId.HasValue)
        {
            var defaultCourse = allCourses.FirstOrDefault(x =>
                x.Code.Equals(BenchmarkScope.DefaultCourseCode, StringComparison.OrdinalIgnoreCase))
                ?? allCourses.FirstOrDefault();
            CourseId = defaultCourse?.Id;
        }

        CourseOptions = allCourses
            .Select(x => new SelectListItem($"{x.Code} — {x.Name}", x.Id.ToString(), CourseId == x.Id))
            .ToList();

        if (CourseId.HasValue)
        {
            var docs = await documents.ListAsync(CourseId, completedOnly: true, HttpContext.RequestAborted);
            DocumentOptions = docs
                .Select(x => new SelectListItem(x.FileName, x.Id.ToString(), DocumentId == x.Id))
                .ToList();
            if (!DocumentId.HasValue)
                DocumentId = docs.FirstOrDefault()?.Id;
        }
    }

    private static string BuildChartJson(BenchmarkRunDto? run)
    {
        if (run is null) return "{}";
        var results = run.Results;
        var missCount = Math.Max(0, run.QuestionCount - run.HitCount);
        return JsonSerializer.Serialize(new
        {
            latency = new
            {
                labels = results.Select(x => x.QuestionId).ToArray(),
                values = results.Select(x => x.LatencyMs).ToArray()
            },
            hitReject = new
            {
                labels = new[] { "Hit", "Reject", "Miss (no hit)" },
                values = new[] { run.HitCount, run.RejectCount, missCount }
            },
            topScore = new
            {
                labels = results.Select(x => x.QuestionId).ToArray(),
                values = results.Select(x => x.TopScore).ToArray()
            }
        });
    }
}
