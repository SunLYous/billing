using System.Collections.Concurrent;
using Billing.Domain.Models;

namespace Billing.Domain.Services;

public sealed class BillingService
{
    public IReadOnlyList<RatedCall> Rate(
        IReadOnlyList<Call> calls,
        IReadOnlyList<Tariff> tariffs,
        Guid batchId,
        IProgress<int>? progress = null)
    {
        var matcher = new TariffMatcher();
        matcher.Build(tariffs);

        var results = new ConcurrentBag<RatedCall>();
        var processed = 0;
        var total = calls.Count;

        Parallel.ForEach(
            calls,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            call =>
            {
                if (!call.IsBillable)
                {
                    Interlocked.Increment(ref processed);
                    return;
                }

                var normalized = NormalizeNumber(call.CalledParty);
                var tariff = matcher.Match(normalized, call.StartTime);
                if (tariff is null)
                {
                    Interlocked.Increment(ref processed);
                    return;
                }

                var minutes = (int)Math.Ceiling(call.BillableSeconds / 60m);
                var minutesCost = minutes * tariff.RatePerMinute;
                var totalCost = tariff.ConnectionFee + minutesCost;

                results.Add(new RatedCall
                {
                    CallId = call.Id,
                    Call = call,
                    TariffId = tariff.Id,
                    AppliedTariff = tariff,
                    ConnectionFee = tariff.ConnectionFee,
                    MinutesCost = minutesCost,
                    CalculatedCost = totalCost,
                    BatchId = batchId
                });

                var current = Interlocked.Increment(ref processed);
                if (current % 5000 == 0 || current == total)
                    progress?.Report((int)((double)current / total * 100));
            });

        return results.ToArray();
    }

    private static string NormalizeNumber(string number)
    {
        if (number.Length > 0 && number[0] is >= '0' and <= '9' &&
            !number.AsSpan().ContainsAny("+-() "))
            return number;

        return string.Create(number.Length, number, static (span, src) =>
        {
            var pos = 0;
            foreach (var ch in src)
            {
                if (ch is >= '0' and <= '9')
                    span[pos++] = ch;
            }
            span[pos..].Fill('\0');
        }).TrimEnd('\0');
    }
}