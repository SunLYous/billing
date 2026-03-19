using Billing.Domain.Models;

namespace Billing.Application.Parsers;

public sealed class SubscriberParser : ISubscriberParser
{
    public async Task<IReadOnlyList<Subscriber>> ParseAsync(Stream stream, Guid batchId, CancellationToken ct = default)
    {
        var subscribers = new List<Subscriber>(4096);
        using var reader = new StreamReader(stream, leaveOpen: true);

        var isFirstLine = true;
        string? line;

        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (isFirstLine)
            {
                isFirstLine = false;
                if (line.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("номер", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var parts = line.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
                continue;

            subscribers.Add(new Subscriber
            {
                PhoneNumber = parts[0],
                ClientName = parts[1],
                BatchId = batchId
            });
        }

        return subscribers;
    }
}
