namespace Billing.Domain.Models;

public sealed class Subscriber
{
    public int Id { get; init; }
    public string PhoneNumber { get; init; } = string.Empty;
    public string ClientName { get; init; } = string.Empty;
    public Guid BatchId { get; init; }
}
