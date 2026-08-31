using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFrete.Modules.Accounts.Domain;

namespace MyFrete.Modules.Accounts.Jobs;

/// <summary>
/// Data minimisation (Constitution §III): the professional's last position is operational only.
/// It is dropped once they stop being available, or after 24h of staleness.
/// </summary>
public sealed class LocationRetentionJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<LocationRetentionJob> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxStaleness = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<DbContext>();
                var cutoff = clock.GetUtcNow() - MaxStaleness;

                var cleared = await db.Set<ProfessionalProfile>()
                    .Where(p => p.LastLocation != null
                        && (!p.ImmediateAvailability || p.LastLocationAt < cutoff))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.LastLocation, (NetTopologySuite.Geometries.Point?)null)
                        .SetProperty(p => p.LastLocationAt, (DateTimeOffset?)null), stoppingToken);

                if (cleared > 0)
                {
                    logger.LogInformation("Cleared stored location for {Count} professional(s)", cleared);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Location retention sweep failed");
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
}
