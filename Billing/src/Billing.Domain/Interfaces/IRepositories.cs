using Billing.Domain.Models;

namespace Billing.Domain.Interfaces;

public interface IBulkRepository
{
    Task BulkInsertCallsAsync(IReadOnlyList<Call> calls, CancellationToken ct = default);
    Task BulkInsertTariffsAsync(IReadOnlyList<Tariff> tariffs, CancellationToken ct = default);
    Task BulkInsertSubscribersAsync(IReadOnlyList<Subscriber> subscribers, CancellationToken ct = default);
    Task BulkInsertRatedCallsAsync(IReadOnlyList<RatedCall> ratedCalls, CancellationToken ct = default);
}

public interface IBatchRepository
{
    Task<BillingBatch> CreateAsync(CancellationToken ct = default);
    Task UpdateStatusAsync(Guid batchId, BatchStatus status, int? totalCalls = null, int? processedCalls = null, string? error = null, CancellationToken ct = default);
    Task<BillingBatch?> GetByIdAsync(Guid batchId, CancellationToken ct = default);
}

public interface IResultRepository
{
    Task<IReadOnlyList<RatedCall>> GetByBatchIdAsync(Guid batchId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<(string Name, decimal Total)>> GetTotalsByBatchIdAsync(Guid batchId, CancellationToken ct = default);
    Task<int> GetCountByBatchIdAsync(Guid batchId, CancellationToken ct = default);
}
