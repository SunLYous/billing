using Billing.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Billing.Infrastructure.Data;

public sealed class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<BillingBatch> Batches => Set<BillingBatch>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<RatedCall> RatedCalls => Set<RatedCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BillingBatch>(e =>
        {
            e.ToTable("billing_batches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);
            e.Ignore(x => x.Elapsed);
        });

        modelBuilder.Entity<Call>(e =>
        {
            e.ToTable("calls");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.CallingParty).HasMaxLength(30).IsRequired();
            e.Property(x => x.CalledParty).HasMaxLength(30).IsRequired();
            e.Property(x => x.CallDirection).HasMaxLength(20).IsRequired();
            e.Property(x => x.Disposition).HasMaxLength(20).IsRequired();
            e.Property(x => x.PbxCharge).HasPrecision(12, 4);
            e.Property(x => x.AccountCode).HasMaxLength(100);
            e.Property(x => x.CallId).HasMaxLength(100).IsRequired();
            e.Property(x => x.TrunkName).HasMaxLength(100);

            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => new { x.BatchId, x.CallId }).IsUnique();
            e.HasIndex(x => x.CallingParty);
            e.Ignore(x => x.IsBillable);
        });

        modelBuilder.Entity<Tariff>(e =>
        {
            e.ToTable("tariffs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
            e.Property(x => x.Destination).HasMaxLength(200).IsRequired();
            e.Property(x => x.RatePerMinute).HasPrecision(12, 4);
            e.Property(x => x.ConnectionFee).HasPrecision(12, 4);
            e.Property(x => x.Timeband).HasMaxLength(20);
            e.Property(x => x.Weekday).HasMaxLength(20);
            e.Property(x => x.EffectiveDate);
            e.Property(x => x.ExpiryDate);

            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => new { x.BatchId, x.Prefix });
        });

        modelBuilder.Entity<Subscriber>(e =>
        {
            e.ToTable("subscribers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            e.Property(x => x.ClientName).HasMaxLength(200).IsRequired();

            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => new { x.BatchId, x.PhoneNumber });
        });

        modelBuilder.Entity<RatedCall>(e =>
        {
            e.ToTable("rated_calls");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).UseIdentityAlwaysColumn();
            e.Property(x => x.ConnectionFee).HasPrecision(12, 4);
            e.Property(x => x.MinutesCost).HasPrecision(14, 4);
            e.Property(x => x.CalculatedCost).HasPrecision(14, 4);

            e.HasOne(x => x.Call).WithMany().HasForeignKey(x => x.CallId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AppliedTariff).WithMany().HasForeignKey(x => x.TariffId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.BatchId);
            e.HasIndex(x => new { x.BatchId, x.CallId });
        });
    }
}