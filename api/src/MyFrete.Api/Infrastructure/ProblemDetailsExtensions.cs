using Microsoft.AspNetCore.Mvc;
using MyFrete.BuildingBlocks.Results;

namespace MyFrete.Api.Infrastructure;

/// <summary>Maps <see cref="Error"/> / unhandled exceptions to RFC 9457 ProblemDetails.</summary>
public static class ProblemDetailsExtensions
{
    public static void AddProblemDetailsHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
        {
            ctx.ProblemDetails.Instance ??= ctx.HttpContext.Request.Path;
            if (ctx.HttpContext.Items.TryGetValue("x-correlation-id", out var cid) && cid is string s)
            {
                ctx.ProblemDetails.Extensions["correlationId"] = s;
            }
        });
        services.AddExceptionHandler<ValidationExceptionHandler>();
    }

    public static (int Status, string Title) ToHttp(this ErrorType type) => type switch
    {
        ErrorType.Validation => (StatusCodes.Status422UnprocessableEntity, "Validation failed"),
        ErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
        ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
        ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
        _ => (StatusCodes.Status400BadRequest, "Request failed"),
    };

    public static IResult ToProblem(this Error error)
    {
        var (status, title) = error.Type.ToHttp();
        return Results.Problem(
            title: title,
            statusCode: status,
            detail: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
}
