using System.Text.Json;
using AutoMapper;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.DataAccess.Interfaces;
using ChatBoxPRJ.DataAccess.Models;
using BusinessMessageRole = ChatBoxPRJ.Business.DTOs.MessageRole;
using BusinessUserRole = ChatBoxPRJ.Business.DTOs.UserRole;
using DataDocumentStatus = ChatBoxPRJ.DataAccess.Models.DocumentStatus;
using DataMessageRole = ChatBoxPRJ.DataAccess.Models.MessageRole;

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
    public async Task<ChatWorkspaceDto?> OpenWorkspaceAsync(Guid userId, BusinessUserRole role, Guid courseId, Guid? conversationId = null, CancellationToken ct = default)
    {
        var course = await courses.FindAsync(courseId, ct);
        if (course is null || !await CanAccessCourseAsync(userId, role, courseId, ct)) return null;
        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var docs = mapper.Map<IReadOnlyList<DocumentDto>>(await documents.ListAsync(courseId, true, ct));
        var documentNames = docs.ToDictionary(x => x.Id, x => x.FileName);
        var historyMessages = await chats.GetMessagesAsync(session.Id, ct: ct);
        var histories = historyMessages
            .Where(x => x.DocumentId.HasValue)
            .GroupBy(x => x.ConversationId)
            .Select(group =>
            {
                var firstQuestion = group
                    .Where(x => x.Role == DataMessageRole.User)
                    .OrderBy(x => x.CreatedAtUtc)
                    .Select(x => x.Content.Trim())
                    .FirstOrDefault() ?? "Cuộc trò chuyện";
                var title = firstQuestion.Length <= 70 ? firstQuestion : firstQuestion[..70] + "…";
                var documentId = group.Select(x => x.DocumentId!.Value).First();
                return new ChatHistoryDto(
                    group.Key,
                    documentId,
                    documentNames.GetValueOrDefault(documentId, "Tài liệu đã bị xóa"),
                    title,
                    group.Count(),
                    group.Max(x => x.CreatedAtUtc));
            })
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();
        var messages = conversationId.HasValue
            ? (await chats.GetMessagesAsync(session.Id, conversationId, ct)).Select(MapMessage).ToList()
            : [];
        return new ChatWorkspaceDto(mapper.Map<CourseDto>(course), session.Id, docs, histories, messages);
    }

    public async Task<ChatAnswer> AskAsync(Guid userId, BusinessUserRole role, Guid courseId, Guid? conversationId, Guid documentId, string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question)) return new("Vui lòng nhập câu hỏi.", [], true);
        if (!await CanAccessCourseAsync(userId, role, courseId, ct)) return new("Bạn chưa được cấp quyền truy cập môn học này.", [], true);

        var selectedDocument = await documents.FindAsync(documentId, ct);
        if (selectedDocument is null || selectedDocument.CourseId != courseId || selectedDocument.Status != DataDocumentStatus.Completed)
            return new("Vui lòng chọn một tài liệu đã lập chỉ mục trước khi đặt câu hỏi.", [], true);

        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var activeConversationId = conversationId ?? Guid.NewGuid();
        var history = await chats.GetMessagesAsync(session.Id, activeConversationId, ct);

        if (history.Any(x => x.DocumentId != documentId))
            return new("Cuộc trò chuyện này thuộc một tài liệu khác. Hãy tạo đoạn chat mới.", [], true, activeConversationId);

        await chats.AddMessageAsync(new ChatMessage { SessionId = session.Id, ConversationId = activeConversationId, DocumentId = documentId, Role = DataMessageRole.User, Content = question.Trim() }, ct);

        var query = await embeddings.EmbedAsync(question, EmbeddingTask.Query, ct);
        var found = await vectors.SearchAsync(courseId, documentId, query, options.TopK, ct);

        if (found.Count == 0 || found.Max(x => x.Score) < options.SimilarityThreshold)
        {
            const string refusal = "Xin lỗi, câu hỏi của bạn không nằm trong phạm vi tài liệu học tập của môn học này.";
            await chats.AddMessageAsync(new ChatMessage { SessionId = session.Id, ConversationId = activeConversationId, DocumentId = documentId, Role = DataMessageRole.Assistant, Content = refusal, CitationsJson = "[]" }, ct);
            return new(refusal, [], true, activeConversationId);
        }

        var answerContext = found.Select(x => new AnswerChunkContext(
            x.ChunkId, x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber, x.Content, x.Score)).ToList();

        var historyContext = history.Select(x => new AnswerMessageContext((BusinessMessageRole)x.Role, x.Content)).ToList();
        var answer = await answers.GenerateAsync(question, answerContext, historyContext, ct);

        var citations = found.Select(x => new CitationDto(x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber,
            x.Content.Length <= 320 ? x.Content : x.Content[..320] + "…")).ToList();

        await chats.AddMessageAsync(new ChatMessage
        {
            SessionId = session.Id,
            ConversationId = activeConversationId,
            DocumentId = documentId,
            Role = DataMessageRole.Assistant,
            Content = answer,
            CitationsJson = JsonSerializer.Serialize(citations)
        }, ct);

        return new(answer, citations, ConversationId: activeConversationId);
    }

    public async Task<(bool Success, string Message)> DeleteHistoryAsync(
        Guid userId,
        BusinessUserRole role,
        Guid courseId,
        Guid? conversationId = null,
        CancellationToken ct = default)
    {
        if (await courses.FindAsync(courseId, ct) is null) return (false, "Không tìm thấy môn học.");
        if (!await CanAccessCourseAsync(userId, role, courseId, ct)) return (false, "Bạn chưa được cấp quyền truy cập môn học này.");
        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var deleted = await chats.DeleteMessagesAsync(session.Id, conversationId, ct);
        if (deleted == 0) return (false, "Không có lịch sử hội thoại để xóa.");
        return (true, conversationId.HasValue
            ? "Đã xóa đoạn chat."
            : "Đã xóa toàn bộ lịch sử hội thoại của môn học.");
    }

    private async Task<bool> CanAccessCourseAsync(Guid userId, BusinessUserRole role, Guid courseId, CancellationToken ct)
        => role == BusinessUserRole.Student
            || role == BusinessUserRole.Lecturer && await courses.GetLecturerAccessLevelAsync(userId, courseId, ct) is not null;

    private static ChatMessageDto MapMessage(ChatMessage x)
    {
        IReadOnlyList<CitationDto> citations = [];
        if (!string.IsNullOrWhiteSpace(x.CitationsJson))
            citations = JsonSerializer.Deserialize<List<CitationDto>>(x.CitationsJson) ?? [];
        return new(x.Id, (BusinessMessageRole)x.Role, x.Content, citations, x.CreatedAtUtc);
    }
}