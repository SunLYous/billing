using Billing.Domain;
using Billing.Domain.Models;

namespace Billing.Application.Parsers.CallParser;

public interface ICallParser
{
    Task<IReadOnlyCollection<Call>> ParseAsync(Stream stream);
}