using System.ComponentModel.DataAnnotations;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Account;

public sealed class RegisterModel(IAccountService accounts) : PageModel
{
    [BindProperty, Required, StringLength(30)] public string Code { get; set; } = "";
    [BindProperty, Required, StringLength(120)] public string FullName { get; set; } = "";
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, MinLength(6), DataType(DataType.Password)] public string Password { get; set; } = "";
    public string? Message { get; set; }
    public bool Success { get; set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        (Success, Message) = await accounts.RegisterStudentAsync(Code, FullName, Email, Password, HttpContext.RequestAborted);
        if (Success) return RedirectToPage("/Account/Login", new { registered = true });
        return Page();
    }
}
