using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Presentation.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Student;

public sealed class CoursesModel(ICourseService courses, IDocumentService documents) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CourseId { get; set; }

    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];
    public CourseDto? SelectedCourse { get; private set; }
    public IReadOnlyList<DocumentDto> Documents { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var allCourses = await courses.ListAsync(HttpContext.RequestAborted);
        SelectedCourse = CourseId.HasValue
            ? allCourses.FirstOrDefault(course => course.Id == CourseId.Value)
            : null;

        var keyword = Search?.Trim();
        Courses = string.IsNullOrWhiteSpace(keyword)
            ? allCourses
            : allCourses.Where(course => course.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        if (SelectedCourse is not null)
            Documents = await documents.ListAsync(SelectedCourse.Id, completedOnly: true, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid documentId, Guid courseId)
    {
        var file = await documents.GetFileAsync(
            documentId,
            courseId,
            User.UserId(),
            User.UserRole(),
            HttpContext.RequestAborted);

        if (file is null) return NotFound();
        var stream = new FileStream(file.Value.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, file.Value.ContentType, file.Value.FileName);
    }
}
