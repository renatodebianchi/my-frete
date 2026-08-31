using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Matching.Features;

namespace MyFrete.Modules.Matching;

public static class MatchingModule
{
    public static IServiceCollection AddMatchingModule(this IServiceCollection services)
    {
        services.AddHostedService<OfferOrchestrator>();
        return services;
    }

    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/offers").RequireAuthorization("professional");

        group.MapGet("/inbox", async (ISender sender) =>
            (await sender.Send(new OfferInboxQuery())).ToResult(v => Results.Ok(v)));

        group.MapPost("/{id:guid}/accept", async (Guid id, ISender sender) =>
            (await sender.Send(new AcceptOfferCommand(id)))
                .ToResult(v => Results.Ok(new { tripId = v.TripId, requestId = v.RequestId })));

        group.MapPost("/{id:guid}/decline", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeclineOfferCommand(id));
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
        });

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
