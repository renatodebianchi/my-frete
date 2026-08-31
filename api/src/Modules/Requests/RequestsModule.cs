using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace MyFrete.Modules.Requests;

/// <summary>
/// Transport-request endpoints. The creation/pricing/matching flow is implemented in US1
/// (T051–T066); this module currently exists to enforce FR-003 — only authenticated clients
/// may reach request creation.
/// </summary>
public static class RequestsModule
{
    public static IEndpointRouteBuilder MapRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/requests").RequireAuthorization("client");

        group.MapPost("/", () => Results.StatusCode(StatusCodes.Status501NotImplemented))
            .WithSummary("Create a transport request (implemented in US1).");

        group.MapGet("/", () => Results.StatusCode(StatusCodes.Status501NotImplemented));

        return app;
    }
}
