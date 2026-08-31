using FluentAssertions;
using MyFrete.Modules.Pricing.Domain;
using MyFrete.Modules.Requests.Domain;
using Xunit;

namespace MyFrete.Tests.Unit;

public class PricingRuleTests
{
    private static PricingRule Rule() => new()
    {
        BaseFare = 12m,
        PerKm = 2.5m,
        PerKg = 0.15m,
        MinPrice = 20m,
        Currency = "BRL",
        EffectiveFrom = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void Applies_base_plus_distance_plus_weight()
    {
        // 12 + 2.5*10km + 0.15*40kg = 12 + 25 + 6 = 43
        Rule().Compute(distanceMeters: 10_000, weightGrams: 40_000).Should().Be(43.00m);
    }

    [Fact]
    public void Never_goes_below_min_price()
    {
        // 12 + 2.5*1km + 0.15*1kg = 14.65 -> clamped to 20
        Rule().Compute(distanceMeters: 1_000, weightGrams: 1_000).Should().Be(20.00m);
    }
}

public class TransportRequestStateTests
{
    private static TransportRequest New() => new()
    {
        OriginAddress = "a",
        DestinationAddress = "b",
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public void Confirm_moves_draft_to_searching()
    {
        var r = New();
        r.ConfirmImmediate(DateTimeOffset.UnixEpoch);
        r.Status.Should().Be(RequestStatus.Searching);
    }

    [Fact]
    public void Exhaustion_moves_searching_to_awaiting_schedule_decision()
    {
        var r = New();
        r.ConfirmImmediate(DateTimeOffset.UnixEpoch);
        r.MarkExhausted(DateTimeOffset.UnixEpoch);
        r.Status.Should().Be(RequestStatus.AwaitingScheduleDecision);
        r.Status.ToWire().Should().Be("awaiting_schedule_decision");
    }

    [Fact]
    public void Assign_from_searching_becomes_hired()
    {
        var r = New();
        r.ConfirmImmediate(DateTimeOffset.UnixEpoch);
        r.Assign(Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        r.Status.Should().Be(RequestStatus.Hired);
    }

    [Fact]
    public void Completed_request_cannot_be_cancelled()
    {
        var r = New();
        r.ConfirmImmediate(DateTimeOffset.UnixEpoch);
        r.Assign(Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        r.MarkCompleted(DateTimeOffset.UnixEpoch);
        r.TryCancel(DateTimeOffset.UnixEpoch).Should().BeFalse();
    }
}
