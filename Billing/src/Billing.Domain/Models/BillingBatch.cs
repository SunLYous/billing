namespace Billing.Domain.Models;

public sealed class BillingBatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Pending;
    public int TotalCalls { get; set; }
    public int ProcessedCalls { get; set; }
    public string? ErrorMessage { get; set; }
 
    public TimeSpan? Elapsed => CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
}
 
public enum BatchStatus
{
    Pending = 0,
    Parsing = 1,
    Rating = 2,
    Completed = 3,
    Failed = 4
}