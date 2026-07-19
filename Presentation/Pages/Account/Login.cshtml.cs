using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Account;

public sealed class LoginModel(IAccountService accounts) : PageModel
{
    [BindProperty, Required] public string Login { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await accounts.AuthenticateAsync(Login, Password, HttpContext.RequestAborted);
        if (user is null) { ErrorMessage = "Tên đăng nhập hoặc mật khẩu không đúng."; return Page(); }
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.ToString()), new Claim("code", user.Code)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)) return LocalRedirect(ReturnUrl);
        return user.Role switch
        {
            UserRole.Student => RedirectToPage("/Student/Courses"),
            UserRole.Lecturer => RedirectToPage("/Lecturer/Documents"),
            _ => RedirectToPage("/Admin/Index")
        };
    }
}
