using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using BusinessMessageRole = ChatBoxPRJ.Business.DTOs.MessageRole;

namespace ChatBoxPRJ.Infrastructure;

public sealed class GeminiEmbeddingService(IConfiguration config) : IEmbeddingService
{
    private readonly HttpClient _http = new();
    public async Task<float[]> EmbedAsync(string text, EmbeddingTask task = EmbeddingTask.Document, CancellationToken ct = default)
    {
        var key = config["AI:GeminiApiKey"] ?? throw new InvalidOperationException("Thiếu AI:GeminiApiKey.");
        var model = config["AI:EmbeddingModel"] ?? "gemini-embedding-001";
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:embedContent?key={Uri.EscapeDataString(key)}";
        using var response = await _http.PostAsJsonAsync(url, new
        {
            model = $"models/{model}",
            taskType = task == EmbeddingTask.Query ? "RETRIEVAL_QUERY" : "RETRIEVAL_DOCUMENT",
            content = new { parts = new[] { new { text } } }
        }, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildApiError("Gemini Embedding", model, response.StatusCode, responseBody));
        using var json = JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("embedding").GetProperty("values").EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    private static string BuildApiError(string service, string model, System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var message = json.RootElement.GetProperty("error").GetProperty("message").GetString();
            return $"{service} ({model}) trả về HTTP {(int)status}: {message}";
        }
        catch { return $"{service} ({model}) trả về HTTP {(int)status}: {body[..Math.Min(body.Length, 500)]}"; }
    }
}

public sealed class GeminiAnswerGenerator(IConfiguration config) : IAnswerGenerator
{
    private readonly HttpClient _http = new();
    public async Task<string> GenerateAsync(string question, IReadOnlyList<RetrievedChunkContext> context, IReadOnlyList<ChatMessageContext> history, CancellationToken ct = default)
    {
        var key = config["AI:GeminiApiKey"] ?? throw new InvalidOperationException("Thiếu AI:GeminiApiKey.");
        var model = config["AI:ChatModel"] ?? "gemini-2.5-flash";
        var contextText = string.Join("\n\n", context.Select(x => $"[Nguồn: {x.FileName}, trang {x.PageNumber}, đoạn {x.ChunkNumber}]\n{x.Content}"));
        var historyText = string.Join("\n", history.TakeLast(12).Select(x => $"{(x.Role == BusinessMessageRole.User ? "Sinh viên" : "Trợ lý")}: {x.Content}"));
        var prompt = $"LỊCH SỬ:\n{historyText}\n\nNGỮ CẢNH TÀI LIỆU:\n{contextText}\n\nCÂU HỎI:\n{question}";
        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = "Bạn là trợ lý học tập chính xác. Chỉ trả lời bằng tiếng Việt dựa trên ngữ cảnh được cung cấp; không bịa kiến thức ngoài tài liệu." } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 1200 }
        };
        using var response = await _http.PostAsJsonAsync($"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(key)}", payload, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildApiError("Gemini Chat", model, response.StatusCode, responseBody));
        using var json = JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "Không tạo được câu trả lời.";
    }

    private static string BuildApiError(string service, string model, System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var message = json.RootElement.GetProperty("error").GetProperty("message").GetString();
            return $"{service} ({model}) trả về HTTP {(int)status}: {message}";
        }
        catch { return $"{service} ({model}) trả về HTTP {(int)status}: {body[..Math.Min(body.Length, 500)]}"; }
    }
}

public sealed class QdrantVectorStore(IConfiguration config) : IVectorStore
{
    private readonly HttpClient _http = CreateClient(config);
    private readonly string _collection = config["VectorStore:Collection"] ?? "student_documents";

    public async Task UpsertAsync(LearningDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0) return;
        var firstVector = JsonSerializer.Deserialize<float[]>(chunks[0].VectorJson) ?? throw new InvalidOperationException("Vector không hợp lệ.");
        await EnsureCollectionAsync(firstVector.Length, ct);
        var points = chunks.Select(x => new
        {
            id = x.Id,
            vector = JsonSerializer.Deserialize<float[]>(x.VectorJson),
            payload = new { courseId = x.CourseId.ToString(), documentId = x.DocumentId.ToString(), fileName = document.OriginalFileName, pageNumber = x.PageNumber, chunkNumber = x.ChunkNumber, content = x.Content }
        });
        using var response = await _http.PutAsJsonAsync($"collections/{_collection}/points?wait=true", new { points }, ct);
        await EnsureQdrantSuccessAsync(response, "nạp vector", ct);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(Guid courseId, Guid documentId, float[] queryVector, int limit, CancellationToken ct = default)
    {
        var request = new { vector = queryVector, limit, with_payload = true, filter = new { must = new object[] { new { key = "courseId", match = new { value = courseId.ToString() } }, new { key = "documentId", match = new { value = documentId.ToString() } } } } };
        using var response = await _http.PostAsJsonAsync($"collections/{_collection}/points/search", request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        await EnsureQdrantSuccessAsync(response, "tìm kiếm", ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("result").EnumerateArray().Select(x =>
        {
            var p = x.GetProperty("payload");
            return new RetrievedChunk(Guid.Parse(x.GetProperty("id").GetString()!), Guid.Parse(p.GetProperty("documentId").GetString()!), p.GetProperty("fileName").GetString()!, p.GetProperty("pageNumber").GetInt32(), p.GetProperty("chunkNumber").GetInt32(), p.GetProperty("content").GetString()!, x.GetProperty("score").GetDouble());
        }).ToList();
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var body = new { filter = new { must = new[] { new { key = "documentId", match = new { value = documentId.ToString() } } } } };
        using var response = await _http.PostAsJsonAsync($"collections/{_collection}/points/delete?wait=true", body, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound) await EnsureQdrantSuccessAsync(response, "xóa vector", ct);
    }

    private async Task EnsureCollectionAsync(int size, CancellationToken ct)
    {
        using var check = await _http.GetAsync($"collections/{_collection}", ct);
        if (check.IsSuccessStatusCode) return;
        using var create = await _http.PutAsJsonAsync($"collections/{_collection}", new { vectors = new { size, distance = "Cosine" } }, ct);
        await EnsureQdrantSuccessAsync(create, "tạo collection", ct);
    }

    private static async Task EnsureQdrantSuccessAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Qdrant lỗi khi {operation}, HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
    }

    private static HttpClient CreateClient(IConfiguration config)
    {
        var client = new HttpClient { BaseAddress = new Uri((config["VectorStore:Url"] ?? "http://localhost:6333").TrimEnd('/') + "/") };
        var key = config["VectorStore:ApiKey"];
        if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("api-key", key);
        return client;
    }
}
