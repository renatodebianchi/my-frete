using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Domain;

namespace MyFrete.Modules.Requests.Jobs;

/// <summary>
/// A request left in `awaiting_schedule_decision` past the timeout is closed as `unfulfilled`
/// — this is what SC-007 measures (explicit decision vs. silent abandonment).
/// </summary>
public sealed class ScheduleDecisionTimeoutJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ScheduleDecisionTimeoutJob> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

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
                logger.LogError(ex, "Schedule-decision timeout sweep failed");
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

        var timeout = await config.GetSecondsAsync(ConfigKeys.ScheduleDecisionTimeoutSeconds, 600, ct);
        var cutoff = clock.GetUtcNow() - timeout;

        var stale = await db.Set<TransportRequest>()
            .Where(r => r.Status == RequestStatus.AwaitingScheduleDecision && r.UpdatedAt <= cutoff)
            .Take(100)
            .ToListAsync(ct);

        foreach (var request in stale)
        {
            request.MarkUnfulfilled(clock.GetUtcNow());
            outbox.Enqueue(new RequestStatusChanged(request.Id, "awaiting_schedule_decision", "unfulfilled"));
        }

        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Closed {Count} request(s) as unfulfilled after schedule-decision timeout", stale.Count);
        }
    }
}
