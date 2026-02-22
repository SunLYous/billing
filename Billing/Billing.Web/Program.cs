using Billing.Application.Parsers.CallParser;
using Billing.Application.Parsers.SubscriberParser;
using Billing.Application.Parsers.TariffParser;
using Billing.Domain.Services;
using Billing.Infrastructure.Parsers;
using Billing.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddScoped<ICallParser, CdrParser>();
builder.Services.AddScoped<ITariffParser, TariffParser>();
builder.Services.AddScoped<ISubscriberParser, SubscriberParser>();
builder.Services.AddScoped<BillingService>();

builder.Services.AddSingleton<BillingResultStore>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();