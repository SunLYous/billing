using Billing.Domain.Models;

namespace Billing.Domain.Services;

public interface IBillingService
{
    IReadOnlyCollection<RatedCall> Rate(
        IEnumerable<Call> calls,
        IEnumerable<Tariff> tariffs);
}