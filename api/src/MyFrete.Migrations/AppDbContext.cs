using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Idempotency;
using MyFrete.BuildingBlocks.Outbox;

namespace MyFrete.Migrations;

/// <summary>
/// Single EF Core context for the modular monolith. Cross-cutting infra tables are configured
/// here; each module contributes its own <c>IEntityTypeConfiguration</c> from its assembly
/// (wired as modules land in later phases) — never reaching into another module's tables.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<ConfigurationEntry> Configuration => Set<ConfigurationEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var b = modelBuilder;
        b.HasPostgresExtension("postgis");

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(200);
            e.Property(x => x.AggregateType).HasMaxLength(100);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.DedupeKey).HasMaxLength(400);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasIndex(x => x.DedupeKey).IsUnique().HasFilter("dedupe_key IS NOT NULL");
            e.HasIndex(x => new { x.State, x.CreatedAt });
        });

        b.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_event");
            e.HasKey(x => x.Id);
            e.Property(x => x.Actor).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.AggregateType).HasMaxLength(100);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasIndex(x => new { x.AggregateType, x.AggregateId });
        });

        b.Entity<IdempotencyRecord>(e =>
        {
            e.ToTable("idempotency_key");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.RequestHash).HasMaxLength(128);
            e.Property(x => x.ResponseBody).HasColumnType("jsonb");
            e.Property(x => x.ResponseContentType).HasMaxLength(100);
        });

        b.Entity<ConfigurationEntry>(e =>
        {
            e.ToTable("configuration");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(2000);
        });

        base.OnModelCreating(modelBuilder);
    }
}
