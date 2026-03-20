using Billing.Domain.Models;

namespace Billing.Application.Parsers;

public interface ICallParser
{
    IAsyncEnumerable<Call> ParseStreamAsync(Stream stream, Guid batchId, CancellationToken ct = default);
}

public interface ITariffParser
{
    Task<IReadOnlyList<Tariff>> ParseAsync(Stream stream, Guid batchId, CancellationToken ct = default);
}

public interface ISubscriberParser
{
    Task<IReadOnlyList<Subscriber>> ParseAsync(Stream stream, Guid batchId, CancellationToken ct = default);
}
