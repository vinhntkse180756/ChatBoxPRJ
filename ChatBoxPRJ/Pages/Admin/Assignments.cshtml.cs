using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Admin;
public sealed class AssignmentsModel(IAccountService accounts, ICourseService courses) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? LecturerId { get; set; }
    [BindProperty] public List<Guid> SelectedCourseIds { get; set; } = [];
    public IReadOnlyList<UserDto> Lecturers { get; set; } = [];
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    public IReadOnlySet<Guid> Current { get; set; } = new HashSet<Guid>();
    [TempData] public string? Flash { get; set; }
    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (LecturerId.HasValue) Current = await courses.GetAssignmentsAsync(LecturerId.Value, HttpContext.RequestAborted);
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!LecturerId.HasValue) { Flash = "Hãy chọn một giảng viên."; return RedirectToPage(); }
        var result = await courses.SaveAssignmentsAsync(LecturerId.Value, SelectedCourseIds.ToHashSet(), HttpContext.RequestAborted);
        Flash = result.Message;
        return RedirectToPage(new { lecturerId = LecturerId });
    }
    private async Task LoadAsync() { Lecturers = await accounts.ListLecturersAsync(HttpContext.RequestAborted); Courses = await courses.ListAsync(HttpContext.RequestAborted); }
}
