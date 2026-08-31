using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Trips.Contracts;
using MyFrete.Modules.Trips.Domain;

namespace MyFrete.Modules.Trips.Jobs;

/// <summary>
/// FR-025d: if the client neither confirms nor disputes within the verification window after a
/// delivery mark, notify both parties to check whether the transport was completed.
/// </summary>
public sealed class DeliveryVerificationJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<DeliveryVerificationJob> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Delivery-verification sweep failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IAppConfiguration>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var hours = await config.GetIntAsync(ConfigKeys.DeliveryVerificationHours, 24, ct);
        var cutoff = clock.GetUtcNow() - TimeSpan.FromHours(hours);

        var due = await db.Set<Trip>()
            .Where(t => t.Status == TripStatus.Entregue
                && t.ClientResponse == null
                && t.VerificationNotifiedAt == null
                && t.DeliveredAt <= cutoff)
            .Take(100)
            .ToListAsync(ct);

        foreach (var trip in due)
        {
            trip.VerificationNotifiedAt = clock.GetUtcNow();
            outbox.Enqueue(new TripVerificationDue(trip.Id, trip.ClientId, trip.ProfessionalId));
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Sent delivery-verification notice for {Count} trip(s)", due.Count);
        }
    }
}
