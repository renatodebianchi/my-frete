using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Pricing.Routing;

namespace MyFrete.Modules.Pricing;

public static class PricingModule
{
    public static IServiceCollection AddPricingModule(this IServiceCollection services)
    {
        // No external routing provider configured yet -> the resilient provider uses the
        // geodesic fallback. A real client will be registered with AddHttpClient here.
        services.TryAddScoped<IExternalRouteClient, NullExternalRouteClient>();
        services.TryAddScoped<IRouteDistanceProvider, ResilientRouteDistanceProvider>();
        services.TryAddScoped<IPricingService, PricingService>();
        return services;
    }

    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/pricing/estimate", async (EstimateBody body, ISender sender) =>
        {
            var result = await sender.Send(new EstimateQuery(
                body.Origin.Point.Lat, body.Origin.Point.Lng,
                body.Destination.Point.Lat, body.Destination.Point.Lng,
                (int)Math.Round(body.EstimatedWeightKg * 1000m)));

            if (result.IsFailure)
            {
                return Results.Problem(detail: result.Error.Message, statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
            }

            var e = result.Value;
            return Results.Ok(new
            {
                amount = e.Amount,
                currency = e.Currency,
                distanceKm = e.DistanceKm,
                distanceSource = e.DistanceSource,
                isEstimate = true,
            });
        }).RequireAuthorization("client");

        return app;
    }
}

public sealed record PointBody(double Lat, double Lng);

public sealed record AddressBody(string? Text, PointBody Point);

public sealed record EstimateBody(AddressBody Origin, AddressBody Destination, decimal EstimatedWeightKg);

public sealed record EstimateQuery(
    double OriginLat,
    double OriginLng,
    double DestLat,
    double DestLng,
    int WeightGrams) : IQuery<Result<PriceEstimate>>;

public sealed class EstimateValidator : AbstractValidator<EstimateQuery>
{
    public EstimateValidator()
    {
        RuleFor(x => x.OriginLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.OriginLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.DestLat).InclusiveBetween(-90, 90);
        RuleFor(x => x.DestLng).InclusiveBetween(-180, 180);
        RuleFor(x => x.WeightGrams).GreaterThan(0);
        RuleFor(x => x)
            .Must(x => Math.Abs(x.OriginLat - x.DestLat) > 1e-6 || Math.Abs(x.OriginLng - x.DestLng) > 1e-6)
            .WithMessage("Origin and destination must be different (FR-007).");
    }
}

public sealed class EstimateHandler(IPricingService pricing)
    : IRequestHandler<EstimateQuery, Result<PriceEstimate>>
{
    public Task<Result<PriceEstimate>> Handle(EstimateQuery q, CancellationToken ct) =>
        pricing.EstimateAsync(
            new GeoPoint(q.OriginLat, q.OriginLng),
            new GeoPoint(q.DestLat, q.DestLng),
            q.WeightGrams,
            ct);
}
