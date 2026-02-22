using Billing.Application.Parsers.SubscriberParser;
using Billing.Domain.Models;

namespace Billing.Infrastructure.Parsers;

public sealed class SubscriberParser : ISubscriberParser
{
    public async Task<IReadOnlyCollection<Subscriber>> ParseAsync(Stream stream)
    {
        var result = new List<Subscriber>();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var p = line.Split(';');

            result.Add(new Subscriber
            {
                PhoneNumber = p[0],
                ClientName = p[1]
            });
        }

        return result;
    }
}