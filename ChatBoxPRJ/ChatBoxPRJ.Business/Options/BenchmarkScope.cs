namespace ChatBoxPRJ.Business.Options;

/// <summary>
/// Scope đã chốt cho flow Benchmarks/Metrics.
/// Các bước sau (test set, entity, service, UI) phải bám theo file này.
/// </summary>
public static class BenchmarkScope
{
    /// <summary>Chỉ Admin được chạy và xem benchmark.</summary>
    public const string AllowedRole = "Admin";

    /// <summary>Page Razor sẽ tạo ở bước UI: /Admin/Benchmarks.</summary>
    public const string AdminPagePath = "/Admin/Benchmarks";

    /// <summary>File test set (Bước 2), relative to Web content root.</summary>
    public const string TestSetRelativePath = "Data/benchmark-questions.json";

    /// <summary>Mã môn mặc định trong test set (khớp DatabaseSeeder).</summary>
    public const string DefaultCourseCode = "PRN222";

    // ----- 4 metrics bắt buộc -----

    /// <summary>Thời gian xử lý 1 câu hỏi (ms), đo bằng Stopwatch quanh pipeline RAG.</summary>
    public const string MetricLatencyMs = "LatencyMs";

    /// <summary>Điểm similarity cao nhất trong top-k chunk retrieve được.</summary>
    public const string MetricTopScore = "TopScore";

    /// <summary>
    /// Hit = trả lời có ít nhất 1 citation (retrieve thành công, không reject).
    /// Miss = không Hit (thường đi cùng Reject hoặc không có nguồn).
    /// </summary>
    public const string MetricHit = "Hit";

    /// <summary>Rejected = RAG từ chối trả lời (dưới SimilarityThreshold / ngoài phạm vi).</summary>
    public const string MetricReject = "Reject";

    // ----- Định nghĩa Hit / Reject trong project này -----

    /// <summary>Hit khi answer không bị reject và có ≥ 1 citation.</summary>
    public static bool IsHit(bool rejected, int citationCount) => !rejected && citationCount > 0;

    /// <summary>Reject lấy trực tiếp từ kết quả ChatAnswer.Rejected.</summary>
    public static bool IsReject(bool rejected) => rejected;

    // ----- Ngoài scope (không làm trong flow này) -----
    // - Packages + Payments
    // - UI Student/Lecturer cho benchmark
    // - Precision@k thủ công với label chuyên gia
    // - A/B nhiều model song song
}
