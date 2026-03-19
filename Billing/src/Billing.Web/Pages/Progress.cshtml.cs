using Billing.Domain.Interfaces;
using Billing.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Billing.Web.Pages;

public class ProgressModel : PageModel
{
    private readonly IBatchRepository _batchRepo;

    public ProgressModel(IBatchRepository batchRepo) => _batchRepo = batchRepo;

    [BindProperty(SupportsGet = true)] public Guid BatchId { get; set; }
    public BillingBatch? Batch { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (BatchId == Guid.Empty)
            return RedirectToPage("Index");

        Batch = await _batchRepo.GetByIdAsync(BatchId, ct);
        if (Batch is null)
            return RedirectToPage("Index");

        // Если уже завершён — сразу на результат
        if (Batch.Status == BatchStatus.Completed)
            return RedirectToPage("Result", new { batchId = BatchId });

        return Page();
    }
}
