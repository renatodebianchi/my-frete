namespace MyFrete.Api.Middleware;

/// <summary>Baseline hardening headers for a JSON API (Constitution §II).</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers.Remove("X-Powered-By");
        headers.Remove("Server");

        await next(context);
    }
}
