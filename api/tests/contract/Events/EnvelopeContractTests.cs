using System.Text.Json;
using FluentAssertions;
using MyFrete.BuildingBlocks.Messaging;
using Xunit;

namespace MyFrete.Tests.Contract.Events;

// T013a — the envelope shape and versioned `type` are a published contract (contracts/events.md).
public class EnvelopeContractTests
{
    [EventType("test.thing.happened.v1")]
    private sealed record ThingHappened(Guid AggregateId, string Extra) : IIntegrationEvent
    {
        public string AggregateType => "Thing";
    }

    [Fact]
    public void Envelope_has_the_documented_fields()
    {
        var id = Guid.NewGuid();
        var evt = new ThingHappened(id, "payload");

        var envelope = EventEnvelope.From(evt, correlationId: "corr-1", TimeProvider.System);

        envelope.Id.Should().NotBeEmpty();
        envelope.Type.Should().Be("test.thing.happened.v1");
        envelope.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        envelope.CorrelationId.Should().Be("corr-1");
        envelope.AggregateType.Should().Be("Thing");
        envelope.AggregateId.Should().Be(id);
        envelope.Data.GetProperty("extra").GetString().Should().Be("payload");
    }

    [Fact]
    public void Envelope_round_trips_through_json()
    {
        var evt = new ThingHappened(Guid.NewGuid(), "x");
        var envelope = EventEnvelope.From(evt, null, TimeProvider.System);

        var json = JsonSerializer.Serialize(envelope);
        var back = JsonSerializer.Deserialize<EventEnvelope>(json)!;

        back.Type.Should().Be(envelope.Type);
        back.AggregateId.Should().Be(envelope.AggregateId);
    }

    [Fact]
    public void Event_without_EventType_attribute_is_rejected()
    {
        var bad = new NoAttribute(Guid.NewGuid());
        var act = () => EventEnvelope.From(bad, null, TimeProvider.System);
        act.Should().Throw<InvalidOperationException>().WithMessage("*EventType*");
    }

    private sealed record NoAttribute(Guid AggregateId) : IIntegrationEvent
    {
        public string AggregateType => "None";
    }
}
