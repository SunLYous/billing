using System.Threading.RateLimiting;
using Billing.Application.Parsers;
using Billing.Application.Services;
using Billing.Domain.Services;
using Billing.Infrastructure.Data;
using Billing.Infrastructure.Extensions;
using Billing.Web.BackgroundServices;
using Billing.Web.Hubs;
using Billing.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// === Kestrel: увеличенные лимиты для файлов ===
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 250 * 1024 * 1024; // 250 MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5);
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 250 * 1024 * 1024;
});

// === Infrastructure (PostgreSQL + Repositories) ===
builder.Services.AddInfrastructure(builder.Configuration);

// === Domain Services ===
builder.Services.AddSingleton<BillingService>();

// === Application Services ===
builder.Services.AddScoped<ICallParser, CallParser>();
builder.Services.AddScoped<ITariffParser, TariffParser>();
builder.Services.AddScoped<ISubscriberParser, SubscriberParser>();
builder.Services.AddScoped<BillingOrchestrator>();

builder.Services.AddSignalR();
builder.Services.AddScoped<BillingProgressNotifier>();

builder.Services.AddSingleton<BillingJobQueue>();
builder.Services.AddHostedService<BillingBackgroundService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("upload", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Слишком много запросов. Попробуйте позже.");
    };
});

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Billing")!,
        name: "postgresql",
        tags: new[] { "db", "ready" });

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddRazorPages();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await db.Database.MigrateAsync();
}

app.UseResponseCompression();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapHub<BillingProgressHub>("/hubs/billing-progress");
app.MapRazorPages();

var api = app.MapGroup("/api/billing").DisableAntiforgery();

api.MapPost("/", async (
    IFormFile cdrFile,
    IFormFile tariffFile,
    IFormFile subscriberFile,
    Billing.Domain.Interfaces.IBatchRepository batchRepo,
    BillingJobQueue queue,
    CancellationToken ct) =>
{
    var batch = await batchRepo.CreateAsync(ct);

    var tempDir = Path.Combine(Path.GetTempPath(), "billing", batch.Id.ToString());
    Directory.CreateDirectory(tempDir);

    var cdrPath = Path.Combine(tempDir, "cdr.txt");
    var tariffPath = Path.Combine(tempDir, "tariffs.csv");
    var subPath = Path.Combine(tempDir, "subscribers.csv");

    await using (var fs = new FileStream(cdrPath, FileMode.Create))
        await cdrFile.CopyToAsync(fs, ct);
    await using (var fs = new FileStream(tariffPath, FileMode.Create))
        await tariffFile.CopyToAsync(fs, ct);
    await using (var fs = new FileStream(subPath, FileMode.Create))
        await subscriberFile.CopyToAsync(fs, ct);

    await queue.EnqueueAsync(new BillingJob(batch.Id, cdrPath, tariffPath, subPath), ct);

    return Results.Accepted($"/api/billing/{batch.Id}/status", new { batchId = batch.Id });
});

api.MapPost("/sync", async (
    IFormFile cdrFile,
    IFormFile tariffFile,
    IFormFile subscriberFile,
    BillingOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    await using var cdrStream = cdrFile.OpenReadStream();
    await using var tariffStream = tariffFile.OpenReadStream();
    await using var subscriberStream = subscriberFile.OpenReadStream();

    var batchId = await orchestrator.ProcessAsync(
        cdrStream, tariffStream, subscriberStream, ct: ct);

    sw.Stop();

    return Results.Ok(new
    {
        batchId,
        elapsedMs = sw.ElapsedMilliseconds,
        elapsedSec = Math.Round(sw.Elapsed.TotalSeconds, 3)
    });
});

api.MapGet("/{batchId:guid}/status", async (
    Guid batchId,
    Billing.Domain.Interfaces.IBatchRepository batchRepo,
    Billing.Domain.Interfaces.IResultRepository resultRepo,
    CancellationToken ct) =>
{
    var batch = await batchRepo.GetByIdAsync(batchId, ct);
    if (batch is null)
        return Results.NotFound(new { error = "Batch not found" });

    var result = new
    {
        batchId = batch.Id,
        status = batch.Status.ToString(),
        totalCalls = batch.TotalCalls,
        processedCalls = batch.ProcessedCalls,
        error = batch.ErrorMessage,
        resultUrl = batch.Status == Billing.Domain.Models.BatchStatus.Completed
            ? $"/Result/{batch.Id}" : (string?)null
    };

    return Results.Ok(result);
});

app.Run();
