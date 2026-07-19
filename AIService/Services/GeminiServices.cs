using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AIService.Models;
using AIService.Options;
using BusinessObjects.Entities;

namespace AIService.Services;

public sealed class GeminiEmbeddingService(AiOptions options) : IEmbeddingService
{
    private readonly HttpClient _http = new();
    private readonly HashEmbeddingService _local = new();

    public async Task<float[]> EmbedAsync(string text, EmbeddingTask task = EmbeddingTask.Document, string? modelOverride = null, CancellationToken ct = default)
    {
        var model = modelOverride ?? options.EmbeddingModel;

        // Local hash embedding – không cần API key
        if (model.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
            model.Equals("local-hash", StringComparison.OrdinalIgnoreCase))
            return await _local.EmbedAsync(text, task, null, ct);

        // GitHub Models / Azure AI – dùng GitHub Token với text-embedding-3-*
        if (model.StartsWith("text-embedding-3", StringComparison.OrdinalIgnoreCase))
        {
            var ghToken = options.GitHubToken ?? throw new InvalidOperationException("Thiếu AI:GitHubToken để dùng text-embedding-3.");
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://models.inference.ai.azure.com/embeddings");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ghToken);
            req.Content = JsonContent.Create(new { input = text, model });
            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"GitHub Embedding ({model}) trả về HTTP {(int)resp.StatusCode}: {body[..Math.Min(body.Length, 400)]}");
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("data")[0].GetProperty("embedding")
                .EnumerateArray().Select(x => x.GetSingle()).ToArray();
        }

        // Mặc định: Gemini Embedding API
        var key = options.GeminiApiKey ?? throw new InvalidOperationException("Thiếu AI:GeminiApiKey.");
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
        using var jsonDoc = JsonDocument.Parse(responseBody);
        return jsonDoc.RootElement.GetProperty("embedding").GetProperty("values").EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }


    internal static string BuildApiError(string service, string model, System.Net.HttpStatusCode status, string body)
    {
        if (status == System.Net.HttpStatusCode.TooManyRequests)
            return "Gemini đang bị giới hạn quota/rate limit (HTTP 429). Vui lòng đợi khoảng 1 phút rồi thử lại, hoặc dùng API key có billing / chuyển tạm sang AI:Provider = Local.";

        try
        {
            using var json = JsonDocument.Parse(body);
            var message = json.RootElement.GetProperty("error").GetProperty("message").GetString();
            if (!string.IsNullOrWhiteSpace(message) &&
                (message.Contains("quota", StringComparison.OrdinalIgnoreCase)
                 || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)))
                return "Gemini đã hết hạn mức free tier. Hãy đợi reset quota, bật billing trên Google AI Studio, hoặc tạm đặt AI:Provider = \"Local\" trong appsettings.json.";

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
            throw new InvalidOperationException(GeminiEmbeddingService.BuildApiError("Gemini Chat", model, response.StatusCode, responseBody));
        using var json = JsonDocument.Parse(responseBody);
        return json.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0]
                   .GetProperty("text").GetString()
               ?? "Không tạo được câu trả lời.";
    }

    public async IAsyncEnumerable<GenerateChunkResult> GenerateStreamAsync(
        string question,
        IReadOnlyList<AnswerChunkContext> context,
        IReadOnlyList<AnswerMessageContext> history,
        string? modelOverride = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var model = modelOverride ?? options.ChatModel;

        if (model.Equals("Local Mock", StringComparison.OrdinalIgnoreCase))
        {
            var localGen = new ExtractiveAnswerGenerator();
            await foreach (var chunk in localGen.GenerateStreamAsync(question, context, history, model, ct))
            {
                yield return chunk;
            }
            yield break;
        }

        var isGitHub = model.Contains("GitHub", StringComparison.OrdinalIgnoreCase) 
                       || model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) 
                       || model.Contains("llama", StringComparison.OrdinalIgnoreCase)
                       || model.Contains("cohere", StringComparison.OrdinalIgnoreCase);

        var contextText = string.Join("\n\n", context.Select(x =>
            $"[Nguồn: {x.FileName}, trang {x.PageNumber}, đoạn {x.ChunkNumber}]\n{x.Content}"));
        var historyText = string.Join("\n", history.TakeLast(12).Select(x =>
            $"{(x.Role == MessageRole.User ? "Sinh viên" : "Trợ lý")}: {x.Content}"));
        var prompt = $"LỊCH SỬ:\n{historyText}\n\nNGỮ CẢNH TÀI LIỆU:\n{contextText}\n\nCÂU HỎI:\n{question}";

        var sw = Stopwatch.StartNew();

        if (isGitHub)
        {
            var token = options.GitHubToken ?? throw new InvalidOperationException("Thiếu AI:GitHubToken.");
            var modelCleaned = model.Replace(" (GitHub)", "", StringComparison.OrdinalIgnoreCase).Trim();
            
            var payload = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "Bạn là trợ lý học tập chính xác. Chỉ trả lời bằng tiếng Việt dựa trên ngữ cảnh được cung cấp; không bịa kiến thức ngoài tài liệu."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                model = modelCleaned,
                stream = true,
                temperature = 0.2,
                max_tokens = 1200
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://models.inference.ai.azure.com/chat/completions")
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("User-Agent", "ChatBoxPRJ-Benchmark");

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"GitHub Models ({modelCleaned}) trả về HTTP {(int)response.StatusCode}: {responseBody}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;
                if (line.StartsWith("data: "))
                {
                    var dataJson = line.Substring(6).Trim();
                    if (dataJson == "[DONE]") break;
                    if (string.IsNullOrEmpty(dataJson)) continue;

                    string? textChunk = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(dataJson);
                        if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                            choices.GetArrayLength() > 0 &&
                            choices[0].TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("content", out var content))
                        {
                            textChunk = content.GetString();
                        }
                    }
                    catch
                    {
                        // Ignore malformed JSON chunks
                    }

                    if (!string.IsNullOrEmpty(textChunk))
                    {
                        yield return new GenerateChunkResult(textChunk, sw.ElapsedMilliseconds);
                    }
                }
            }
        }
        else
        {
            // Gemini Stream Completion
            var key = options.GeminiApiKey ?? throw new InvalidOperationException("Thiếu AI:GeminiApiKey.");
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

            var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(key)}";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(GeminiEmbeddingService.BuildApiError("Gemini Chat Stream", model, response.StatusCode, responseBody));
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line == null) break;
                if (line.StartsWith("data: "))
                {
                    var dataJson = line.Substring(6).Trim();
                    if (string.IsNullOrEmpty(dataJson)) continue;

                    string? textChunk = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(dataJson);
                        if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var text))
                        {
                            textChunk = text.GetString();
                        }
                    }
                    catch
                    {
                        // Ignore malformed JSON chunks
                    }

                    if (!string.IsNullOrEmpty(textChunk))
                    {
                        yield return new GenerateChunkResult(textChunk, sw.ElapsedMilliseconds);
                    }
                }
            }
        }
    }
}
