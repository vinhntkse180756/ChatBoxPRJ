using System.Net.Http.Json;
using System.Text.Json;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using ChatBoxPRJ.DataAccess.Options;

namespace ChatBoxPRJ.DataAccess.External;

public sealed class QdrantVectorStore(QdrantOptions options) : IVectorStore
{
    private readonly HttpClient _http = CreateClient(options);
    private readonly string _collection = options.Collection;

    public async Task UpsertAsync(LearningDocument document, IReadOnlyList<DocumentChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0) return;
        var firstVector = JsonSerializer.Deserialize<float[]>(chunks[0].VectorJson)
                          ?? throw new InvalidOperationException("Vector không hợp lệ.");
        await EnsureCollectionAsync(firstVector.Length, ct);
        var points = chunks.Select(x => new
        {
            id = x.Id,
            vector = JsonSerializer.Deserialize<float[]>(x.VectorJson),
            payload = new
            {
                courseId = x.CourseId.ToString(),
                documentId = x.DocumentId.ToString(),
                fileName = document.OriginalFileName,
                pageNumber = x.PageNumber,
                chunkNumber = x.ChunkNumber,
                content = x.Content
            }
        });
        using var response = await _http.PutAsJsonAsync(
            $"collections/{_collection}/points?wait=true", new { points }, ct);
        await EnsureQdrantSuccessAsync(response, "nạp vector", ct);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Guid courseId,
        Guid? documentId,
        float[] queryVector,
        int limit,
        CancellationToken ct = default)
    {
        var must = new List<object>
        {
            new { key = "courseId", match = new { value = courseId.ToString() } }
        };
        if (documentId.HasValue)
            must.Add(new { key = "documentId", match = new { value = documentId.Value.ToString() } });

        var request = new
        {
            vector = queryVector,
            limit,
            with_payload = true,
            filter = new { must }
        };
        using var response = await _http.PostAsJsonAsync($"collections/{_collection}/points/search", request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return [];
        await EnsureQdrantSuccessAsync(response, "tìm kiếm", ct);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("result").EnumerateArray().Select(x =>
        {
            var payload = x.GetProperty("payload");
            return new RetrievedChunk(
                Guid.Parse(x.GetProperty("id").GetString()!),
                Guid.Parse(payload.GetProperty("documentId").GetString()!),
                payload.GetProperty("fileName").GetString()!,
                payload.GetProperty("pageNumber").GetInt32(),
                payload.GetProperty("chunkNumber").GetInt32(),
                payload.GetProperty("content").GetString()!,
                x.GetProperty("score").GetDouble());
        }).ToList();
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var body = new
        {
            filter = new
            {
                must = new[] { new { key = "documentId", match = new { value = documentId.ToString() } } }
            }
        };
        using var response = await _http.PostAsJsonAsync(
            $"collections/{_collection}/points/delete?wait=true", body, ct);
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            await EnsureQdrantSuccessAsync(response, "xóa vector", ct);
    }

    private async Task EnsureCollectionAsync(int size, CancellationToken ct)
    {
        using var check = await _http.GetAsync($"collections/{_collection}", ct);
        if (check.IsSuccessStatusCode) return;
        using var create = await _http.PutAsJsonAsync(
            $"collections/{_collection}", new { vectors = new { size, distance = "Cosine" } }, ct);
        await EnsureQdrantSuccessAsync(create, "tạo collection", ct);
    }

    private static async Task EnsureQdrantSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Qdrant lỗi khi {operation}, HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 500)]}");
    }

    private static HttpClient CreateClient(QdrantOptions options)
    {
        var client = new HttpClient { BaseAddress = new Uri(options.Url.TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
        return client;
    }
}
