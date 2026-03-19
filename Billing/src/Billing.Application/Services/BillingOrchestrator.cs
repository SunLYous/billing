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
        Stream cdrStream,
        Stream tariffStream,
        Stream subscriberStream,
        Guid? existingBatchId = null,
        IProgress<int>? progress = null,
        IProgress<(string Stage, int Percent, string Message)>? stageProgress = null,
        CancellationToken ct = default)
    {
        Guid batchId;
        if (existingBatchId.HasValue)
        {
            batchId = existingBatchId.Value;
        }
        else
        {
            var batch = await _batchRepo.CreateAsync(ct);
            batchId = batch.Id;
        }

        try
        {
            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Parsing, ct: ct);
            stageProgress?.Report(("parsing", 10, "Парсинг файлов..."));
            _logger.LogInformation("Batch {BatchId}: начало парсинга", batchId);

            var callsTask = _callParser.ParseAsync(cdrStream, batchId, ct);
            var tariffsTask = _tariffParser.ParseAsync(tariffStream, batchId, ct);
            var subscribersTask = _subscriberParser.ParseAsync(subscriberStream, batchId, ct);

            await Task.WhenAll(callsTask, tariffsTask, subscribersTask);

            var calls = callsTask.Result;
            var tariffs = tariffsTask.Result;
            var subscribers = subscribersTask.Result;

            _logger.LogInformation(
                "Batch {BatchId}: распарсено {Calls} звонков, {Tariffs} тарифов, {Subs} абонентов",
                batchId, calls.Count, tariffs.Count, subscribers.Count);

            stageProgress?.Report(("saving", 25, $"Сохранение {calls.Count:N0} записей в БД..."));

            await Task.WhenAll(
                _bulkRepo.BulkInsertCallsAsync(calls, ct),
                _bulkRepo.BulkInsertTariffsAsync(tariffs, ct),
                _bulkRepo.BulkInsertSubscribersAsync(subscribers, ct));

            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Rating, totalCalls: calls.Count, ct: ct);
            stageProgress?.Report(("rating", 40, "Тарификация..."));
            _logger.LogInformation("Batch {BatchId}: начало тарификации", batchId);

            var rated = _billingService.Rate(calls, tariffs, batchId, progress);

            _logger.LogInformation("Batch {BatchId}: тарифицировано {Count} звонков", batchId, rated.Count);

            stageProgress?.Report(("saving_results", 90, "Сохранение результатов..."));
            await _bulkRepo.BulkInsertRatedCallsAsync(rated, ct);

            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Completed,
                processedCalls: rated.Count, ct: ct);

            stageProgress?.Report(("done", 100, $"Готово! Тарифицировано {rated.Count:N0} звонков."));

            return batchId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch {BatchId}: ошибка обработки", batchId);
            await _batchRepo.UpdateStatusAsync(batchId, BatchStatus.Failed,
                error: ex.Message, ct: ct);
            throw;
        }
    }
}
