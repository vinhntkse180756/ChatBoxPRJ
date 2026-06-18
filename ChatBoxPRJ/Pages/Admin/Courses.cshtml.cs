using System.ComponentModel.DataAnnotations;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Admin;
public sealed class CoursesModel(ICourseService service) : PageModel
{
    [BindProperty, Required] public string Code { get; set; } = "";
    [BindProperty, Required] public string Name { get; set; } = "";
    [BindProperty, Range(1, 20)] public int Credits { get; set; } = 3;
    [BindProperty] public string Description { get; set; } = "";
    public IReadOnlyList<CourseDto> Courses { get; set; } = [];
    [TempData] public string? Flash { get; set; }
    public async Task OnGetAsync() => Courses = await service.ListAsync(HttpContext.RequestAborted);
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        var result = await service.CreateAsync(Code, Name, Credits, Description, HttpContext.RequestAborted);
        Flash = result.Message;
        if (!result.Success) { await OnGetAsync(); return Page(); }
        return RedirectToPage();
    }
}
