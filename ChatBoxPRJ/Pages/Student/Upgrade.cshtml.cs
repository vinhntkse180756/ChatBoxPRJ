using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Business.Options;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Student;

[Authorize(Policy = "StudentsOnly")]
public sealed class UpgradeModel(
    ISubscriptionService subscriptions,
    VnPayOptions vnPay) : PageModel
{
    public IReadOnlyList<SubscriptionPackageDto> Packages { get; private set; } = [];
    public StudentSubscriptionDto Current { get; private set; } = null!;
    public bool VnPayConfigured => vnPay.IsConfigured;
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

    public async Task<IActionResult> OnPostCheckoutAsync(Guid packageId)
    {
        await LoadAsync();
        try
        {
            var returnUrl = $"{Request.Scheme}://{Request.Host}{vnPay.ReturnPath}";
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            if (ip == "::1") ip = "127.0.0.1";

            var result = await subscriptions.StartCheckoutAsync(
                User.UserId(),
                packageId,
                ip,
                returnUrl,
                HttpContext.RequestAborted);

            if (!result.Success || string.IsNullOrWhiteSpace(result.PaymentUrl))
            {
                Flash = result.Message;
                FlashType = "danger";
                return Page();
            }

            return Redirect(result.PaymentUrl);
        }
        catch (Exception ex)
        {
            Flash = $"Không tạo được thanh toán VNPay: {ex.Message}";
            FlashType = "danger";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDowngradeFreeAsync()
    {
        var result = await subscriptions.DowngradeToFreeAsync(User.UserId(), HttpContext.RequestAborted);
        await LoadAsync();
        Flash = result.Message;
        FlashType = result.Success ? "success" : "warning";
        return Page();
    }

    private async Task LoadAsync()
    {
        var userId = User.UserId();
        Packages = await subscriptions.ListPackagesAsync(userId, HttpContext.RequestAborted);
        Current = await subscriptions.GetEffectiveSubscriptionAsync(userId, HttpContext.RequestAborted);
    }
}
