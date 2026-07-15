using System.Collections.Generic;
using System.Threading.Tasks;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Student;

[Authorize(Roles = "Student")]
public sealed class PackagesModel(IBillingService billingService) : PageModel
{
    public IReadOnlyList<BillingPackageDto> Packages { get; private set; } = [];
    public IReadOnlyList<PaymentTransactionDto> Transactions { get; private set; } = [];
    public int CurrentCredits { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = User.UserId();
        Packages = await billingService.ListPackagesAsync(HttpContext.RequestAborted);
        Transactions = await billingService.ListUserTransactionsAsync(userId, HttpContext.RequestAborted);
        CurrentCredits = await billingService.GetUserCreditsAsync(userId, HttpContext.RequestAborted);
    }
}
