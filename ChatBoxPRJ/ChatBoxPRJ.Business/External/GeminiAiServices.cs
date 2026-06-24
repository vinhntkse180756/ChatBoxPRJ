using System.Net.Http.Json;
using System.Text.Json;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;

namespace ChatBoxPRJ.Business.External;

public sealed class GeminiEmbeddingService(AiOptions options) : IEmbeddingService
{
    private readonly HttpClient _http = new();

    public async Task<float[]> EmbedAsync(string text, EmbeddingTask task = EmbeddingTask.Document, CancellationToken ct = default)
    {
        var key = options.GeminiApiKey ?? throw new InvalidOperationException("Thiếu AI:GeminiApiKey.");
        var model = options.EmbeddingModel;
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
        catch
        {
            return $"{service} ({model}) trả về HTTP {(int)status}: {body[..Math.Min(body.Length, 500)]}";
        }
    }
}

public sealed class GeminiAnswerGenerator(AiOptions options) : IAnswerGenerator
{
    private readonly HttpClient _http = new();

    public async Task<string> GenerateAsync(
        string question,
        IReadOnlyList<AnswerChunkContext> context,
        IReadOnlyList<AnswerMessageContext> history,
        CancellationToken ct = default)
    {
        var key = options.GeminiApiKey ?? throw new InvalidOperationException("Thiếu AI:GeminiApiKey.");
        var model = options.ChatModel;
        var contextText = string.Join("\n\n", context.Select(x =>
            $"[Nguồn: {x.FileName}, trang {x.PageNumber}, đoạn {x.ChunkNumber}]\n{x.Content}"));
        var historyText = string.Join("\n", history.TakeLast(12).Select(x =>
            $"{(x.Role == MessageRole.User ? "Sinh viên" : "Trợ lý")}: {x.Content}"));
        var prompt = $"LỊCH SỬ:\n{historyText}\n\nNGỮ CẢNH TÀI LIỆU:\n{contextText}\n\nCÂU HỎI:\n{question}";
        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = "Bạn là trợ lý học tập chính xác. Chỉ trả lời bằng tiếng Việt dựa trên ngữ cảnh được cung cấp; không bịa kiến thức ngoài tài liệu."
                    }
                }
            },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = 1200 }
        };
        using var response = await _http.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(key)}",
            payload,
            ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildApiError("Gemini Chat", model, response.StatusCode, responseBody));
        using var json = JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0]
                   .GetProperty("text").GetString()
               ?? "Không tạo được câu trả lời.";
    }

    private static string BuildApiError(string service, string model, System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var message = json.RootElement.GetProperty("error").GetProperty("message").GetString();
            return $"{service} ({model}) trả về HTTP {(int)status}: {message}";
        }
        catch
        {
            return $"{service} ({model}) trả về HTTP {(int)status}: {body[..Math.Min(body.Length, 500)]}";
        }
    }
}
