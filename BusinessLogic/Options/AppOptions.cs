namespace BusinessLogic.Options;

public sealed class RagOptions
{
    public double SimilarityThreshold { get; set; } = 0.7;
    public int TopK { get; set; } = 5;
    public int ChunkWords { get; set; } = 350;
    public int OverlapWords { get; set; } = 50;
}

public sealed class StorageOptions
{
    public string RootPath { get; set; } = "App_Data/Uploads";
}

public sealed class AiOptions
{
    public string Provider { get; set; } = "Local";
    public string? GeminiApiKey { get; set; }
    public string? GitHubToken { get; set; }
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";
    public string ChatModel { get; set; } = "gemini-2.5-flash";
}

public sealed class VectorStoreOptions
{
    public string Provider { get; set; } = "SqlServer";
    public string Url { get; set; } = "http://localhost:6333";
    public string? ApiKey { get; set; }
    public string Collection { get; set; } = "student_documents";
}

/// <summary>Giới hạn hỏi đáp theo ngày cho tài khoản sinh viên (mặc định gói Free).</summary>
public sealed class StudentUsageOptions
{
    public bool Enabled { get; set; } = true;
    /// <summary>Số câu hỏi tối đa mỗi ngày (fallback khi chưa có gói Free trong DB).</summary>
    public int DailyQuestionLimit { get; set; } = 10;
    /// <summary>Độ dài tối đa câu hỏi (ký tự) để tránh prompt quá dài.</summary>
    public int MaxQuestionChars { get; set; } = 500;
}

/// <summary>Cấu hình cổng thanh toán VNPay (sandbox hoặc production).</summary>
public sealed class VnPayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string Version { get; set; } = "2.1.0";
    public string Command { get; set; } = "pay";
    public string CurrCode { get; set; } = "VND";
    public string Locale { get; set; } = "vn";
    public string OrderType { get; set; } = "other";
    /// <summary>
    /// VNBANK = chỉ thẻ nội địa/ATM; VNPAYQR = QR; INTCARD = thẻ quốc tế; trống = hiện tất cả.
    /// </summary>
    public string BankCode { get; set; } = "VNBANK";
    /// <summary>Đường dẫn tương đối, ví dụ /Payments/VnPayReturn.</summary>
    public string ReturnPath { get; set; } = "/Payments/VnPayReturn";

    public string TmnCodeTrimmed => TmnCode.Trim();
    public string HashSecretTrimmed => HashSecret.Trim();
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret);
}

public sealed class ApplicationLayerOptions
{
    public required string ConnectionString { get; init; }
    public required RagOptions Rag { get; init; }
    public required StorageOptions Storage { get; init; }
    public required AiOptions Ai { get; init; }
    public required VectorStoreOptions VectorStore { get; init; }
    public required StudentUsageOptions StudentUsage { get; init; }
    public required VnPayOptions VnPay { get; init; }
}
