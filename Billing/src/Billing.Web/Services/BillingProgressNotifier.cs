using Billing.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Billing.Web.Services;

/// <summary>
/// Отправляет прогресс тарификации клиентам через SignalR.
/// </summary>
public sealed class BillingProgressNotifier
{
    private readonly IHubContext<BillingProgressHub> _hub;

    public BillingProgressNotifier(IHubContext<BillingProgressHub> hub) => _hub = hub;

    public async Task SendProgressAsync(Guid batchId, string stage, int percent, string? message = null)
    {
        await _hub.Clients.Group(batchId.ToString()).SendAsync("ProgressUpdate", new
        {
            batchId = batchId.ToString(),
            stage,
            percent,
            message
        });
    }

    public async Task SendCompletedAsync(Guid batchId, int totalRated)
    {
        await _hub.Clients.Group(batchId.ToString()).SendAsync("Completed", new
        {
            batchId = batchId.ToString(),
            totalRated
        });
    }

    public async Task SendErrorAsync(Guid batchId, string error)
    {
        await _hub.Clients.Group(batchId.ToString()).SendAsync("Error", new
        {
            batchId = batchId.ToString(),
            error
        });
    }
}
