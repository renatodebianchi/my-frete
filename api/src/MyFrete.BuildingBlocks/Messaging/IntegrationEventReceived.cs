using MediatR;

namespace MyFrete.BuildingBlocks.Messaging;

/// <summary>
/// Published in-process by the outbox dispatcher for every dispatched envelope. Module consumers
/// implement <see cref="INotificationHandler{T}"/> and filter on <see cref="EventEnvelope.Type"/>.
/// When a real bus is introduced this is the only edge that changes.
/// </summary>
public sealed record IntegrationEventReceived(EventEnvelope Envelope) : INotification;
