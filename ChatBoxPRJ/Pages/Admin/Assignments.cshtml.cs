using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Admin;
public sealed class AssignmentsModel(IAccountService accounts, ICourseService courses) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid? LecturerId { get; set; }
    [BindProperty] public List<CoursePermissionInput> Permissions { get; set; } = [];
    public IReadOnlyList<UserDto> Lecturers { get; set; } = [];
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    public IReadOnlyDictionary<Guid, LecturerAccessLevel> Current { get; set; } = new Dictionary<Guid, LecturerAccessLevel>();
    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }
    public async Task OnGetAsync()
    {
        await LoadAsync();
        if (LecturerId.HasValue) Current = await courses.GetAssignmentsAsync(LecturerId.Value, HttpContext.RequestAborted);
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!LecturerId.HasValue) { Flash = "Hãy chọn một giảng viên."; FlashType = "danger"; return RedirectToPage(); }
        if (!ModelState.IsValid || Permissions.GroupBy(x => x.CourseId).Any(group => group.Count() > 1))
        {
            Flash = "Dữ liệu phân quyền không hợp lệ.";
            FlashType = "danger";
            return RedirectToPage(new { lecturerId = LecturerId });
        }
        var assignments = Permissions
            .Where(x => x.AccessLevel.HasValue)
            .ToDictionary(x => x.CourseId, x => x.AccessLevel!.Value);
        var result = await courses.SaveAssignmentsAsync(LecturerId.Value, assignments, HttpContext.RequestAborted);
        Flash = result.Message;
        FlashType = result.Success ? "success" : "danger";
        return RedirectToPage(new { lecturerId = LecturerId });
    }
    private async Task LoadAsync() { Lecturers = await accounts.ListLecturersAsync(HttpContext.RequestAborted); Courses = await courses.ListAsync(HttpContext.RequestAborted); }

    public sealed class CoursePermissionInput
    {
        public Guid CourseId { get; set; }
        public LecturerAccessLevel? AccessLevel { get; set; }
    }
}
