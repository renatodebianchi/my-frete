using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.Modules.Notifications.Domain;
using MyFrete.Modules.Notifications.Sending;

namespace MyFrete.Modules.Notifications;

/// <summary>
/// Consumes every dispatched integration event and turns the relevant ones into push
/// notifications. Concrete per-event templates land with the user-story tasks (T074, T091);
/// this skeleton wires the delivery path and the dedupe ledger.
/// </summary>
public sealed class NotificationsConsumer(
    DbContext db,
    INotificationSender sender,
    TimeProvider clock,
    ILogger<NotificationsConsumer> logger)
    : INotificationHandler<IntegrationEventReceived>
{
    public async Task Handle(IntegrationEventReceived notification, CancellationToken ct)
    {
        var envelope = notification.Envelope;
        var template = NotificationTemplates.For(envelope);
        if (template is null)
        {
            return;
        }

        var dedupeKey = $"{envelope.Type}:{template.RecipientUserId}:{envelope.AggregateId}";
        if (await db.Set<NotificationDispatch>().AnyAsync(d => d.DedupeKey == dedupeKey, ct))
        {
            return;
        }

        var tokens = await db.Set<DeviceToken>()
            .Where(t => t.UserId == template.RecipientUserId)
            .Select(t => t.Token)
            .ToListAsync(ct);

        await sender.SendAsync(tokens, template.Message, ct);

        db.Set<NotificationDispatch>().Add(new NotificationDispatch
        {
            DedupeKey = dedupeKey,
            SentAt = clock.GetUtcNow(),
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Notification dispatched for {Type} to {Recipient}", envelope.Type, template.RecipientUserId);
    }
}

internal sealed record ResolvedTemplate(Guid RecipientUserId, PushMessage Message);

internal static class NotificationTemplates
{
    /// <summary>Returns null for event types that do not notify anyone (skeleton — extended by T074/T091).</summary>
    public static ResolvedTemplate? For(EventEnvelope envelope) => null;
}
