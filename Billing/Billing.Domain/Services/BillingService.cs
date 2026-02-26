using Billing.Domain.Models;

namespace Billing.Domain.Services;

public sealed class BillingService
{
    public IReadOnlyCollection<RatedCall> Rate(
        IReadOnlyCollection<Call> calls,
        IReadOnlyCollection<Tariff> tariffs,
        IProgress<int>? progress = null)
    {
        var callList = calls.ToList();
        var result = new List<RatedCall>();

        for (var i = 0; i < callList.Count; i++)
        {
            var call = callList[i];

            if (call.Disposition != CallDisposition.Answered ||
                call.Direction != CallDirection.Outgoing)
                continue;

            var applicableTariff = SelectTariff(call, tariffs);

            if (applicableTariff is null)
                continue;

            var minutes = Math.Ceiling(call.BillableSeconds / 60m);
            var cost = minutes * applicableTariff.RatePerMinute;

            if (call.BillableSeconds > 0)
                cost += applicableTariff.ConnectionFee;

            result.Add(new RatedCall
            {
                Call = call,
                AppliedTariff = applicableTariff,
                CalculatedCost = decimal.Round(cost, 2)
            });

            progress?.Report((i + 1) * 100 / callList.Count);
        }

        return result;
    }

    private static Tariff? SelectTariff(Call call, IReadOnlyCollection<Tariff> tariffs)
    {
        var callDate = DateOnly.FromDateTime(call.StartTime);
        var callTime = TimeOnly.FromDateTime(call.StartTime);
        var weekday = call.StartTime.DayOfWeek;

        return tariffs
            .Where(t =>
                call.CalledParty.StartsWith(t.Prefix) &&
                callDate >= t.EffectiveDate &&
                callDate <= t.ExpiryDate &&
                t.Weekdays.Contains(weekday) &&
                callTime >= t.TimeFrom &&
                callTime <= t.TimeTo)
            .OrderByDescending(t => t.Prefix.Length)
            .ThenByDescending(t => t.Priority)
            .FirstOrDefault();
    }
}