using Billing.Domain;
using Billing.Domain.Models;

namespace Billing.Application.Parsers.TariffParser;

public interface ITariffParser
{
    Task<IReadOnlyCollection<Tariff>> ParseAsync(Stream stream);
}