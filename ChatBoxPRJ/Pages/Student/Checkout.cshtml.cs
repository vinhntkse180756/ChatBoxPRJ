using System;
using System.Linq;
using System.Threading.Tasks;
using ChatBoxPRJ.Business.DTOs;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatBoxPRJ.Pages.Student;

[Authorize(Roles = "Student")]
public sealed class CheckoutModel(IBillingService billingService) : PageModel
{
    public BillingPackageDto Package { get; private set; } = null!;
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid packageId)
    {
        var packages = await billingService.ListPackagesAsync(HttpContext.RequestAborted);
        var pkg = packages.FirstOrDefault(x => x.Id == packageId);
        if (pkg is null) return RedirectToPage("/Student/Packages");
        
        Package = pkg;
        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(Guid packageId)
    {
        var userId = User.UserId();
        var result = await billingService.CreateTransactionAsync(userId, packageId, HttpContext.RequestAborted);
        
        if (!result.Success || string.IsNullOrEmpty(result.TransactionNo))
        {
            ErrorMessage = result.Message;
            var packages = await billingService.ListPackagesAsync(HttpContext.RequestAborted);
            Package = packages.First(x => x.Id == packageId);
            return Page();
        }

        return RedirectToPage("/Student/MockPaymentPortal", new { transactionNo = result.TransactionNo });
    }
}
