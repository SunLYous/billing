using System.Globalization;
using Billing.Domain.Models;

namespace Billing.Application.Parsers;

public sealed class TariffParser : ITariffParser
{
    public async Task<IReadOnlyList<Tariff>> ParseAsync(Stream stream, Guid batchId, CancellationToken ct = default)
    {
        var tariffs = new List<Tariff>(1024);
        using var reader = new StreamReader(stream, leaveOpen: true);

        var isFirstLine = true;
        string? line;

        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (isFirstLine)
            {
                isFirstLine = false;
                if (line.Contains("prefix", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("тариф", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("rate", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var parts = line.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
                continue;

            if (!decimal.TryParse(parts[2].Replace(',', '.'), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var rate))
                continue;

            decimal connectionFee = 0;
            if (parts.Length > 3)
                decimal.TryParse(parts[3].Replace(',', '.'), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out connectionFee);

            string? timeband = parts.Length > 4 && !string.IsNullOrEmpty(parts[4]) ? parts[4] : null;
            string? weekday = parts.Length > 5 && !string.IsNullOrEmpty(parts[5]) ? parts[5] : null;

            int priority = 0;
            if (parts.Length > 6)
                int.TryParse(parts[6], out priority);

            DateOnly? effectiveDate = null;
            if (parts.Length > 7 && DateOnly.TryParse(parts[7], CultureInfo.InvariantCulture, out var ed))
                effectiveDate = ed;

            DateOnly? expiryDate = null;
            if (parts.Length > 8 && DateOnly.TryParse(parts[8], CultureInfo.InvariantCulture, out var xd))
                expiryDate = xd;

            tariffs.Add(new Tariff
            {
                Prefix = parts[0],
                Destination = parts[1],
                RatePerMinute = rate,
                ConnectionFee = connectionFee,
                Timeband = timeband,
                Weekday = weekday,
                Priority = priority,
                EffectiveDate = effectiveDate,
                ExpiryDate = expiryDate,
                BatchId = batchId
            });
        }

        return tariffs;
    }
}
