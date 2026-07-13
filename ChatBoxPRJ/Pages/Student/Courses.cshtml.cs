using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Student;

public sealed class CoursesModel(ICourseService courses) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }

    public IReadOnlyList<CourseDto> Courses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var allCourses = await courses.ListAsync(HttpContext.RequestAborted);

        var keyword = Search?.Trim();
        Courses = string.IsNullOrWhiteSpace(keyword)
            ? allCourses
            : allCourses.Where(course => course.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
