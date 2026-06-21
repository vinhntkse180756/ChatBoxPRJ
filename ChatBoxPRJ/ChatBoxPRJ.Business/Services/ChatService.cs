using System.Text.Json;
using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;

namespace ChatBoxPRJ.Business.Services;

public sealed class ChatService(
    ICourseRepository courses,
    IDocumentRepository documents,
    IChatRepository chats,
    IEmbeddingService embeddings,
    IVectorStore vectors,
    IAnswerGenerator answers,
    RagOptions options,
    IMapper mapper) : IChatService
{
    public async Task<ChatWorkspaceDto?> OpenWorkspaceAsync(Guid userId, UserRole role, Guid courseId, Guid? documentId = null, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(courseId, ct);
        if (course is null || !await CanAccessCourseAsync(userId, role, courseId, ct)) return null;
        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var docs = mapper.Map<IReadOnlyList<DocumentDto>>(await documents.ListAsync(courseId, true, ct));
        var documentNames = docs.ToDictionary(x => x.Id, x => x.FileName);
        var histories = (await chats.GetHistoriesAsync(session.Id, ct))
            .Select(x => new ChatHistoryDto(
                x.DocumentId,
                documentNames.GetValueOrDefault(x.DocumentId, "Tài liệu đã bị xóa"),
                x.MessageCount,
                x.UpdatedAtUtc))
            .ToList();
        var messages = documentId.HasValue
            ? (await chats.GetMessagesAsync(session.Id, documentId, ct)).Select(MapMessage).ToList()
            : [];
        return new ChatWorkspaceDto(mapper.Map<CourseDto>(course), session.Id, docs, histories, messages);
    }

    public async Task<ChatAnswer> AskAsync(Guid userId, UserRole role, Guid courseId, Guid documentId, string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question)) return new("Vui lòng nhập câu hỏi.", [], true);
        if (!await CanAccessCourseAsync(userId, role, courseId, ct)) return new("Bạn chưa được cấp quyền truy cập môn học này.", [], true);
        var selectedDocument = await documents.FindAsync(documentId, ct);
        if (selectedDocument is null || selectedDocument.CourseId != courseId || selectedDocument.Status != DocumentStatus.Completed)
            return new("Vui lòng chọn một tài liệu đã lập chỉ mục trước khi đặt câu hỏi.", [], true);
        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var history = await chats.GetMessagesAsync(session.Id, documentId, ct);
        await chats.AddMessageAsync(new ChatMessage { SessionId = session.Id, DocumentId = documentId, Role = MessageRole.User, Content = question.Trim() }, ct);
        var query = await embeddings.EmbedAsync(question, EmbeddingTask.Query, ct);
        var found = await vectors.SearchAsync(courseId, documentId, query, options.TopK, ct);
        if (found.Count == 0 || found.Max(x => x.Score) < options.SimilarityThreshold)
        {
            const string refusal = "Xin lỗi, câu hỏi của bạn không nằm trong phạm vi tài liệu học tập của môn học này.";
            await chats.AddMessageAsync(new ChatMessage { SessionId = session.Id, DocumentId = documentId, Role = MessageRole.Assistant, Content = refusal, CitationsJson = "[]" }, ct);
            return new(refusal, [], true);
        }
        var answer = await answers.GenerateAsync(question, found, history, ct);
        var citations = found.Select(x => new CitationDto(x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber,
            x.Content.Length <= 320 ? x.Content : x.Content[..320] + "…")).ToList();
        await chats.AddMessageAsync(new ChatMessage
        {
            SessionId = session.Id, DocumentId = documentId, Role = MessageRole.Assistant, Content = answer,
            CitationsJson = JsonSerializer.Serialize(citations)
        }, ct);
        return new(answer, citations);
    }

    public async Task<(bool Success, string Message)> DeleteHistoryAsync(
        Guid userId,
        UserRole role,
        Guid courseId,
        Guid? documentId = null,
        CancellationToken ct = default)
    {
        if (await courses.FindAsync(courseId, ct) is null) return (false, "Không tìm thấy môn học.");
        if (!await CanAccessCourseAsync(userId, role, courseId, ct)) return (false, "Bạn chưa được cấp quyền truy cập môn học này.");
        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var deleted = await chats.DeleteMessagesAsync(session.Id, documentId, ct);
        if (deleted == 0) return (false, "Không có lịch sử hội thoại để xóa.");
        return (true, documentId.HasValue
            ? "Đã xóa lịch sử hội thoại của tài liệu."
            : "Đã xóa toàn bộ lịch sử hội thoại của môn học.");
    }

    private async Task<bool> CanAccessCourseAsync(Guid userId, UserRole role, Guid courseId, CancellationToken ct)
        => role == UserRole.Student
            || role == UserRole.Lecturer && await courses.GetLecturerAccessLevelAsync(userId, courseId, ct) is not null;

    private static ChatMessageDto MapMessage(ChatMessage x)
    {
        IReadOnlyList<CitationDto> citations = [];
        if (!string.IsNullOrWhiteSpace(x.CitationsJson))
            citations = JsonSerializer.Deserialize<List<CitationDto>>(x.CitationsJson) ?? [];
        return new(x.Id, x.Role, x.Content, citations, x.CreatedAtUtc);
    }
}
