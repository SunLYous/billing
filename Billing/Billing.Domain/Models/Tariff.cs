namespace Billing.Domain.Models;

public sealed class Tariff
{
    public string Prefix { get; init; } = default!;
    public string Destination { get; init; } = default!;
    public decimal RatePerMinute { get; init; }
    public decimal ConnectionFee { get; init; }
    public TimeOnly TimeFrom { get; init; }
    public TimeOnly TimeTo { get; init; }
    public DayOfWeek[] Weekdays { get; init; } = [];
    public int Priority { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public DateOnly ExpiryDate { get; init; }
}