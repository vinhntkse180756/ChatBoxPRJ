using System.Text.Json;
using ChatBoxPRJ.Business.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Payments;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class VnPayIpnModel(ISubscriptionService subscriptions) : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        var query = Request.Query.ToDictionary(
            x => x.Key,
            x => x.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await subscriptions.CompleteVnPayIpnAsync(query, HttpContext.RequestAborted);
        var payload = new
        {
            RspCode = result.Success ? "00" : "97",
            Message = result.Success ? "Confirm Success" : result.Message
        };
        return Content(JsonSerializer.Serialize(payload), "application/json");
    }
}
