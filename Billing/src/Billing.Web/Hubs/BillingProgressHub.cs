using Microsoft.AspNetCore.SignalR;

namespace Billing.Web.Hubs;

/// <summary>
/// SignalR хаб для real-time прогресса тарификации.
/// Клиент подключается и присоединяется к группе по batchId.
/// </summary>
public sealed class BillingProgressHub : Hub
{
    /// <summary>Клиент подписывается на обновления конкретного batch.</summary>
    public async Task JoinBatch(string batchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, batchId);
    }
}
