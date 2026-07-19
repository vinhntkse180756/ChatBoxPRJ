using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Admin;

public sealed class StudentsModel(ISubscriptionService subscriptions) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string PackageFilter { get; set; } = "ALL";

    public AdminStudentsDashboardDto Dashboard { get; private set; } = null!;
    public IReadOnlyList<AdminStudentAccountDto> FilteredStudents { get; private set; } = [];
    public string? Flash { get; private set; }
    public string FlashType { get; private set; } = "info";

    public async Task OnGetAsync(string? status, string? message)
    {
        await LoadAsync();
        if (!string.IsNullOrWhiteSpace(message))
        {
            Flash = message;
            FlashType = string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase) ? "success" : "danger";
        }
    }

    public async Task<IActionResult> OnPostGrantAsync(Guid studentId, string packageCode)
    {
        var result = await subscriptions.GrantPackageAsync(studentId, packageCode, HttpContext.RequestAborted);
        return RedirectToPage(new
        {
            Search,
            PackageFilter,
            status = result.Success ? "ok" : "fail",
            message = result.Message
        });
    }

    private async Task LoadAsync()
    {
        Dashboard = await subscriptions.GetAdminStudentsDashboardAsync(HttpContext.RequestAborted);
        IEnumerable<AdminStudentAccountDto> query = Dashboard.Students;

        if (!string.IsNullOrWhiteSpace(PackageFilter) && !PackageFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.PackageCode.Equals(PackageFilter, StringComparison.OrdinalIgnoreCase));

        var keyword = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || x.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || x.Email.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        FilteredStudents = query.ToList();
    }
}
