namespace Billing.Domain.Models;

public sealed class Call
{
    public long Id { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public string CallingParty { get; init; } = string.Empty;
    public string CalledParty { get; init; } = string.Empty;

    public string CallDirection { get; init; } = string.Empty;

    public string Disposition { get; init; } = string.Empty;

    public int Duration { get; init; }

    public int BillableSeconds { get; init; }

    public decimal PbxCharge { get; init; }

    public string? AccountCode { get; init; }

    public string CallId { get; init; } = string.Empty;

    public string? TrunkName { get; init; }

    public Guid BatchId { get; init; }

    public bool IsBillable =>
        CallDirection.Equals("outgoing", StringComparison.OrdinalIgnoreCase) &&
        Disposition.Equals("answered", StringComparison.OrdinalIgnoreCase) &&
        BillableSeconds > 0;
}
