using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Configuration;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.Modules.Accounts.Professionals;
using MyFrete.Modules.Scheduling.Contracts;
using MyFrete.Modules.Scheduling.Domain;

namespace MyFrete.Modules.Scheduling;

/// <summary>
/// On request.schedule_requested.v1, notify every professional available on that date whose
/// capacity fits and who has not reached the per-date limit (FR-021, FR-022a). Runs in the
/// outbox dispatcher's scope; the dispatcher persists the changes.
/// </summary>
public sealed class SchedulingBroadcaster(
    DbContext db,
    IProfessionalDirectory directory,
    IAppConfiguration config,
    IOutboxWriter outbox,
    TimeProvider clock,
    ILogger<SchedulingBroadcaster> logger) : INotificationHandler<IntegrationEventReceived>
{
    public async Task Handle(IntegrationEventReceived notification, CancellationToken ct)
    {
        var e = notification.Envelope;
        if (e.Type != "request.schedule_requested.v1")
        {
            return;
        }

        var requestId = e.AggregateId;
        if (await db.Set<ScheduledOffer>().AnyAsync(o => o.RequestId == requestId, ct))
        {
            return;
        }

        var clientId = e.Data.GetProperty("clientId").GetGuid();
        var date = DateOnly.Parse(
            e.Data.GetProperty("scheduledDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var weightGrams = e.Data.GetProperty("weightGrams").GetInt32();
        var estimatedPrice = e.Data.GetProperty("estimatedPrice").GetDecimal();
        var currency = e.Data.GetProperty("currency").GetString() ?? "BRL";
        var maxPerDate = await config.GetIntAsync(ConfigKeys.MaxSchedulesPerDate, 1, ct);
        var now = clock.GetUtcNow();

        var available = await db.Set<ProfessionalScheduleAvailability>()
            .Where(a => a.AvailableDate == date)
            .Select(a => a.ProfessionalId)
            .ToListAsync(ct);

        if (available.Count == 0)
        {
            outbox.Enqueue(new SchedulingBroadcastSent(requestId, date, 0));
            logger.LogInformation("No professionals available on {Date} for request {RequestId}", date, requestId);
            return;
        }

        var withCapacity = await directory.WithCapacityAsync(available, weightGrams, ct);
        var loadByPro = await db.Set<ProfessionalDailyLoad>()
            .Where(l => l.LoadDate == date && available.Contains(l.ProfessionalId))
            .ToDictionaryAsync(l => l.ProfessionalId, l => l.AcceptedCount, ct);

        var recipients = available
            .Where(id => withCapacity.Contains(id) && loadByPro.GetValueOrDefault(id) < maxPerDate)
            .ToList();

        foreach (var professionalId in recipients)
        {
            var scheduledOffer = new ScheduledOffer
            {
                RequestId = requestId,
                ClientId = clientId,
                ProfessionalId = professionalId,
                ScheduledDate = date,
                WeightGrams = weightGrams,
                EstimatedPrice = estimatedPrice,
                Currency = currency,
                SentAt = now,
            };
            db.Set<ScheduledOffer>().Add(scheduledOffer);
            outbox.Enqueue(new ScheduledOfferSent(scheduledOffer.Id, requestId, professionalId, date));
        }

        outbox.Enqueue(new SchedulingBroadcastSent(requestId, date, recipients.Count));
        logger.LogInformation("Scheduling broadcast: {Count} professional(s) for request {RequestId}",
            recipients.Count, requestId);
    }
}
