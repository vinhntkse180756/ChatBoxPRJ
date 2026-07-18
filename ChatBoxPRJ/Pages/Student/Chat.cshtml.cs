using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Student;
public sealed class ChatModel(ICourseService courses, IChatService chat, IDocumentService documents) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CourseId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? DocumentId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? ConversationId { get; set; }
    [BindProperty] public string Question { get; set; } = "";
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    public ChatWorkspaceDto? Workspace { get; set; }
    public bool CanChat => CourseId.HasValue && Workspace is not null && Workspace.Documents.Count > 0;
    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAskAsync()
    {
        if (!CourseId.HasValue) return RedirectToPage();
        var result = await chat.AskAsync(User.UserId(), User.UserRole(), CourseId.Value, ConversationId, DocumentId, Question, HttpContext.RequestAborted);
        if (result.Rejected && result.ConversationId is null)
        {
            Flash = result.Answer;
            FlashType = "danger";
        }
        return RedirectToPage(new { courseId = CourseId, documentId = DocumentId, conversationId = result.ConversationId ?? ConversationId });
    }

    public async Task<IActionResult> OnPostDeleteHistoryAsync(Guid? historyConversationId, bool all = false)
    {
        if (!CourseId.HasValue || (!all && !historyConversationId.HasValue))
        {
            Flash = "Yêu cầu xóa lịch sử không hợp lệ.";
            FlashType = "danger";
            return RedirectToPage(new { courseId = CourseId, documentId = DocumentId });
        }

        var result = await chat.DeleteHistoryAsync(
            User.UserId(),
            User.UserRole(),
            CourseId.Value,
            all ? null : historyConversationId,
            HttpContext.RequestAborted);
        Flash = result.Message;
        FlashType = result.Success ? "success" : "danger";
        var deletedActiveConversation = all || ConversationId == historyConversationId;
        return RedirectToPage(new
        {
            courseId = CourseId,
            documentId = deletedActiveConversation ? null : DocumentId,
            conversationId = deletedActiveConversation ? null : ConversationId
        });
    }
    public async Task<IActionResult> OnGetFileAsync(Guid documentId, Guid courseId)
    {
        var file = await documents.GetFileAsync(documentId, courseId, User.UserId(), User.UserRole(), HttpContext.RequestAborted);
        if (file is null) return NotFound();
        var stream = new FileStream(file.Value.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, file.Value.ContentType, file.Value.FileName);
    }

    private async Task LoadAsync()
    {
        Courses = User.UserRole() == UserRole.Lecturer
            ? await courses.ListForLecturerAsync(User.UserId(), HttpContext.RequestAborted)
            : await courses.ListAsync(HttpContext.RequestAborted);
        if (CourseId.HasValue && Courses.All(x => x.Id != CourseId.Value)) CourseId = null;
        CourseId ??= Courses.FirstOrDefault()?.Id;
        if (CourseId.HasValue) Workspace = await chat.OpenWorkspaceAsync(User.UserId(), User.UserRole(), CourseId.Value, ConversationId, HttpContext.RequestAborted);
        if (Workspace is not null && ConversationId.HasValue)
        {
            var activeHistory = Workspace.Histories.FirstOrDefault(x => x.ConversationId == ConversationId.Value);
            if (activeHistory is null) ConversationId = null;
            else DocumentId = activeHistory.DocumentId;
        }
        if (Workspace is not null && !ConversationId.HasValue && DocumentId.HasValue
            && Workspace.Documents.All(x => x.Id != DocumentId.Value)
            && Workspace.Histories.All(x => x.DocumentId != DocumentId.Value)) DocumentId = null;
    }
}
