using Billing.Domain;
using Billing.Domain.Models;

namespace Billing.Application.Parsers.SubscriberParser;

public interface ISubscriberParser
{
    Task<IReadOnlyCollection<Subscriber>> ParseAsync(Stream stream);
}