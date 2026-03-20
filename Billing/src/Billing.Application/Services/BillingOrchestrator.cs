using Billing.Application.DTOs;
using Billing.Application.Parsers;
using Billing.Domain.Interfaces;
using Billing.Domain.Models;
using Billing.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Billing.Application.Services;

public sealed class BillingOrchestrator
{
    private readonly ICallParser _callParser;
    private readonly ITariffParser _tariffParser;
    private readonly ISubscriberParser _subscriberParser;
    private readonly BillingService _billingService;
    private readonly IBulkRepository _bulkRepo;
    private readonly IBatchRepository _batchRepo;
    private readonly ILogger<BillingOrchestrator> _logger;

    public BillingOrchestrator(
        ICallParser callParser,
        ITariffParser tariffParser,
        ISubscriberParser subscriberParser,
        BillingService billingService,
        IBulkRepository bulkRepo,
        IBatchRepository batchRepo,
        ILogger<BillingOrchestrator> logger)
    {
        _callParser = callParser;
        _tariffParser = tariffParser;
        _subscriberParser = subscriberParser;
        _billingService = billingService;
        _bulkRepo = bulkRepo;
        _batchRepo = batchRepo;
        _logger = logger;
    }

    public async Task<Guid> ProcessAsync(
        Stream cdrStream, Stream tariffStream, Stream subscriberStream,
        Guid? existingBatchId = null,
        IProgress<int>? progress = null,
        IProgress<(string, int, string)>? stageProgress = null,
        CancellationToken ct = default)
    {
        const int ChunkSize = 50_000;

        Guid batchId = existingBatchId
                       ?? (await _batchRepo.CreateAsync(ct)).Id;

        try
        {
            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Parsing, ct: ct);
            stageProgress?.Report(("parsing", 5, "Парсинг тарифов и абонентов..."));

            var tariffs = await _tariffParser.ParseAsync(tariffStream, batchId, ct);
            var subscribers = await _subscriberParser.ParseAsync(subscriberStream, batchId, ct);

            await _bulkRepo.BulkInsertTariffsAsync(tariffs, ct);
            await _bulkRepo.BulkInsertSubscribersAsync(subscribers, ct);

            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Rating, ct: ct);
            stageProgress?.Report(("rating", 10, "Обработка CDR..."));

            var chunk = new List<Call>(ChunkSize);
            var totalProcessed = 0;
            var totalRated = 0;

            await foreach (var call in _callParser.ParseStreamAsync(cdrStream, batchId, ct))
            {
                chunk.Add(call);

                if (chunk.Count >= ChunkSize)
                {
                    var rated = await ProcessChunkAsync(chunk, tariffs, batchId, ct);
                    totalProcessed += chunk.Count;
                    totalRated += rated;

                    stageProgress?.Report(("rating", 10 + (int)(80.0 * totalProcessed / 3_000_000),
                        $"Обработано {totalProcessed:N0} записей, тарифицировано {totalRated:N0}"));

                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                var rated = await ProcessChunkAsync(chunk, tariffs, batchId, ct);
                totalProcessed += chunk.Count;
                totalRated += rated;
            }

            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Completed,
                totalCalls: totalProcessed, processedCalls: totalRated, ct: ct);

            stageProgress?.Report(("done", 100,
                $"Готово! {totalProcessed:N0} записей, {totalRated:N0} тарифицировано"));

            return batchId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch {BatchId}: ошибка", batchId);
            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Failed,
                error: ex.Message, ct: ct);
            throw;
        }
    }

    private async Task<int> ProcessChunkAsync(
        List<Call> calls, IReadOnlyList<Tariff> tariffs,
        Guid batchId, CancellationToken ct)
    {
        await _bulkRepo.BulkInsertCallsAsync(calls, ct);

        var rated = _billingService.Rate(calls, tariffs, batchId);

        if (rated.Count > 0)
            await _bulkRepo.BulkInsertRatedCallsAsync(rated, ct);

        return rated.Count;
    }
}
