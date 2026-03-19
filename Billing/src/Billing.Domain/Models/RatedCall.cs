namespace Billing.Domain.Models;

public sealed class RatedCall
{
    public long Id { get; init; }
    public long CallId { get; init; }
    public Call Call { get; init; } = null!;
    public int TariffId { get; init; }
    public Tariff AppliedTariff { get; init; } = null!;

    public decimal ConnectionFee { get; init; }

    public decimal MinutesCost { get; init; }

    public decimal CalculatedCost { get; init; }

    public Guid BatchId { get; init; }
}
