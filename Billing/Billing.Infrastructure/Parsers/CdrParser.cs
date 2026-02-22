using Billing.Application.Parsers.CallParser;
using Billing.Domain.Models;

namespace Billing.Infrastructure.Parsers;

public sealed class CdrParser : ICallParser
{
    public async Task<IReadOnlyCollection<Call>> ParseAsync(Stream stream)
    {
        var result = new List<Call>();

        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var p = line.Split('|');

            if (p.Length < 12)
                throw new InvalidDataException("Invalid CDR format.");

            result.Add(new Call
            {
                StartTime = DateTime.Parse(p[0]),
                EndTime = DateTime.Parse(p[1]),
                CallingParty = p[2],
                CalledParty = p[3],
                Direction = ParseDirection(p[4]),
                Disposition = ParseDisposition(p[5]),
                Duration = int.Parse(p[6]),
                BillableSeconds = int.Parse(p[7]),
                CallId = p[10],
                TrunkName = p[11]
            });
        }

        return result;
    }

    private static CallDirection ParseDirection(string value) =>
        value.ToLowerInvariant() switch
        {
            "incoming" => CallDirection.Incoming,
            "outgoing" => CallDirection.Outgoing,
            "internal" => CallDirection.Internal,
            _ => throw new InvalidDataException($"Unknown direction: {value}")
        };

    private static CallDisposition ParseDisposition(string value) =>
        value.ToLowerInvariant() switch
        {
            "answered" => CallDisposition.Answered,
            "busy" => CallDisposition.Busy,
            "no_answer" => CallDisposition.NoAnswer,
            "failed" => CallDisposition.Failed,
            _ => throw new InvalidDataException($"Unknown disposition: {value}")
        };
}