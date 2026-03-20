using System.Threading.Channels;
using Billing.Application.Services;
using Billing.Web.Services;

namespace Billing.Web.BackgroundServices;

public sealed class BillingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BillingJobQueue _queue;
    private readonly ILogger<BillingBackgroundService> _logger;

    public BillingBackgroundService(
        IServiceScopeFactory scopeFactory,
        BillingJobQueue queue,
        ILogger<BillingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BillingBackgroundService запущен");

        await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки batch {BatchId}", job.BatchId);
            }
        }
    }

    private async Task ProcessJobAsync(BillingJob job, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<BillingOrchestrator>();
        var notifier = scope.ServiceProvider.GetRequiredService<BillingProgressNotifier>();

        var batchId = job.BatchId;

        try
        {
            await notifier.SendProgressAsync(batchId, "upload", 5, "Файлы загружены, начинаем обработку...");

            var ratingProgress = new Progress<int>(async p =>
            {
                var overall = 40 + (int)(p * 0.5);
                await notifier.SendProgressAsync(batchId, "rating", overall, $"Тарификация: {p}%");
            });

            var stageProgress = new Progress<(string Stage, int Percent, string Message)>(async update =>
            {
                await notifier.SendProgressAsync(batchId, update.Stage, update.Percent, update.Message);
            });

            await using var cdrStream = File.OpenRead(job.CdrFilePath);
            await using var tariffStream = File.OpenRead(job.TariffFilePath);
            await using var subscriberStream = File.OpenRead(job.SubscriberFilePath);

            await orchestrator.ProcessAsync(
                cdrStream, tariffStream, subscriberStream, batchId, ratingProgress, stageProgress, ct);

            await notifier.SendCompletedAsync(batchId, 0);

            _logger.LogInformation("Batch {BatchId}: обработка завершена", batchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch {BatchId}: ошибка", batchId);
            await notifier.SendErrorAsync(batchId, ex.Message);
        }
        finally
        {
            TryDelete(job.CdrFilePath);
            TryDelete(job.TariffFilePath);
            TryDelete(job.SubscriberFilePath);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* ignore */ }
    }
}

public sealed record BillingJob(
    Guid BatchId,
    string CdrFilePath,
    string TariffFilePath,
    string SubscriberFilePath);

public sealed class BillingJobQueue
{
    private readonly Channel<BillingJob> _channel =
        Channel.CreateBounded<BillingJob>(new BoundedChannelOptions(20)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public async ValueTask EnqueueAsync(BillingJob job, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(job, ct);
    }

    public IAsyncEnumerable<BillingJob> DequeueAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
