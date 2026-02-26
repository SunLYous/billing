using System.Globalization;
using Billing.Application.Parsers.TariffParser;
using Billing.Domain.Models;

namespace Billing.Infrastructure.Parsers;

public sealed class TariffParser : ITariffParser
{
    public async Task<IReadOnlyCollection<Tariff>> ParseAsync(Stream stream)
    {
        var result = new List<Tariff>(64);

        using var reader = new StreamReader(stream);

        await reader.ReadLineAsync();

        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var p = line.Split(';');

            if (p.Length != 9)
                throw new FormatException($"Invalid tariff line: {line}");

            var timeParts = p[4].Split('-', 2);

            result.Add(new Tariff
            {
                Prefix = p[0],
                Destination = p[1],
                RatePerMinute = decimal.Parse(p[2], CultureInfo.InvariantCulture),
                ConnectionFee = decimal.Parse(p[3], CultureInfo.InvariantCulture),
                TimeFrom = TimeOnly.Parse(timeParts[0], CultureInfo.InvariantCulture),
                TimeTo = TimeOnly.Parse(timeParts[1], CultureInfo.InvariantCulture),
                Weekdays = ParseWeekdays(p[5]),
                Priority = int.Parse(p[6], CultureInfo.InvariantCulture),
                EffectiveDate = DateOnly.Parse(p[7], CultureInfo.InvariantCulture),
                ExpiryDate = DateOnly.Parse(p[8], CultureInfo.InvariantCulture)
            });
        }

        return result;
    }

    private static DayOfWeek[] ParseWeekdays(string value)
    {
        var parts = value.Split('-', 2);

        var start = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var end = int.Parse(parts[1], CultureInfo.InvariantCulture);

        var days = new List<DayOfWeek>(7);

        for (var d = start; d <= end; d++)
        {
            // В файле: 1=Monday ... 7=Sunday
            var mapped = d == 7
                ? DayOfWeek.Sunday
                : (DayOfWeek)d;

            days.Add(mapped);
        }

        return days.ToArray();
    }
}