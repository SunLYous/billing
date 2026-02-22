using Billing.Application.Parsers.CallParser;
using Billing.Application.Parsers.SubscriberParser;
using Billing.Application.Parsers.TariffParser;
using Billing.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Billing.Web.Pages;

public class IndexModel(
    ICallParser callParser,
    ITariffParser tariffParser,
    ISubscriberParser subscriberParser,
    BillingService billingService,
    BillingResultStore store)
    : PageModel
{
    [BindProperty] public IFormFile? CdrFile { get; set; }
    [BindProperty] public IFormFile? TariffFile { get; set; }
    [BindProperty] public IFormFile? SubscriberFile { get; set; }

    public int Progress { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var calls = await callParser.ParseAsync(CdrFile!.OpenReadStream());
        var tariffs = await tariffParser.ParseAsync(TariffFile!.OpenReadStream());
        var subscribers = await subscriberParser.ParseAsync(SubscriberFile!.OpenReadStream());

        var progress = new Progress<int>(p => Progress = p);

        var result = billingService.Rate(calls, tariffs, progress);

        store.Result = result;
        store.Subscribers = subscribers;

        return RedirectToPage("Result");
    }
}