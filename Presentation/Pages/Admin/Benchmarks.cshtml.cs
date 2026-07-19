using System.Text.Json;
using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using BusinessLogic.Options;
using Presentation.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Presentation.Pages.Admin;

public sealed class BenchmarksModel(
    IBenchmarkService benchmarks,
    ICourseService courses,
    IDocumentService documents,
    IWebHostEnvironment env) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CourseId { get; set; }
    [BindProperty] public List<Guid> SelectedDocumentIds { get; set; } = [];
    [BindProperty(SupportsGet = true)] public Guid? RunId { get; set; }
    [BindProperty] public List<string> SelectedModels { get; set; } = [];

    public IReadOnlyList<SelectListItem> CourseOptions { get; private set; } = [];
    public IReadOnlyList<DocumentDto> CompletedDocuments { get; private set; } = [];
    public IReadOnlyList<BenchmarkRunSummaryDto> RecentRuns { get; private set; } = [];
    public BenchmarkRunDto? SelectedRun { get; private set; }
    public string ChartJson { get; private set; } = "{}";

    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    public static readonly IReadOnlyList<string> AvailableModels = new[]
    {
        "gemini-2.5-flash",
        "gemini-2.5-pro",
        "gemini-1.5-flash",
        "gemini-1.5-pro",
        "gpt-4o (GitHub)",
        "gpt-4o-mini (GitHub)",
        "Local Mock"
    };

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
            SelectedDocumentIds,
            SelectedModels.Count > 0 ? SelectedModels : new List<string> { "gemini-2.5-flash" },
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
            CompletedDocuments = await documents.ListAsync(CourseId, completedOnly: true, HttpContext.RequestAborted);
        }
    }

    private static string BuildChartJson(BenchmarkRunDto? run)
    {
        if (run is null) return "{}";
        var results = run.Results;
        var models = results.Select(x => x.ModelName).Distinct().ToList();
        var questionIds = results.Select(x => x.QuestionId).Distinct().OrderBy(x => x).ToList();

        // Distinct colors for up to 8 models
        var borderColors = new[] { "#176b52", "#1d63b8", "#b82e1d", "#8c1db8", "#b88c1d", "#1db8b8", "#5c5c5c", "#ff6b6b" };
        var bgColors = new[] { "rgba(23,107,82,0.65)", "rgba(29,99,184,0.65)", "rgba(184,46,29,0.65)", "rgba(140,29,184,0.65)", "rgba(184,140,29,0.65)", "rgba(29,184,184,0.65)", "rgba(92,92,92,0.65)", "rgba(255,107,107,0.65)" };

        var latencyDatasets = models.Select((model, idx) => new
        {
            label = model,
            data = questionIds.Select(qId => 
                results.FirstOrDefault(r => r.QuestionId == qId && r.ModelName == model)?.LatencyMs ?? 0
            ).ToArray(),
            backgroundColor = bgColors[idx % bgColors.Length],
            borderColor = borderColors[idx % borderColors.Length],
            borderRadius = 4
        }).ToArray();

        var ttftDatasets = models.Select((model, idx) => new
        {
            label = model,
            data = questionIds.Select(qId => 
                results.FirstOrDefault(r => r.QuestionId == qId && r.ModelName == model)?.TtftMs ?? 0
            ).ToArray(),
            backgroundColor = bgColors[idx % bgColors.Length],
            borderColor = borderColors[idx % borderColors.Length],
            borderRadius = 4
        }).ToArray();

        var chunksSecDatasets = models.Select((model, idx) => new
        {
            label = model,
            data = questionIds.Select(qId => 
                results.FirstOrDefault(r => r.QuestionId == qId && r.ModelName == model)?.ChunksPerSecond ?? 0
            ).ToArray(),
            backgroundColor = bgColors[idx % bgColors.Length],
            borderColor = borderColors[idx % borderColors.Length],
            borderRadius = 4
        }).ToArray();

        var topScoreDatasets = models.Select((model, idx) => new
        {
            label = model,
            data = questionIds.Select(qId => 
                results.FirstOrDefault(r => r.QuestionId == qId && r.ModelName == model)?.TopScore ?? 0
            ).ToArray(),
            borderColor = borderColors[idx % borderColors.Length],
            backgroundColor = bgColors[idx % bgColors.Length].Replace("0.65", "0.1"),
            fill = true,
            tension = 0.3,
            pointRadius = 3
        }).ToArray();

        var missCount = Math.Max(0, run.QuestionCount - run.HitCount);

        return JsonSerializer.Serialize(new
        {
            latency = new
            {
                labels = questionIds.ToArray(),
                datasets = latencyDatasets
            },
            ttft = new
            {
                labels = questionIds.ToArray(),
                datasets = ttftDatasets
            },
            chunksSec = new
            {
                labels = questionIds.ToArray(),
                datasets = chunksSecDatasets
            },
            topScore = new
            {
                labels = questionIds.ToArray(),
                datasets = topScoreDatasets
            },
            hitReject = new
            {
                labels = new[] { "Hit", "Reject", "Miss" },
                values = new[] { run.HitCount, run.RejectCount, missCount }
            }
        });
    }
}
