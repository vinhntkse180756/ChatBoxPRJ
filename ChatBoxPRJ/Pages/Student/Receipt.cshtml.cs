using System.Threading.Tasks;
using ChatBoxPRJ.DataAccess.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ChatBoxPRJ.Pages.Student;

[Authorize(Roles = "Student")]
public sealed class ReceiptModel(ChatBoxDbContext db) : PageModel
{
    public string TransactionNo { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string PackageName { get; private set; } = "";
    public int CreditsAdded { get; private set; }
    public string Status { get; private set; } = "";
    public string CreatedAtLocal { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string transactionNo)
    {
        var transaction = await db.PaymentTransactions
            .Include(x => x.Package)
            .FirstOrDefaultAsync(x => x.TransactionNo == transactionNo, HttpContext.RequestAborted);

        if (transaction is null)
            return RedirectToPage("/Student/Packages");

        TransactionNo = transactionNo;
        Amount = transaction.Amount;
        PackageName = transaction.Package.Name;
        CreditsAdded = transaction.Package.Credits;
        Status = transaction.Status;
        CreatedAtLocal = transaction.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        
        return Page();
    }
}
