using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Scheduling.Features;
using MyFrete.Modules.Scheduling.Jobs;

namespace MyFrete.Modules.Scheduling;

public static class SchedulingModule
{
    public static IServiceCollection AddSchedulingModule(this IServiceCollection services)
    {
        services.AddHostedService<ScheduledUnfulfilledJob>();
        return services;
    }

    public static IEndpointRouteBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder app)
    {
        var pro = app.MapGroup("/v1/professionals/me").RequireAuthorization("professional");

        pro.MapGet("/schedule-availability", async (ISender sender) =>
            (await sender.Send(new GetAvailabilityQuery())).ToResult(Results.Ok));

        pro.MapPut("/schedule-availability", async (DateOnly[] dates, ISender sender) =>
        {
            var result = await sender.Send(new SetAvailabilityCommand(dates));
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
        });

        var offers = app.MapGroup("/v1/schedule-offers").RequireAuthorization("professional");

        offers.MapGet("/inbox", async (ISender sender) =>
            (await sender.Send(new ScheduledInboxQuery())).ToResult(Results.Ok));

        offers.MapPost("/{id:guid}/accept", async (Guid id, ISender sender) =>
            (await sender.Send(new AcceptScheduledOfferCommand(id)))
                .ToResult(v => Results.Ok(new { tripId = v.TripId, requestId = v.RequestId })));

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
