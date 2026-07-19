using BusinessLogic.DTOs;
using BusinessLogic.Interfaces;
using Presentation.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Lecturer;

public sealed class ChunksModel(IDocumentService documents) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid DocumentId { get; set; }
    public DocumentChunksDto Document { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await documents.GetChunksAsync(DocumentId, User.UserId(), User.UserRole(), HttpContext.RequestAborted);
        if (result is null) return NotFound();
        Document = result;
        return Page();
    }
}
