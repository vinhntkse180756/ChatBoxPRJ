using System.ComponentModel.DataAnnotations;
using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Admin;
public sealed class LecturersModel(IAccountService accounts) : PageModel
{
    [BindProperty, Required] public string Code { get; set; } = "";
    [BindProperty, Required] public string FullName { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, MinLength(6)] public string Password { get; set; } = "";
    [BindProperty] public Guid EditId { get; set; }
    public IReadOnlyList<UserDto> Lecturers { get; set; } = [];
    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashType { get; set; }
    public async Task OnGetAsync() => Lecturers = await accounts.ListLecturersAsync(HttpContext.RequestAborted);
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        var result = await accounts.CreateLecturerAsync(Code, FullName, Email, Password, HttpContext.RequestAborted);
        Flash = result.Message;
        FlashType = result.Success ? "success" : "danger";
        if (!result.Success) { await OnGetAsync(); return Page(); }
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostUpdateAsync() { ModelState.Remove(nameof(Password)); var r = await accounts.UpdateLecturerAsync(EditId, Code, FullName, Email, Password, HttpContext.RequestAborted); Flash = r.Message; FlashType = r.Success ? "success" : "danger"; return RedirectToPage(); }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id) { var r = await accounts.DeleteLecturerAsync(id, HttpContext.RequestAborted); Flash = r.Message; FlashType = r.Success ? "success" : "danger"; return RedirectToPage(); }
}
