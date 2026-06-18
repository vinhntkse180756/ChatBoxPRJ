using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ChatBoxPRJ.Business.Domain;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;

namespace ChatBoxPRJ.Business.Services;

public sealed class DocumentService(
    IDocumentRepository documents,
    ICourseRepository courses,
    ICourseService courseService,
    IEmbeddingService embeddings,
    IVectorStore vectorStore,
    IDocumentWorkQueue queue,
    IDocumentStatusNotifier notifier,
    StorageOptions storage,
    RagOptions rag) : IDocumentService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".txt" };

    public async Task<UploadResult> UploadAsync(UploadRequest request, CancellationToken ct = default)
    {
        if (!Allowed.Contains(Path.GetExtension(request.FileName))) return new(false, "Chỉ hỗ trợ PDF, DOCX hoặc TXT.");
        if (request.Content.CanSeek && request.Content.Length > 25 * 1024 * 1024) return new(false, "Tệp vượt quá giới hạn 25 MB.");

        await using var memory = new MemoryStream();
        await request.Content.CopyToAsync(memory, ct);
        var hash = Convert.ToHexString(SHA256.HashData(memory.ToArray())).ToLowerInvariant();
        if (await documents.HashExistsAsync(request.CourseId, hash, ct)) return new(false, "Nội dung tệp đã tồn tại trong môn học này.");

        var existing = await documents.FindByNameAsync(request.CourseId, request.FileName, ct);
        if (existing is not null && !request.Overwrite) return new(false, "Tên tệp đã tồn tại. Hãy chọn ghi đè để tiếp tục.");
        if (existing is not null)
        {
            await vectorStore.DeleteDocumentAsync(existing.Id, ct);
            if (File.Exists(existing.StoragePath)) File.Delete(existing.StoragePath);
            await documents.DeleteAsync(existing, ct);
        }

        var course = await courses.FindAsync(request.CourseId, ct);
        if (course is null) return new(false, "Không tìm thấy môn học.");
        var safeName = string.Concat(Path.GetFileName(request.FileName).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var folder = Path.Combine(storage.RootPath, course.Code);
        Directory.CreateDirectory(folder);
        var document = new LearningDocument
        {
            CourseId = request.CourseId, UploadedById = request.UploadedById, OriginalFileName = safeName,
            Sha256 = hash, Status = DocumentStatus.Processing,
            StoragePath = Path.Combine(folder, $"{Guid.NewGuid():N}_{safeName}")
        };
        memory.Position = 0;
        await using (var target = File.Create(document.StoragePath)) await memory.CopyToAsync(target, ct);
        await documents.AddAsync(document, ct);
        await queue.EnqueueAsync(document.Id, ct);
        await notifier.NotifyAsync(document.Id, DocumentStatus.Processing, "Đang trích xuất và lập chỉ mục…", ct);
        return new(true, "Tệp đã vào hàng đợi xử lý.", document.Id);
    }

    public async Task ProcessAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await documents.FindAsync(documentId, ct);
        if (document is null) return;
        try
        {
            var pages = await ExtractPagesAsync(document.StoragePath, ct);
            if (pages.Sum(x => x.Text.Length) < 20) throw new InvalidOperationException("Không trích xuất được chữ. PDF có thể là bản scan ảnh.");
            var chunks = new List<DocumentChunk>();
            var chunkNo = 1;
            foreach (var page in pages)
            {
                foreach (var text in Chunk(page.Text, rag.ChunkWords, rag.OverlapWords))
                {
                    var vector = await embeddings.EmbedAsync(text, EmbeddingTask.Document, ct);
                    chunks.Add(new DocumentChunk
                    {
                        DocumentId = document.Id, CourseId = document.CourseId, PageNumber = page.Page,
                        ChunkNumber = chunkNo++, Content = text, VectorJson = System.Text.Json.JsonSerializer.Serialize(vector)
                    });
                }
            }
            // Luôn giữ một bản chunk trong SQL để giảng viên có thể kiểm tra nội dung,
            // kể cả khi vector chính được lưu ở Qdrant.
            await documents.ReplaceChunksAsync(document.Id, chunks, ct);
            await vectorStore.UpsertAsync(document, chunks, ct);
            document.Status = DocumentStatus.Completed;
            document.FailureReason = null;
            await documents.UpdateAsync(document, ct);
            await notifier.NotifyAsync(document.Id, document.Status, "Lập chỉ mục hoàn tất.", ct);
        }
        catch (Exception ex)
        {
            document.Status = DocumentStatus.Failed;
            document.FailureReason = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
            await documents.UpdateAsync(document, ct);
            await notifier.NotifyAsync(document.Id, document.Status, document.FailureReason, ct);
        }
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(Guid? courseId, bool completedOnly, CancellationToken ct = default)
        => (await documents.ListAsync(courseId, completedOnly, ct)).Select(x => new DocumentDto(
            x.Id, x.CourseId, x.Course.Name, x.OriginalFileName, x.Status, x.FailureReason, x.UploadedAtUtc)).ToList();

    public async Task<DocumentChunksDto?> GetChunksAsync(Guid documentId, Guid actorId, UserRole role, CancellationToken ct = default)
    {
        var document = await documents.FindAsync(documentId, ct);
        if (document is null || !await courseService.CanManageAsync(actorId, role, document.CourseId, ct)) return null;
        var chunks = document.Chunks.OrderBy(x => x.ChunkNumber).Select(x => new DocumentChunkDto(
            x.Id, x.PageNumber, x.ChunkNumber,
            Regex.Split(x.Content.Trim(), @"\s+").Count(word => word.Length > 0), x.Content)).ToList();
        return new DocumentChunksDto(document.Id, document.OriginalFileName, document.Course.Name, document.Status, chunks);
    }

    public async Task<(bool Success, string Message)> DeleteAsync(Guid documentId, Guid actorId, UserRole role, CancellationToken ct = default)
    {
        var document = await documents.FindAsync(documentId, ct);
        if (document is null) return (false, "Không tìm thấy tài liệu.");
        if (!await courseService.CanManageAsync(actorId, role, document.CourseId, ct)) return (false, "Bạn không có quyền xóa tài liệu này.");
        try { await vectorStore.DeleteDocumentAsync(document.Id, ct); }
        catch { return (false, "Lỗi kết nối hạ tầng AI, vui lòng thử lại sau."); }
        if (File.Exists(document.StoragePath)) File.Delete(document.StoragePath);
        await documents.DeleteAsync(document, ct);
        return (true, "Đã xóa tài liệu và toàn bộ tri thức liên quan.");
    }

    public async Task<(string Path, string FileName, string ContentType)?> GetStudentFileAsync(Guid documentId, Guid courseId, CancellationToken ct = default)
    {
        var document = await documents.FindAsync(documentId, ct);
        if (document is null || document.CourseId != courseId || document.Status != DocumentStatus.Completed || !File.Exists(document.StoragePath)) return null;
        var type = Path.GetExtension(document.OriginalFileName).ToLowerInvariant() switch
        { ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".txt" => "text/plain; charset=utf-8", _ => "application/octet-stream" };
        return (document.StoragePath, document.OriginalFileName, type);
    }

    private static IEnumerable<string> Chunk(string text, int size, int overlap)
    {
        var words = Regex.Split(text.Trim(), @"\s+").Where(x => x.Length > 0).ToArray();
        var step = Math.Max(1, size - overlap);
        for (var i = 0; i < words.Length; i += step)
        {
            var count = Math.Min(size, words.Length - i);
            if (count > 0) yield return string.Join(' ', words, i, count);
            if (i + count >= words.Length) break;
        }
    }

    private static async Task<IReadOnlyList<(int Page, string Text)>> ExtractPagesAsync(string path, CancellationToken ct)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".txt") return [(1, await File.ReadAllTextAsync(path, ct))];
        if (ext == ".docx")
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidOperationException("DOCX không hợp lệ.");
            await using var stream = entry.Open();
            var xml = await XDocument.LoadAsync(stream, LoadOptions.None, ct);
            var ns = (XNamespace)"http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = xml.Descendants(ns + "p").Select(p => string.Concat(p.Descendants(ns + "t").Select(t => t.Value)));
            return [(1, string.Join(Environment.NewLine, paragraphs))];
        }
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var raw = Encoding.Latin1.GetString(bytes);
        var pageParts = Regex.Split(raw, @"/Type\s*/Page\b").Skip(1).ToArray();
        if (pageParts.Length == 0) pageParts = [raw];
        var result = new List<(int, string)>();
        for (var i = 0; i < pageParts.Length; i++)
        {
            var matches = Regex.Matches(pageParts[i], @"\((?<text>(?:\\.|[^\\)])*)\)\s*Tj");
            var text = string.Join(" ", matches.Select(m => Regex.Unescape(m.Groups["text"].Value)));
            result.Add((i + 1, text));
        }
        return result;
    }
}
