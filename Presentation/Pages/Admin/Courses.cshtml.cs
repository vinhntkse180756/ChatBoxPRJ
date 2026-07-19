using System.ComponentModel.DataAnnotations;
using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Presentation.SignalR;

namespace Presentation.Pages.Admin;
public sealed class CoursesModel(ICourseService service, IHubContext<CourseHub> courseHub) : PageModel
{
    [BindProperty, Required] public string Code { get; set; } = "";
    [BindProperty, Required] public string Name { get; set; } = "";
    [BindProperty, Range(1, 20)] public int Credits { get; set; } = 3;
    [BindProperty] public string Description { get; set; } = "";
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

        // Lấy thông tin môn vừa tạo để broadcast qua SignalR
        var all = await service.ListAsync(HttpContext.RequestAborted);
        var created = all.FirstOrDefault(c => c.Code == Code && c.Name == Name);
        if (created is not null)
        {
            await courseHub.Clients.All.SendAsync("courseCreated", new
            {
                id = created.Id,
                code = created.Code,
                name = created.Name,
                credits = created.Credits,
                description = created.Description
            });
        }

        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostUpdateAsync() { var r = await service.UpdateAsync(EditId, Code, Name, Credits, Description, HttpContext.RequestAborted); Flash = r.Message; return RedirectToPage(); }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id) { var r = await service.DeleteAsync(id, HttpContext.RequestAborted); Flash = r.Message; return RedirectToPage(); }
}
