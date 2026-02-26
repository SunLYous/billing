namespace Billing.Domain.Models;

public sealed class RatedCall
{
    public Call Call { get; init; } = default!;
    public Tariff AppliedTariff { get; init; } = default!;
    public decimal CalculatedCost { get; init; }
}