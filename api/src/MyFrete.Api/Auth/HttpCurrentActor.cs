using System.Security.Claims;
using MyFrete.Api.Middleware;
using MyFrete.BuildingBlocks.Audit;

namespace MyFrete.Api.Auth;

/// <summary>Resolves the acting user and correlation id from the current HTTP request.</summary>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    private HttpContext? Http => accessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var sub = Http?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Http?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string Actor => UserId is { } id ? $"user:{id}" : "system";

    public string? CorrelationId =>
        Http?.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var v) == true ? v as string : null;
}
