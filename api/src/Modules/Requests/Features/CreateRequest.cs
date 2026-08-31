using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Audit;
using MyFrete.BuildingBlocks.Outbox;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Pricing;
using MyFrete.Modules.Pricing.Routing;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Domain;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace MyFrete.Modules.Requests.Features;

public sealed record ItemInput(string Description, int Quantity);

public sealed record CreateRequestCommand(
    IReadOnlyList<ItemInput> Items,
    decimal EstimatedWeightKg,
    string OriginAddress,
    double OriginLat,
    double OriginLng,
    string DestinationAddress,
    double DestinationLat,
    double DestinationLng) : ICommand<Result<Guid>>;

public sealed class CreateRequestValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
            i.RuleFor(x => x.Quantity).GreaterThan(0);
        });
        RuleFor(x => x.EstimatedWeightKg).GreaterThan(0);
        RuleFor(x => x.OriginAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DestinationAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.OriginLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.OriginLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.DestinationLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.DestinationLng).InclusiveBetween(-180, 180);
        RuleFor(x => x)
            .Must(x => Math.Abs(x.OriginLat - x.DestinationLat) > 1e-6 || Math.Abs(x.OriginLng - x.DestinationLng) > 1e-6)
            .WithMessage("Origin and destination must be different (FR-007).");
    }
}

public sealed class CreateRequestHandler(
    DbContext db,
    ICurrentActor actor,
    IPricingService pricing,
    IOutboxWriter outbox,
    IAuditLog audit,
    TimeProvider clock) : IRequestHandler<CreateRequestCommand, Result<Guid>>
{
    private static readonly GeometryFactory Geo = NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    public async Task<Result<Guid>> Handle(CreateRequestCommand cmd, CancellationToken ct)
    {
        if (actor.UserId is not { } clientId)
        {
            return Error.Unauthorized("requests.not_authenticated", "Not authenticated.");
        }

        var weightGrams = (int)Math.Round(cmd.EstimatedWeightKg * 1000m);

        var estimate = await pricing.EstimateAsync(
            new GeoPoint(cmd.OriginLat, cmd.OriginLng),
            new GeoPoint(cmd.DestinationLat, cmd.DestinationLng),
            weightGrams,
            ct);

        if (estimate.IsFailure)
        {
            return estimate.Error;
        }

        var now = clock.GetUtcNow();
        var request = new TransportRequest
        {
            ClientId = clientId,
            EstimatedWeightGrams = weightGrams,
            OriginAddress = cmd.OriginAddress,
            OriginPoint = Geo.CreatePoint(new Coordinate(cmd.OriginLng, cmd.OriginLat)),
            DestinationAddress = cmd.DestinationAddress,
            DestinationPoint = Geo.CreatePoint(new Coordinate(cmd.DestinationLng, cmd.DestinationLat)),
            DistanceMeters = estimate.Value.DistanceMeters,
            DistanceSource = estimate.Value.DistanceSource,
            EstimatedPrice = estimate.Value.Amount,
            Currency = estimate.Value.Currency,
            PricingRuleId = estimate.Value.PricingRuleId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        request.SetItems(cmd.Items.Select(i => new RequestItem(i.Description, i.Quantity)));
        request.ConfirmImmediate(now);

        db.Set<TransportRequest>().Add(request);

        outbox.Enqueue(new RequestConfirmed(
            request.Id, clientId,
            cmd.OriginLat, cmd.OriginLng,
            cmd.DestinationLat, cmd.DestinationLng,
            weightGrams));
        outbox.Enqueue(new RequestStatusChanged(request.Id, "draft", "searching"));

        await audit.WriteAsync("request.confirmed", "TransportRequest", request.Id,
            new { weightGrams, estimate.Value.Amount }, ct: ct);

        return request.Id;
    }
}
