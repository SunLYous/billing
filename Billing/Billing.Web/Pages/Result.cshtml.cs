using Billing.Domain.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Billing.Web.Pages;

public class ResultModel(BillingResultStore store) : PageModel
{
    public List<(string Name, decimal Total)> Totals { get; private set; } = [];
    public IReadOnlyCollection<RatedCall> Calls { get; private set; } = [];

    public void OnGet()
    {
        var result = store.Result ?? [];
        var subscribers = store.Subscribers ?? [];

        Calls = result;

        Totals = result
            .GroupBy(r => r.Call.CallingParty)
            .Select(g =>
            {
                var subscriber = subscribers
                    .FirstOrDefault(s => s.PhoneNumber == g.Key);

                var name = subscriber?.ClientName ?? g.Key;

                return (name, g.Sum(x => x.CalculatedCost));
            })
            .ToList();
    }
}