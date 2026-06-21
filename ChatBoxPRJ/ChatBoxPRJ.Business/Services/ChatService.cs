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
    public async Task<ChatWorkspaceDto?> OpenWorkspaceAsync(Guid studentId, Guid courseId, Guid? documentId = null, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(courseId, ct);
        if (course is null) return null;
        var session = await chats.GetOrCreateSessionAsync(studentId, courseId, ct);
        var docs = mapper.Map<IReadOnlyList<DocumentDto>>(await documents.ListAsync(courseId, true, ct));
        var messages = documentId.HasValue
            ? (await chats.GetMessagesAsync(session.Id, documentId, ct)).Select(MapMessage).ToList()
            : [];
        return new ChatWorkspaceDto(mapper.Map<CourseDto>(course), session.Id, docs, messages);
    }

    public async Task<ChatAnswer> AskAsync(Guid studentId, Guid courseId, Guid documentId, string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question)) return new("Vui lòng nhập câu hỏi.", [], true);
        var selectedDocument = await documents.FindAsync(documentId, ct);
        if (selectedDocument is null || selectedDocument.CourseId != courseId || selectedDocument.Status != DocumentStatus.Completed)
            return new("Vui lòng chọn một tài liệu đã lập chỉ mục trước khi đặt câu hỏi.", [], true);
        var session = await chats.GetOrCreateSessionAsync(studentId, courseId, ct);
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

    private static ChatMessageDto MapMessage(ChatMessage x)
    {
        IReadOnlyList<CitationDto> citations = [];
        if (!string.IsNullOrWhiteSpace(x.CitationsJson))
            citations = JsonSerializer.Deserialize<List<CitationDto>>(x.CitationsJson) ?? [];
        return new(x.Id, x.Role, x.Content, citations, x.CreatedAtUtc);
    }
}
