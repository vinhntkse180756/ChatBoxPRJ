using ChatBoxPRJ.Business.Domain;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Lecturer;
public sealed class DocumentsModel(ICourseService courses, IDocumentService documents) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CourseId { get; set; }
    [BindProperty] public IFormFile? UploadFile { get; set; }
    [BindProperty] public bool Overwrite { get; set; }
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    public IReadOnlyList<DocumentDto> Documents { get; set; } = [];
    [TempData] public string? Flash { get; set; }

    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (!CourseId.HasValue || UploadFile is null || UploadFile.Length == 0) { Flash = "Hãy chọn môn học và tệp cần xử lý."; return RedirectToPage(new { courseId = CourseId }); }
        if (!await courses.CanManageAsync(User.UserId(), User.UserRole(), CourseId.Value, HttpContext.RequestAborted)) return Forbid();
        await using var stream = UploadFile.OpenReadStream();
        var result = await documents.UploadAsync(new UploadRequest(CourseId.Value, User.UserId(), UploadFile.FileName, UploadFile.ContentType, stream, Overwrite), HttpContext.RequestAborted);
        Flash = result.Message;
        return RedirectToPage(new { courseId = CourseId });
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid documentId, Guid? courseId)
    {
        var result = await documents.DeleteAsync(documentId, User.UserId(), User.UserRole(), HttpContext.RequestAborted);
        Flash = result.Message;
        return RedirectToPage(new { courseId });
    }
    private async Task LoadAsync()
    {
        Courses = User.UserRole() == UserRole.Admin ? await courses.ListAsync(HttpContext.RequestAborted) : await courses.ListForLecturerAsync(User.UserId(), HttpContext.RequestAborted);
        if (!CourseId.HasValue && User.UserRole() == UserRole.Lecturer) CourseId = Courses.FirstOrDefault()?.Id;
        if (CourseId.HasValue && !await courses.CanManageAsync(User.UserId(), User.UserRole(), CourseId.Value, HttpContext.RequestAborted)) { CourseId = null; }
        Documents = await documents.ListAsync(CourseId, false, HttpContext.RequestAborted);
    }
}
