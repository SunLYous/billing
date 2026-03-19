using Billing.Domain.Interfaces;
using Billing.Domain.Models;
using Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Repositories;

public sealed class BatchRepository : IBatchRepository
{
    private readonly BillingDbContext _db;

    public BatchRepository(BillingDbContext db) => _db = db;

    public async Task<BillingBatch> CreateAsync(CancellationToken ct = default)
    {
        var batch = new BillingBatch();
        _db.Batches.Add(batch);
        await _db.SaveChangesAsync(ct);
        return batch;
    }

    public async Task UpdateStatusAsync(
        Guid batchId,
        BatchStatus status,
        int? totalCalls = null,
        int? processedCalls = null,
        string? error = null,
        CancellationToken ct = default)
    {
        var completedAt = status is BatchStatus.Completed or BatchStatus.Failed
            ? DateTime.UtcNow
            : (DateTime?)null;

        await _db.Batches
            .Where(b => b.Id == batchId)
            .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, status)
                    .SetProperty(b => b.TotalCalls, b => totalCalls ?? b.TotalCalls)
                    .SetProperty(b => b.ProcessedCalls, b => processedCalls ?? b.ProcessedCalls)
                    .SetProperty(b => b.ErrorMessage, b => error ?? b.ErrorMessage)
                    .SetProperty(b => b.CompletedAt, b => completedAt ?? b.CompletedAt),
                ct);
    }

    public async Task<BillingBatch?> GetByIdAsync(Guid batchId, CancellationToken ct = default)
    {
        return await _db.Batches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, ct);
    }
}