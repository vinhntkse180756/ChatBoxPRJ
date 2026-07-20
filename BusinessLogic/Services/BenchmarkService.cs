using AIService.Services;
using AIService.Models;
using AIService.Options;
using System.Diagnostics;
using System.Text.Json;
using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using BusinessLogic.Options;
using DataAcessLayer.Interfaces;
using BusinessObjects.Entities;
using DataDocumentStatus = BusinessObjects.Entities.DocumentStatus;
using DataUserRole = BusinessObjects.Entities.UserRole;

namespace BusinessLogic.Services;

public sealed class BenchmarkService(
    ICourseRepository courses,
    IDocumentRepository documents,
    IUserRepository users,
    IBenchmarkRepository benchmarks,
    IEmbeddingService embeddings,
    IVectorStore vectors,
    IAnswerGenerator answers,
    RagOptions rag,
    AIService.Options.AiOptions aiOptions) : IBenchmarkService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<(bool Success, string Message, BenchmarkRunDto? Run)> RunAsync(
        Guid startedByAdminId,
        string testSetPath,
        Guid? courseId = null,
        IReadOnlyList<Guid>? selectedDocumentIds = null,
        IReadOnlyList<string>? selectedModels = null,
        CancellationToken ct = default)
    {
        var admin = await users.FindByIdAsync(startedByAdminId, ct);
        if (admin is null || admin.Role != DataUserRole.Admin)
            return (false, "Chỉ Admin được chạy benchmark.", null);

        if (string.IsNullOrWhiteSpace(testSetPath) || !File.Exists(testSetPath))
            return (false, $"Không tìm thấy test set: {testSetPath}", null);

        BenchmarkTestSetFile testSet;
        try
        {
            testSet = JsonSerializer.Deserialize<BenchmarkTestSetFile>(await File.ReadAllTextAsync(testSetPath, ct), JsonOptions)
                ?? throw new JsonException("Empty test set.");
        }
        catch (Exception ex)
        {
            return (false, $"Không đọc được test set JSON: {ex.Message}", null);
        }

        if (testSet.Questions.Count == 0)
            return (false, "Test set không có câu hỏi.", null);

        Course? course;
        if (courseId.HasValue)
            course = await courses.FindAsync(courseId.Value, ct);
        else
        {
            var code = string.IsNullOrWhiteSpace(testSet.CourseCode)
                ? BenchmarkScope.DefaultCourseCode
                : testSet.CourseCode.Trim().ToUpperInvariant();
            course = (await courses.ListAsync(ct)).FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        if (course is null)
            return (false, "Không tìm thấy môn học cho benchmark.", null);

        var docsToRun = new List<LearningDocument>();
        if (selectedDocumentIds != null && selectedDocumentIds.Count > 0)
        {
            foreach (var docId in selectedDocumentIds)
            {
                var doc = await documents.FindAsync(docId, ct);
                if (doc != null && doc.CourseId == course.Id && doc.Status == DataDocumentStatus.Completed)
                {
                    docsToRun.Add(doc);
                }
            }
        }
        else
        {
            var completedDocs = await documents.ListAsync(course.Id, completedOnly: true, ct);
            if (completedDocs.Count > 0)
            {
                docsToRun.Add(completedDocs[0]);
            }
        }

        if (docsToRun.Count == 0)
        {
            return (false, $"Môn {course.Code} chưa có tài liệu Completed nào để chạy benchmark.", null);
        }

        var run = new BenchmarkRun
        {
            CourseId = course.Id,
            DocumentId = docsToRun[0].Id,
            StartedById = startedByAdminId,
            StartedAtUtc = DateTime.UtcNow
        };

        var models = selectedModels != null && selectedModels.Count > 0
            ? selectedModels
            : new[] { aiOptions.ChatModel ?? "gemini-3.1-flash-lite" };

        run.ModelName = string.Join(", ", models);

        var resultRows = new List<BenchmarkResult>();
        foreach (var document in docsToRun)
        {
            foreach (var modelName in models)
            {
                // Nhãn hiển thị gộp thông số cấu hình RAG: Model - File (Chunking, Embedding)
                var runModelLabel = $"{modelName} - {document.OriginalFileName} ({document.ChunkingStrategy}, {document.EmbeddingModel})";

                foreach (var item in testSet.Questions)
                {
                    var sw = Stopwatch.StartNew();
                    var (rejected, answer, citations, topScore, ttftMs, chunksPerSecond, chunkCount) = 
                        await EvaluateQuestionStreamAsync(course.Id, document.Id, item.Question, modelName, ct);
                    sw.Stop();

                    var citationCount = citations.Count;
                    var hit = BenchmarkScope.IsHit(rejected, citationCount);
                    var expectationMet = item.ExpectReject
                        ? BenchmarkScope.IsReject(rejected)
                        : hit;

                    resultRows.Add(new BenchmarkResult
                    {
                        RunId = run.Id,
                        QuestionId = item.Id,
                        Question = item.Question,
                        ExpectReject = item.ExpectReject,
                        Rejected = rejected,
                        Hit = hit,
                        ExpectationMet = expectationMet,
                        LatencyMs = sw.ElapsedMilliseconds,
                        TopScore = topScore,
                        CitationCount = citationCount,
                        Answer = answer,
                        ModelName = runModelLabel,
                        TtftMs = ttftMs,
                        ChunksPerSecond = chunksPerSecond,
                        ChunkCount = chunkCount
                    });
                }
            }
        }

        run.Results = resultRows;
        run.QuestionCount = resultRows.Count;
        run.HitCount = resultRows.Count(x => x.Hit);
        run.RejectCount = resultRows.Count(x => x.Rejected);
        run.AverageLatencyMs = resultRows.Count == 0 ? 0 : resultRows.Average(x => x.LatencyMs);
        run.AverageTopScore = resultRows.Count == 0 ? 0 : resultRows.Average(x => x.TopScore);
        run.AverageTtftMs = resultRows.Count == 0 ? 0 : resultRows.Average(x => x.TtftMs);
        run.AverageChunksPerSecond = resultRows.Count == 0 ? 0 : resultRows.Average(x => x.ChunksPerSecond);
        run.FinishedAtUtc = DateTime.UtcNow;

        await benchmarks.AddRunAsync(run, ct);
        var saved = await benchmarks.FindRunAsync(run.Id, ct);
        return (true, $"Đã chạy {run.QuestionCount} câu benchmark cho {models.Count} model.", saved is null ? null : MapRun(saved));
    }

    public async Task<BenchmarkRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await benchmarks.FindRunAsync(runId, ct);
        return run is null ? null : MapRun(run);
    }

    public async Task<IReadOnlyList<BenchmarkRunSummaryDto>> ListRecentAsync(int take = 20, CancellationToken ct = default)
    {
        var runs = await benchmarks.ListRecentRunsAsync(take, ct);
        return runs.Select(MapSummary).ToList();
    }

    private async Task<(bool Rejected, string Answer, IReadOnlyList<CitationDto> Citations, double TopScore, long TtftMs, double ChunksPerSecond, int ChunkCount)> EvaluateQuestionStreamAsync(
        Guid courseId,
        Guid documentId,
        string question,
        string modelName,
        CancellationToken ct)
    {
        var query = await embeddings.EmbedAsync(question, EmbeddingTask.Query, null, ct);
        var found = await vectors.SearchAsync(courseId, documentId, query, rag.TopK, ct);
        var topScore = found.Count == 0 ? 0 : found.Max(x => x.Score);

        if (found.Count == 0 || topScore < rag.SimilarityThreshold)
        {
            return (true,
                "Xin lỗi, câu hỏi của bạn không nằm trong phạm vi tài liệu học tập của môn học này.",
                Array.Empty<CitationDto>(),
                topScore,
                0,
                0,
                0);
        }

        var answerContext = found.Select(x => new AnswerChunkContext(
            x.ChunkId, x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber, x.Content, x.Score)).ToList();

        long ttftMs = 0;
        int chunkCount = 0;
        string answer = "";
        var sw = Stopwatch.StartNew();

        try
        {
            await foreach (var chunk in answers.GenerateStreamAsync(question, answerContext, [], modelName, ct))
            {
                if (chunkCount == 0)
                {
                    ttftMs = chunk.ElapsedMsSinceStart;
                }
                chunkCount++;
                answer += chunk.ChunkText;
            }
        }
        catch (Exception ex)
        {
            answer = $"[Lỗi khi sinh kết quả từ model {modelName}: {ex.Message}]";
        }
        
        sw.Stop();
        var totalTimeMs = sw.ElapsedMilliseconds;
        var activeGenTimeMs = totalTimeMs - ttftMs;
        double chunksPerSec = activeGenTimeMs > 0 ? (chunkCount * 1000.0 / activeGenTimeMs) : 0;

        var citations = found.Select(x => new CitationDto(
            x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber,
            x.Content.Length <= 320 ? x.Content : x.Content[..320] + "…")).ToList();

        return (false, answer, citations, topScore, ttftMs, chunksPerSec, chunkCount);
    }

    private static BenchmarkRunDto MapRun(BenchmarkRun run)
    {
        var results = run.Results
            .OrderBy(x => x.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BenchmarkResultDto(
                x.QuestionId, x.Question, x.ExpectReject, x.Rejected, x.Hit, x.ExpectationMet,
                x.LatencyMs, x.TopScore, x.CitationCount, x.Answer,
                x.ModelName, x.TtftMs, x.ChunksPerSecond, x.ChunkCount))
            .ToList();

        return new BenchmarkRunDto(
            run.Id,
            run.CourseId,
            run.Course?.Code ?? "",
            run.DocumentId,
            run.Document?.OriginalFileName ?? "",
            run.StartedBy?.Code ?? "",
            run.StartedAtUtc,
            run.FinishedAtUtc,
            run.QuestionCount,
            run.HitCount,
            run.RejectCount,
            run.AverageLatencyMs,
            run.AverageTopScore,
            results.Count(x => x.ExpectationMet),
            run.ModelName,
            run.AverageTtftMs,
            run.AverageChunksPerSecond,
            results);
    }

    private static BenchmarkRunSummaryDto MapSummary(BenchmarkRun run)
        => new(
            run.Id,
            run.Course?.Code ?? "",
            run.Document?.OriginalFileName ?? "",
            run.StartedAtUtc,
            run.QuestionCount,
            run.HitCount,
            run.RejectCount,
            run.AverageLatencyMs,
            run.AverageTopScore,
            run.Results?.Count(x => x.ExpectationMet) ?? 0,
            run.ModelName,
            run.AverageTtftMs,
            run.AverageChunksPerSecond);

    private sealed class BenchmarkTestSetFile
    {
        public string CourseCode { get; set; } = BenchmarkScope.DefaultCourseCode;
        public List<BenchmarkQuestionItem> Questions { get; set; } = [];
    }

    private sealed class BenchmarkQuestionItem
    {
        public string Id { get; set; } = "";
        public string Question { get; set; } = "";
        public bool ExpectReject { get; set; }
        public List<string> ExpectedKeywords { get; set; } = [];
    }
}
