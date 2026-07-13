using System.ComponentModel.DataAnnotations;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Admin;
public sealed class CoursesModel(ICourseService service) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập mã môn học.")] public string Code { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập tên môn học.")] public string Name { get; set; } = "";
    [BindProperty, Range(1, 20, ErrorMessage = "Số tín chỉ phải từ 1 đến 20.")] public int Credits { get; set; } = 3;
    [BindProperty, Required(ErrorMessage = "Vui lòng nhập mô tả môn học.")] public string Description { get; set; } = "";
    [BindProperty] public Guid EditId { get; set; }
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
    public async Task<IActionResult> OnPostUpdateAsync() { var r = await service.UpdateAsync(EditId, Code, Name, Credits, Description, HttpContext.RequestAborted); Flash = r.Message; return RedirectToPage(); }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id) { var r = await service.DeleteAsync(id, HttpContext.RequestAborted); Flash = r.Message; return RedirectToPage(); }
}
