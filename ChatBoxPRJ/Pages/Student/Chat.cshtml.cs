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
    [BindProperty] public string Question { get; set; } = "";
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    public ChatWorkspaceDto? Workspace { get; set; }
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAskAsync()
    {
        if (!CourseId.HasValue || !DocumentId.HasValue) return RedirectToPage(new { courseId = CourseId });
        await chat.AskAsync(User.UserId(), CourseId.Value, DocumentId.Value, Question, HttpContext.RequestAborted);
        return RedirectToPage(new { courseId = CourseId, documentId = DocumentId });
    }
    public async Task<IActionResult> OnGetFileAsync(Guid documentId, Guid courseId, bool download = false)
    {
        var file = await documents.GetStudentFileAsync(documentId, courseId, HttpContext.RequestAborted);
        if (file is null) return NotFound();
        var stream = new FileStream(file.Value.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (download) return File(stream, file.Value.ContentType, file.Value.FileName);
        return new FileStreamResult(stream, file.Value.ContentType) { EnableRangeProcessing = true };
    }

    private async Task LoadAsync()
    {
        Courses = await courses.ListAsync(HttpContext.RequestAborted);
        CourseId ??= Courses.FirstOrDefault()?.Id;
        if (CourseId.HasValue) Workspace = await chat.OpenWorkspaceAsync(User.UserId(), CourseId.Value, DocumentId, HttpContext.RequestAborted);
        if (Workspace is not null && DocumentId.HasValue && Workspace.Documents.All(x => x.Id != DocumentId.Value)) DocumentId = null;
    }
}
