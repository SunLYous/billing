using Billing.Domain.Interfaces;
using Billing.Web.BackgroundServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Billing.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IBatchRepository _batchRepo;
    private readonly BillingJobQueue _queue;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IBatchRepository batchRepo, BillingJobQueue queue, ILogger<IndexModel> logger)
    {
        _batchRepo = batchRepo;
        _queue = queue;
        _logger = logger;
    }

    [BindProperty] public IFormFile? CdrFile { get; set; }
    [BindProperty] public IFormFile? TariffFile { get; set; }
    [BindProperty] public IFormFile? SubscriberFile { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (CdrFile is null || TariffFile is null || SubscriberFile is null)
        {
            ModelState.AddModelError(string.Empty, "Все три файла обязательны.");
            return Page();
        }

        if (CdrFile.Length > 200 * 1024 * 1024)
        {
            ModelState.AddModelError(nameof(CdrFile), "CDR файл слишком большой (макс. 200 МБ).");
            return Page();
        }

        try
        {
            // 1. Создаём batch в БД
            var batch = await _batchRepo.CreateAsync(ct);

            // 2. Сохраняем файлы во временную директорию
            var tempDir = Path.Combine(Path.GetTempPath(), "billing", batch.Id.ToString());
            Directory.CreateDirectory(tempDir);

            var cdrPath = Path.Combine(tempDir, "cdr.txt");
            var tariffPath = Path.Combine(tempDir, "tariffs.csv");
            var subPath = Path.Combine(tempDir, "subscribers.csv");

            await using (var fs = new FileStream(cdrPath, FileMode.Create))
                await CdrFile.CopyToAsync(fs, ct);
            await using (var fs = new FileStream(tariffPath, FileMode.Create))
                await TariffFile.CopyToAsync(fs, ct);
            await using (var fs = new FileStream(subPath, FileMode.Create))
                await SubscriberFile.CopyToAsync(fs, ct);

            // 3. Ставим задачу в очередь — обработка пойдёт в фоне
            await _queue.EnqueueAsync(new BillingJob(batch.Id, cdrPath, tariffPath, subPath), ct);

            _logger.LogInformation("Batch {BatchId}: файлы загружены, задача в очереди", batch.Id);

            // 4. Сразу редиректим на страницу прогресса
            return RedirectToPage("Progress", new { batchId = batch.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки файлов");
            ModelState.AddModelError(string.Empty, $"Ошибка загрузки: {ex.Message}");
            return Page();
        }
    }
}
