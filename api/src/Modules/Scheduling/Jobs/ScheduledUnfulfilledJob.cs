using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Scheduling.Contracts;
using MyFrete.Modules.Scheduling.Domain;

namespace MyFrete.Modules.Scheduling.Jobs;

/// <summary>
/// FR-024: a scheduled request that reaches its date without any professional accepting is
/// closed as unfulfilled and the client is notified.
/// </summary>
public sealed class ScheduledUnfulfilledJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ScheduledUnfulfilledJob> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

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
                logger.LogError(ex, "Scheduled-unfulfilled sweep failed");
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
        var requests = scope.ServiceProvider.GetRequiredService<IRequestAssignment>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var stale = await db.Set<ScheduledOffer>()
            .Where(o => o.Outcome == ScheduledOfferOutcome.Pending && o.ScheduledDate < today)
            .GroupBy(o => new { o.RequestId, o.ClientId })
            .Select(g => new { g.Key.RequestId, g.Key.ClientId })
            .Take(100)
            .ToListAsync(ct);

        foreach (var group in stale)
        {
            await db.Set<ScheduledOffer>()
                .Where(o => o.RequestId == group.RequestId && o.Outcome == ScheduledOfferOutcome.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Outcome, ScheduledOfferOutcome.Unfulfilled), ct);

            await requests.MarkUnfulfilledScheduledAsync(group.RequestId, ct);
            outbox.Enqueue(new SchedulingUnfulfilled(group.RequestId, group.ClientId, "no_acceptance_by_date"));
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Closed {Count} scheduled request(s) as unfulfilled", stale.Count);
        }
    }
}
