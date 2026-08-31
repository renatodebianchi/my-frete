using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyFrete.BuildingBlocks.Contracts;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Trips.Contracts;
using MyFrete.Modules.Trips.Features;
using MyFrete.Modules.Trips.Jobs;

namespace MyFrete.Modules.Trips;

public static class TripsModule
{
    public static IServiceCollection AddTripsModule(this IServiceCollection services)
    {
        services.TryAddScoped<ITripFactory, TripFactory>();
        // Real availability guard — replaces the Accounts no-op default (FR-011a).
        services.Replace(ServiceDescriptor.Scoped<IActiveTripGuard, TripActiveTripGuard>());
        services.AddHostedService<DeliveryVerificationJob>();
        return services;
    }

    public static IEndpointRouteBuilder MapTripsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/trips").RequireAuthorization();

        group.MapGet("/", async (int? page, ISender sender) =>
            (await sender.Send(new ListTripsQuery(page ?? 1))).ToResult(v => Results.Ok(new { items = v })));

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
            (await sender.Send(new GetTripQuery(id))).ToResult(Results.Ok));

        group.MapPatch("/{id:guid}/agreed-amount", async (Guid id, AmountBody body, ISender sender) =>
            (await sender.Send(new SetAgreedAmountCommand(id, body.Amount))).ToResult(Results.Ok));

        group.MapPost("/{id:guid}/deliver", async (Guid id, ISender sender) =>
            (await sender.Send(new DeliverTripCommand(id))).ToResult(Results.Ok));

        group.MapPost("/{id:guid}/client-response", async (Guid id, ClientResponseBody body, ISender sender) =>
            (await sender.Send(new ClientRespondCommand(id, body.Response, body.Note))).ToResult(Results.Ok));

        group.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender) =>
            (await sender.Send(new CancelTripCommand(id))).ToResult(Results.Ok));

        return app;
    }

    private static IResult ToResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    private static IResult ToProblem(this Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return Results.Problem(detail: error.Message, statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}

public sealed record AmountBody(decimal Amount);

public sealed record ClientResponseBody(string Response, string? Note);
