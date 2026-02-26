using Billing.Domain.Models;

namespace Billing.Web;

public sealed class BillingResultStore
{
    public IReadOnlyCollection<RatedCall>? Result { get; set; }
    public IReadOnlyCollection<Subscriber>? Subscribers { get; set; }
}