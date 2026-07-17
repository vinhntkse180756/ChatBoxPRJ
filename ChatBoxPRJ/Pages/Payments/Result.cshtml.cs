using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Payments;

[AllowAnonymous]
public sealed class ResultModel(ISubscriptionService subscriptions) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? OrderCode { get; set; }
    [BindProperty(SupportsGet = true)] public int Ok { get; set; }
    [BindProperty(SupportsGet = true)] public string? Message { get; set; }

    public PaymentResultDto? Result { get; private set; }
    public bool Success { get; private set; }
    public string DisplayMessage { get; private set; } = "";

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(OrderCode))
            Result = await subscriptions.GetPaymentResultAsync(OrderCode, HttpContext.RequestAborted);

        Success = Result?.Success == true || Ok == 1;
        DisplayMessage = Result?.Message
            ?? Message
            ?? (Success ? "Thanh toán thành công." : "Thanh toán không thành công.");
    }
}
