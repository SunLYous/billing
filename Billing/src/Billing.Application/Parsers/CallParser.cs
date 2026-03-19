using System.Globalization;
using Billing.Domain.Models;

namespace Billing.Application.Parsers;

public sealed class CallParser : ICallParser
{
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "dd.MM.yyyy HH:mm:ss",
        "dd.MM.yyyy HH:mm",
        "yyyy-MM-ddTHH:mm:ss"
    };

    public async Task<IReadOnlyList<Call>> ParseAsync(Stream stream, Guid batchId, CancellationToken ct = default)
    {
        var calls = new List<Call>(65_536);
        using var reader = new StreamReader(stream, leaveOpen: true);

        string? line;

        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 12)
                continue;

            if (!DateTime.TryParseExact(parts[0], DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var startTime))
                continue;

            DateTime.TryParseExact(parts[1], DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var endTime);

            int.TryParse(parts[6], out var duration);
            int.TryParse(parts[7], out var billableSec);

            decimal.TryParse(parts[8].Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var charge);

            calls.Add(new Call
            {
                StartTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc),
                EndTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc),
                CallingParty = parts[2],
                CalledParty = parts[3],
                CallDirection = parts[4],
                Disposition = parts[5],
                Duration = duration,
                BillableSeconds = billableSec,
                PbxCharge = charge,
                AccountCode = string.IsNullOrEmpty(parts[9]) ? null : parts[9],
                CallId = parts[10],
                TrunkName = string.IsNullOrEmpty(parts[11]) ? null : parts[11],
                BatchId = batchId
            });
        }

        return calls;
    }
}
