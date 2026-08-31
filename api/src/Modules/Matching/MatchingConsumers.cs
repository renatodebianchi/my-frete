using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.Modules.Matching.Domain;

namespace MyFrete.Modules.Matching;

/// <summary>
/// Starts an immediate-search session when a request is confirmed (T065). Runs inside the
/// outbox dispatcher's scope; the dispatcher persists the changes.
/// </summary>
public sealed class RequestConfirmedConsumer(
    DbContext db,
    IAppConfiguration config,
    TimeProvider clock,
    ILogger<RequestConfirmedConsumer> logger) : INotificationHandler<IntegrationEventReceived>
{
    public async Task Handle(IntegrationEventReceived notification, CancellationToken ct)
    {
        var envelope = notification.Envelope;
        if (envelope.Type != "request.confirmed.v1")
        {
            return;
        }

        var requestId = envelope.AggregateId;
        if (await db.Set<MatchingSession>().AnyAsync(s => s.RequestId == requestId, ct))
        {
            return;
        }

        var data = envelope.Data;
        var now = clock.GetUtcNow();
        var maxDuration = await config.GetSecondsAsync(ConfigKeys.MaxSearchDurationSeconds, 300, ct);

        db.Set<MatchingSession>().Add(new MatchingSession
        {
            RequestId = requestId,
            ClientId = data.GetProperty("clientId").GetGuid(),
            OriginLat = data.GetProperty("originLat").GetDouble(),
            OriginLng = data.GetProperty("originLng").GetDouble(),
            WeightGrams = data.GetProperty("weightGrams").GetInt32(),
            State = SessionState.Searching,
            StartedAt = now,
            DeadlineAt = now + maxDuration,
        });

        logger.LogInformation("Matching session started for request {RequestId}", requestId);
    }
}
