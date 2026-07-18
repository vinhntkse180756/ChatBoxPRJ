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
    IStudentTokenUsageRepository tokenUsage,
    ISubscriptionService subscriptions,
    IEmbeddingService embeddings,
    IVectorStore vectors,
    IAnswerGenerator answers,
    RagOptions options,
    StudentUsageOptions studentUsage,
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
            .GroupBy(x => x.ConversationId)
            .Select(group =>
            {
                var firstQuestion = group
                    .Where(x => x.Role == DataMessageRole.User)
                    .OrderBy(x => x.CreatedAtUtc)
                    .Select(x => x.Content.Trim())
                    .FirstOrDefault() ?? "Cuộc trò chuyện";
                var title = firstQuestion.Length <= 70 ? firstQuestion : firstQuestion[..70] + "…";
                var documentId = group.Select(x => x.DocumentId).FirstOrDefault(x => x.HasValue);
                var fileName = documentId is { } id
                    ? documentNames.GetValueOrDefault(id, "Tài liệu đã bị xóa")
                    : "Toàn bộ tài liệu môn";
                return new ChatHistoryDto(
                    group.Key,
                    documentId,
                    fileName,
                    title,
                    group.Count(),
                    group.Max(x => x.CreatedAtUtc));
            })
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();
        var messages = conversationId.HasValue
            ? (await chats.GetMessagesAsync(session.Id, conversationId, ct)).Select(MapMessage).ToList()
            : [];
        var quota = role == BusinessUserRole.Student
            ? await BuildQuotaAsync(userId, ct)
            : null;
        return new ChatWorkspaceDto(mapper.Map<CourseDto>(course), session.Id, docs, histories, messages, quota);
    }

    public async Task<ChatAnswer> AskAsync(Guid userId, BusinessUserRole role, Guid courseId, Guid? conversationId, Guid? documentId, string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question)) return new("Vui lòng nhập câu hỏi.", [], true);
        if (!await CanAccessCourseAsync(userId, role, courseId, ct)) return new("Bạn chưa được cấp quyền truy cập môn học này.", [], true);

        var trimmedQuestion = question.Trim();
        if (role == BusinessUserRole.Student && studentUsage.Enabled)
        {
            var limits = await subscriptions.ResolveLimitsAsync(userId, ct);
            var dailyLimit = limits.QuestionsPerDay;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var used = await tokenUsage.GetUsedTokensAsync(userId, today, ct);
            if (used >= dailyLimit)
                return new($"Bạn đã hết hạn mức {dailyLimit:N0} câu hỏi hôm nay (gói {limits.PackageName}). Hãy nâng cấp gói hoặc quay lại ngày mai.", [], true);
        }

        var courseDocuments = await documents.ListAsync(courseId, completedOnly: true, ct);
        if (courseDocuments.Count == 0)
            return new("Môn học chưa có tài liệu đã lập chỉ mục để hỏi đáp.", [], true);

        Guid? scopedDocumentId = null;
        if (documentId.HasValue)
        {
            var selectedDocument = courseDocuments.FirstOrDefault(x => x.Id == documentId.Value);
            if (selectedDocument is null || selectedDocument.Status != DataDocumentStatus.Completed)
                return new("Tài liệu đã chọn không còn khả dụng. Hãy chat theo môn hoặc chọn tài liệu khác.", [], true);
            scopedDocumentId = selectedDocument.Id;
        }

        var session = await chats.GetOrCreateSessionAsync(userId, courseId, ct);
        var activeConversationId = conversationId ?? Guid.NewGuid();
        var history = await chats.GetMessagesAsync(session.Id, activeConversationId, ct);

        if (history.Count > 0)
        {
            var historyScoped = history.Any(x => x.DocumentId.HasValue);
            if (scopedDocumentId.HasValue)
            {
                if (history.Any(x => x.DocumentId != scopedDocumentId))
                    return new("Cuộc trò chuyện này không khớp tài liệu đang chọn. Hãy tạo đoạn chat mới.", [], true, activeConversationId);
            }
            else if (historyScoped)
            {
                return new("Cuộc trò chuyện này gắn với một tài liệu cụ thể. Hãy tạo đoạn chat mới để hỏi theo cả môn.", [], true, activeConversationId);
            }
        }

        await chats.AddMessageAsync(new ChatMessage
        {
            SessionId = session.Id,
            ConversationId = activeConversationId,
            DocumentId = scopedDocumentId,
            Role = DataMessageRole.User,
            Content = trimmedQuestion
        }, ct);

        try
        {
            var query = await embeddings.EmbedAsync(trimmedQuestion, EmbeddingTask.Query, ct);
            var found = await vectors.SearchAsync(courseId, scopedDocumentId, query, options.TopK, ct);

            if (found.Count == 0 || found.Max(x => x.Score) < options.SimilarityThreshold)
            {
                const string refusal = "Xin lỗi, câu hỏi của bạn không nằm trong phạm vi tài liệu học tập của môn học này.";
                await chats.AddMessageAsync(new ChatMessage
                {
                    SessionId = session.Id,
                    ConversationId = activeConversationId,
                    DocumentId = scopedDocumentId,
                    Role = DataMessageRole.Assistant,
                    Content = refusal,
                    CitationsJson = "[]"
                }, ct);
                await ChargeStudentQuestionAsync(userId, role, ct);
                return new(refusal, [], true, activeConversationId);
            }

            var answerContext = found.Select(x => new AnswerChunkContext(
                x.ChunkId, x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber, x.Content, x.Score)).ToList();

            var historyContext = history.Select(x => new AnswerMessageContext((BusinessMessageRole)x.Role, x.Content)).ToList();
            var answer = await answers.GenerateAsync(trimmedQuestion, answerContext, historyContext, ct);

            var citations = found.Select(x => new CitationDto(x.DocumentId, x.FileName, x.PageNumber, x.ChunkNumber,
                x.Content.Length <= 320 ? x.Content : x.Content[..320] + "…")).ToList();

            await chats.AddMessageAsync(new ChatMessage
            {
                SessionId = session.Id,
                ConversationId = activeConversationId,
                DocumentId = scopedDocumentId,
                Role = DataMessageRole.Assistant,
                Content = answer,
                CitationsJson = JsonSerializer.Serialize(citations)
            }, ct);

            await ChargeStudentQuestionAsync(userId, role, ct);
            return new(answer, citations, ConversationId: activeConversationId);
        }
        catch (InvalidOperationException ex)
        {
            var error = string.IsNullOrWhiteSpace(ex.Message)
                ? "Hệ thống AI tạm thời không phản hồi. Vui lòng thử lại sau."
                : ex.Message;
            await chats.AddMessageAsync(new ChatMessage
            {
                SessionId = session.Id,
                ConversationId = activeConversationId,
                DocumentId = scopedDocumentId,
                Role = DataMessageRole.Assistant,
                Content = error,
                CitationsJson = "[]"
            }, ct);
            return new(error, [], true, activeConversationId);
        }
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

    private async Task ChargeStudentQuestionAsync(
        Guid userId,
        BusinessUserRole role,
        CancellationToken ct)
    {
        if (role != BusinessUserRole.Student || !studentUsage.Enabled) return;
        await tokenUsage.AddTokensAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), 1, ct);
    }

    private async Task<StudentTokenQuotaDto> BuildQuotaAsync(Guid userId, CancellationToken ct)
    {
        var limits = await subscriptions.ResolveLimitsAsync(userId, ct);
        if (!studentUsage.Enabled)
            return new(false, limits.QuestionsPerDay, 0, limits.QuestionsPerDay, limits.MaxQuestionChars, limits.PackageCode, limits.PackageName);

        var used = await tokenUsage.GetUsedTokensAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        var remaining = Math.Max(0, limits.QuestionsPerDay - used);
        return new(true, limits.QuestionsPerDay, used, remaining, limits.MaxQuestionChars, limits.PackageCode, limits.PackageName);
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
