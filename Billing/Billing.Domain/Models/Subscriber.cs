namespace Billing.Domain.Models;

public sealed class Subscriber
{
    public string PhoneNumber { get; init; } = default!;
    public string ClientName { get; init; } = default!;
}