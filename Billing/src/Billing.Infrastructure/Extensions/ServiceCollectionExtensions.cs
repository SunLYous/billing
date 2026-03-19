using Billing.Domain.Interfaces;
using Billing.Infrastructure.Data;
using Billing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Billing.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Billing")
            ?? throw new InvalidOperationException("ConnectionStrings:Billing is not configured");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();

        services.AddSingleton(dataSourceBuilder.Build());

        services.AddDbContext<BillingDbContext>((sp, options) =>
        {
            var ds = sp.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(ds, npgsql =>
            {
                npgsql.CommandTimeout(120);
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            })
            .UseSnakeCaseNamingConvention();

            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddScoped<IBulkRepository, BulkRepository>();
        services.AddScoped<IBatchRepository, BatchRepository>();
        services.AddScoped<IResultRepository, ResultRepository>();

        return services;
    }
}
