namespace Billing.Application.DTOs;

public sealed record BatchStatusDto(
    Guid BatchId,
    string Status,
    int TotalCalls,
    int ProcessedCalls,
    int ProgressPercent);

public sealed record SubscriberTotalDto(string Name, decimal Total);

public sealed record RatedCallDto(
    DateTime StartTime,
    string CallingParty,
    string CalledParty,
    string Destination,
    int Minutes,
    decimal RatePerMinute,
    decimal ConnectionFee,
    decimal MinutesCost,
    decimal TotalCost);

public sealed record BillingResultDto(
    Guid BatchId,
    IReadOnlyList<SubscriberTotalDto> Totals,
    IReadOnlyList<RatedCallDto> Calls,
    int TotalCount,
    int Page,
    int PageSize);
