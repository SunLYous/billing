using Billing.Application.DTOs;
using Billing.Domain.Interfaces;
using Billing.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Billing.Web.Pages;

public class ResultModel : PageModel
{
    private readonly IResultRepository _resultRepo;
    private readonly IBatchRepository _batchRepo;

    public ResultModel(IResultRepository resultRepo, IBatchRepository batchRepo)
    {
        _resultRepo = resultRepo;
        _batchRepo = batchRepo;
    }

    [BindProperty(SupportsGet = true)] public Guid BatchId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 100;

    public List<SubscriberTotalDto> Totals { get; private set; } = new();
    public IReadOnlyList<RatedCall> Calls { get; private set; } = Array.Empty<RatedCall>();
    public int TotalCount { get; private set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public BillingBatch? Batch { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (BatchId == Guid.Empty)
            return RedirectToPage("Index");

        Batch = await _batchRepo.GetByIdAsync(BatchId, ct);
        if (Batch is null)
            return RedirectToPage("Index");

        if (Batch.Status != BatchStatus.Completed)
        {
            // Batch ещё обрабатывается
            return Page();
        }

        // Параллельные запросы: итоги + детализация + count
        var totalsTask = _resultRepo.GetTotalsByBatchIdAsync(BatchId, ct);
        var callsTask = _resultRepo.GetByBatchIdAsync(BatchId, PageNumber, PageSize, ct);
        var countTask = _resultRepo.GetCountByBatchIdAsync(BatchId, ct);

        await Task.WhenAll(totalsTask, callsTask, countTask);

        Totals = totalsTask.Result
            .Select(t => new SubscriberTotalDto(t.Name, t.Total))
            .ToList();

        Calls = callsTask.Result;
        TotalCount = countTask.Result;

        return Page();
    }
}
