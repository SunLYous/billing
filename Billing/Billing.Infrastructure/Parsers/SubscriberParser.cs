using Billing.Application.Parsers.SubscriberParser;
using Billing.Domain.Models;

namespace Billing.Infrastructure.Parsers;

public sealed class SubscriberParser : ISubscriberParser
{
    public async Task<IReadOnlyCollection<Subscriber>> ParseAsync(Stream stream)
    {
        var result = new List<Subscriber>(32);

        using var reader = new StreamReader(stream);


        await reader.ReadLineAsync();

        string? line;

        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var p = line.Split(';');

            if (p.Length != 2)
                throw new FormatException($"Invalid subscriber line: {line}");

            result.Add(new Subscriber
            {
                PhoneNumber = p[0].Trim(),
                ClientName = p[1].Trim()
            });
        }

        return result;
    }
}