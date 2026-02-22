using Billing.Application.Parsers.TariffParser;
using Billing.Domain.Models;

namespace Billing.Infrastructure.Parsers;

public sealed class TariffParser : ITariffParser
{
    public async Task<IReadOnlyCollection<Tariff>> ParseAsync(Stream stream)
    {
        var result = new List<Tariff>();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var p = line.Split(';');

            var time = p[4].Split('-');
            var days = ParseWeekdays(p[5]);

            result.Add(new Tariff
            {
                Prefix = p[0],
                Destination = p[1],
                RatePerMinute = decimal.Parse(p[2]),
                ConnectionFee = decimal.Parse(p[3]),
                TimeFrom = TimeOnly.Parse(time[0]),
                TimeTo = TimeOnly.Parse(time[1]),
                Weekdays = days,
                Priority = int.Parse(p[6]),
                EffectiveDate = DateOnly.Parse(p[7]),
                ExpiryDate = DateOnly.Parse(p[8])
            });
        }

        return result;
    }

    private static DayOfWeek[] ParseWeekdays(string value)
    {
        var range = value.Split('-');
        var start = int.Parse(range[0]);
        var end = int.Parse(range[1]);

        return Enumerable
            .Range(start, end - start + 1)
            .Select(d => (DayOfWeek)(d % 7))
            .ToArray();
    }
}