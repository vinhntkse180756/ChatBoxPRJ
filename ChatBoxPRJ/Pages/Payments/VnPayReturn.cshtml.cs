using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Payments;

[AllowAnonymous]
public sealed class VnPayReturnModel(ISubscriptionService subscriptions) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var query = Request.Query.ToDictionary(
            x => x.Key,
            x => x.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await subscriptions.CompleteVnPayReturnAsync(query, HttpContext.RequestAborted);
        var orderCode = result.OrderCode
            ?? query.GetValueOrDefault("vnp_TxnRef")
            ?? "";

        return RedirectToPage("/Payments/Result", new
        {
            orderCode,
            ok = result.Success ? 1 : 0,
            message = result.Message
        });
    }
}
