using ChatBoxPRJ.DataAccess.Models;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Lecturer;
public sealed class DocumentsModel(ICourseService courses, IDocumentService documents) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? CourseId { get; set; }
    [BindProperty] public Guid? UploadCourseId { get; set; }
    [BindProperty] public IFormFile? UploadFile { get; set; }
    [BindProperty] public bool Overwrite { get; set; }
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    public IReadOnlyList<CourseDto> UploadCourses { get; set; } = [];
    public IReadOnlyList<DocumentDto> Documents { get; set; } = [];
    public LecturerAccessLevel? AccessLevel { get; set; }
    public bool CanManageCourse => AccessLevel == LecturerAccessLevel.CourseHead;
    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }

    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostUploadAsync()
    {
        if (!UploadCourseId.HasValue || UploadFile is null || UploadFile.Length == 0) { Flash = "Hãy chọn môn được cấp quyền upload và tệp cần xử lý."; FlashType = "danger"; return RedirectToPage(new { courseId = CourseId }); }
        if (!await courses.CanManageAsync(User.UserId(), User.UserRole(), UploadCourseId.Value, HttpContext.RequestAborted)) return Forbid();
        await using var stream = UploadFile.OpenReadStream();
        var result = await documents.UploadAsync(new UploadRequest(UploadCourseId.Value, User.UserId(), UploadFile.FileName, UploadFile.ContentType, stream, Overwrite), HttpContext.RequestAborted);
        Flash = result.Success ? $"Upload tài liệu thành công. {result.Message}" : $"Tài liệu bị từ chối. Lý do: {result.Message}";
        FlashType = result.Success ? "success" : "danger";
        return RedirectToPage(new { courseId = UploadCourseId });
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid documentId, Guid? courseId)
    {
        var result = await documents.DeleteAsync(documentId, User.UserId(), User.UserRole(), HttpContext.RequestAborted);
        Flash = result.Message;
        FlashType = result.Success ? "success" : "danger";
        return RedirectToPage(new { courseId });
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
        Courses = await courses.ListForLecturerAsync(User.UserId(), HttpContext.RequestAborted);
        var assignments = await courses.GetAssignmentsAsync(User.UserId(), HttpContext.RequestAborted);
        UploadCourses = Courses
            .Where(x => assignments.GetValueOrDefault(x.Id) == LecturerAccessLevel.CourseHead)
            .ToList();
        UploadCourseId ??= UploadCourses.FirstOrDefault()?.Id;
        if (CourseId.HasValue && Courses.All(x => x.Id != CourseId.Value)) CourseId = null;
        CourseId ??= Courses.FirstOrDefault()?.Id;
        if (!CourseId.HasValue) return;
        AccessLevel = await courses.GetLecturerAccessLevelAsync(User.UserId(), CourseId.Value, HttpContext.RequestAborted);
        Documents = await documents.ListAsync(CourseId, completedOnly: !CanManageCourse, HttpContext.RequestAborted);
    }
}
