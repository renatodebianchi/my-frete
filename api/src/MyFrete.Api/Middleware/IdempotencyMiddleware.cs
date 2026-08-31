using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyFrete.BuildingBlocks.Idempotency;

namespace MyFrete.Api.Middleware;

/// <summary>
/// Honours the <c>Idempotency-Key</c> header on unsafe requests: a replay with the same key and
/// body returns the stored response; a replay with a different body is a 422.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, TimeProvider clock)
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly HashSet<string> Methods = ["POST", "PATCH", "PUT", "DELETE"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!Methods.Contains(context.Request.Method)
            || !context.Request.Headers.TryGetValue(HeaderName, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues))
        {
            await next(context);
            return;
        }

        var key = keyValues.ToString();
        if (key.Length > 128)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { code = "idempotency.key_too_long" });
            return;
        }

        var db = context.RequestServices.GetRequiredService<DbContext>();
        var set = db.Set<IdempotencyRecord>();

        context.Request.EnableBuffering();
        var requestHash = await HashRequestAsync(context.Request);
        context.Request.Body.Position = 0;

        var existing = await set.AsNoTracking().FirstOrDefaultAsync(r => r.Key == key, context.RequestAborted);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                await context.Response.WriteAsJsonAsync(new { code = "idempotency.key_reused_with_different_body" });
                return;
            }

            context.Response.StatusCode = existing.ResponseStatusCode;
            if (existing.ResponseContentType is not null)
            {
                context.Response.ContentType = existing.ResponseContentType;
            }

            context.Response.Headers["Idempotent-Replayed"] = "true";
            if (existing.ResponseBody is not null)
            {
                await context.Response.WriteAsync(existing.ResponseBody);
            }

            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var responseText = await new StreamReader(buffer).ReadToEndAsync(context.RequestAborted);
        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody, context.RequestAborted);

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            set.Add(new IdempotencyRecord
            {
                Key = key,
                RequestHash = requestHash,
                ResponseStatusCode = context.Response.StatusCode,
                ResponseBody = responseText.Length == 0 ? null : responseText,
                ResponseContentType = context.Response.ContentType,
                CreatedAt = clock.GetUtcNow(),
            });

            try
            {
                await db.SaveChangesAsync(context.RequestAborted);
            }
            catch (DbUpdateException)
            {
                // Concurrent request with the same key won the race — safe to ignore.
            }
        }
    }

    private static async Task<string> HashRequestAsync(HttpRequest request)
    {
        using var sha = SHA256.Create();
        using var ms = new MemoryStream();
        await using (var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            await writer.WriteAsync($"{request.Method}\n{request.Path}{request.QueryString}\n");
        }

        await request.Body.CopyToAsync(ms);
        ms.Position = 0;
        return Convert.ToHexString(await sha.ComputeHashAsync(ms));
    }
}
