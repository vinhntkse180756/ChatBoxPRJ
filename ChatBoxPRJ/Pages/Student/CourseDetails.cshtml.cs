using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Student;

public sealed class CourseDetailsModel(ICourseService courses, IDocumentService documents) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid CourseId { get; set; }

    public CourseDto? Course { get; private set; }
    public IReadOnlyList<DocumentDto> Documents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (CourseId == Guid.Empty)
        {
            return RedirectToPage("/Student/Courses");
        }

        var allCourses = await courses.ListAsync(HttpContext.RequestAborted);
        Course = allCourses.FirstOrDefault(c => c.Id == CourseId);

        if (Course is null)
        {
            return NotFound();
        }

        Documents = await documents.ListAsync(Course.Id, completedOnly: true, HttpContext.RequestAborted);
        return Page();
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
