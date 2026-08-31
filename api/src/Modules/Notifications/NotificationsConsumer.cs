using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFrete.BuildingBlocks.Messaging;
using MyFrete.Modules.Notifications.Domain;
using MyFrete.Modules.Notifications.Sending;

namespace MyFrete.Modules.Notifications;

/// <summary>
/// Turns dispatched integration events into push notifications (T074). Idempotent via the
/// notification_dispatch ledger — a re-delivered event never double-sends.
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
        var templates = NotificationTemplates.For(envelope);
        if (templates.Count == 0)
        {
            return;
        }

        foreach (var template in templates)
        {
            var dedupeKey = $"{envelope.Type}:{template.RecipientUserId}:{envelope.AggregateId}";
            if (await db.Set<NotificationDispatch>().AnyAsync(d => d.DedupeKey == dedupeKey, ct))
            {
                continue;
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

            logger.LogInformation("Notification {Type} -> {Recipient}", envelope.Type, template.RecipientUserId);
        }

        await db.SaveChangesAsync(ct);
    }
}

internal sealed record ResolvedTemplate(Guid RecipientUserId, PushMessage Message);

internal static class NotificationTemplates
{
    public static IReadOnlyList<ResolvedTemplate> For(EventEnvelope e)
    {
        var d = e.Data;
        var aggregate = e.AggregateId.ToString();

        return e.Type switch
        {
            "matching.offer.sent.v1" =>
            [
                new(Prop(d, "professionalId"), new PushMessage(
                    "Nova oferta de frete",
                    "Você tem 30 segundos para aceitar.",
                    Data("offer_received", "offerId", aggregate))),
            ],

            "matching.offer.accepted.v1" =>
            [
                new(Prop(d, "clientId"), new PushMessage(
                    "Frete contratado",
                    "Um profissional aceitou seu frete.",
                    Data("offer_result", "requestId", Prop(d, "requestId").ToString()))),
            ],

            "matching.exhausted.v1" =>
            [
                new(Prop(d, "clientId"), new PushMessage(
                    "Nenhum profissional disponível",
                    "Ninguém aceitou agora. Deseja agendar para outro dia?",
                    Data("request_status", "requestId", aggregate))),
            ],

            "trip.delivered.v1" =>
            [
                new(Prop(d, "clientId"), new PushMessage(
                    "Entrega registrada",
                    "O profissional marcou o transporte como entregue. Confirme o recebimento.",
                    Data("trip_delivered", "tripId", aggregate))),
            ],

            "trip.verification_due.v1" =>
            [
                Verification(Prop(d, "clientId"), aggregate),
                Verification(Prop(d, "professionalId"), aggregate),
            ],

            _ => [],
        };
    }

    private static ResolvedTemplate Verification(Guid recipient, string tripId) =>
        new(recipient, new PushMessage(
            "Verificação do transporte",
            "O transporte foi concluído? Confirme no app.",
            Data("trip_verification", "tripId", tripId)));

    private static Dictionary<string, string> Data(string type, string idKey, string idValue) =>
        new() { ["type"] = type, [idKey] = idValue };

    private static Guid Prop(JsonElement d, string prop) =>
        d.TryGetProperty(prop, out var v) && v.TryGetGuid(out var g) ? g : Guid.Empty;
}
