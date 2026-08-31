using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Requests.Contracts;
using MyFrete.Modules.Requests.Features;
using MyFrete.Modules.Requests.Jobs;

namespace MyFrete.Modules.Requests;

public static class RequestsModule
{
    public static IServiceCollection AddRequestsModule(this IServiceCollection services)
    {
        services.TryAddScoped<IRequestAssignment, RequestAssignmentService>();
        services.AddHostedService<ScheduleDecisionTimeoutJob>();
        return services;
    }

    public static IEndpointRouteBuilder MapRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/requests").RequireAuthorization("client");

        group.MapPost("/", async (CreateRequestBody body, ISender sender) =>
        {
            var result = await sender.Send(new CreateRequestCommand(
                body.Items.Select(i => new ItemInput(i.Description, i.Quantity ?? 1)).ToList(),
                body.EstimatedWeightKg,
                body.Origin.Text, body.Origin.Point.Lat, body.Origin.Point.Lng,
                body.Destination.Text, body.Destination.Point.Lat, body.Destination.Point.Lng));

            return result.IsSuccess
                ? Results.Created($"/v1/requests/{result.Value}", new { id = result.Value })
                : result.Error.ToProblem();
        });

        group.MapGet("/", async (int? page, ISender sender) =>
        {
            var result = await sender.Send(new ListRequestsQuery(page ?? 1));
            return result.IsSuccess ? Results.Ok(new { items = result.Value }) : result.Error.ToProblem();
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetRequestQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new CancelRequestCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        });

        return app;
    }

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

public sealed record AddressBody(string Text, PointBodyLatLng Point);

public sealed record PointBodyLatLng(double Lat, double Lng);

public sealed record ItemBody(string Description, int? Quantity);

public sealed record CreateRequestBody(
    IReadOnlyList<ItemBody> Items,
    decimal EstimatedWeightKg,
    AddressBody Origin,
    AddressBody Destination);
