using System.Threading.Tasks;
using ChatBoxPRJ.Business.Interfaces;
using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.Pages.Student;

[Authorize(Roles = "Student")]
public sealed class MockPaymentPortalModel(IBillingService billingService, ChatBoxDbContext db) : PageModel
{
    public string TransactionNo { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string PackageName { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string transactionNo)
    {
        var transaction = await db.PaymentTransactions
            .Include(x => x.Package)
            .FirstOrDefaultAsync(x => x.TransactionNo == transactionNo, HttpContext.RequestAborted);

        if (transaction is null || transaction.Status != "Pending")
            return RedirectToPage("/Student/Packages");

        TransactionNo = transactionNo;
        Amount = transaction.Amount;
        PackageName = transaction.Package.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostSuccessAsync(string transactionNo)
    {
        var result = await billingService.CompleteTransactionAsync(transactionNo, true, HttpContext.RequestAborted);
        return RedirectToPage("/Student/Receipt", new { transactionNo });
    }

    public async Task<IActionResult> OnPostCancelAsync(string transactionNo)
    {
        var result = await billingService.CompleteTransactionAsync(transactionNo, false, HttpContext.RequestAborted);
        return RedirectToPage("/Student/Receipt", new { transactionNo });
    }
}
