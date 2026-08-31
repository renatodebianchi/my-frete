using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Messaging;

namespace MyFrete.BuildingBlocks.Outbox;

/// <summary>
/// Polls the outbox and publishes each pending envelope in-process via MediatR. Idempotent and
/// stateless — safe to run on multiple replicas (rows are claimed with a row lock).
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatch batch failed");
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

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var now = clock.GetUtcNow();

        var due = await db.Set<OutboxMessage>()
            .Where(m => m.State == OutboxState.Pending
                && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in due)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.Payload, MyFreteJson.Options)
                    ?? throw new InvalidOperationException("Empty outbox payload.");

                await mediator.Publish(new IntegrationEventReceived(envelope), ct);

                message.State = OutboxState.Sent;
                message.SentAt = clock.GetUtcNow();
                message.LastError = null;
                message.NextAttemptAt = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message;

                if (message.Attempts >= MaxAttempts)
                {
                    message.State = OutboxState.Failed;
                    message.NextAttemptAt = null;
                    logger.LogError(ex, "Outbox message {Id} ({Type}) failed permanently after {Attempts} attempts",
                        message.Id, message.Type, message.Attempts);
                }
                else
                {
                    var backoff = TimeSpan.FromSeconds(Math.Pow(2, message.Attempts))
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                    message.NextAttemptAt = clock.GetUtcNow() + backoff;
                }
            }
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
