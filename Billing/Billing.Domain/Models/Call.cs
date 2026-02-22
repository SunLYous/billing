namespace Billing.Domain.Models;

public sealed class Call
{
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public string CallingParty { get; init; } = default!;
    public string CalledParty { get; init; } = default!;
    public CallDirection Direction { get; init; }
    public CallDisposition Disposition { get; init; }
    public int Duration { get; init; }
    public int BillableSeconds { get; init; }
    public decimal? ChargeFromPbx { get; init; }
    public string? AccountCode { get; init; }
    public string CallId { get; init; } = default!;
    public string? TrunkName { get; init; }
}