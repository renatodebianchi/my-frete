using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MyFrete.BuildingBlocks.Contracts;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Accounts.Auth;
using MyFrete.Modules.Accounts.Domain;
using MyFrete.Modules.Accounts.Features;
using MyFrete.Modules.Accounts.Professionals;

namespace MyFrete.Modules.Accounts;

public static class AccountsModule
{
    public static IServiceCollection AddAccountsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            jwt = jwt with { SigningKey = configuration["Jwt:SigningKey"] ?? "" };
        }

        if (jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
        }

        services.AddSingleton(jwt);
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("client", p => p.RequireRole(Roles.Client))
            .AddPolicy("professional", p => p.RequireRole(Roles.Professional));

        services.TryAddScoped<IActiveTripGuard, NoActiveTripGuard>();
        services.TryAddScoped<IProfessionalDirectory, ProfessionalDirectory>();
        services.TryAddScoped<IVerificationProvider, NoOpVerificationProvider>();
        services.AddScoped<VerificationService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/v1");

        v1.MapPost("/auth/register", async (RegisterRequest req, ISender sender) =>
        {
            var result = await sender.Send(new RegisterCommand(
                req.Name, req.Email, req.Phone, req.Password, req.Roles, req.MaxLoadKg));
            return result.ToTokenResponse(StatusCodes.Status201Created);
        }).AllowAnonymous();

        v1.MapPost("/auth/login", async (LoginRequest req, ISender sender) =>
        {
            var result = await sender.Send(new LoginCommand(req.Email, req.Password));
            return result.ToTokenResponse();
        }).AllowAnonymous();

        v1.MapPost("/auth/refresh", async (RefreshRequest req, ISender sender) =>
        {
            var result = await sender.Send(new RefreshCommand(req.RefreshToken));
            return result.ToTokenResponse();
        }).AllowAnonymous();

        v1.MapGet("/accounts/me", async (ISender sender) =>
        {
            var result = await sender.Send(new GetMeQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        }).RequireAuthorization();

        v1.MapPatch("/professionals/me", async (UpdateProfessionalBody body, ISender sender) =>
        {
            var result = await sender.Send(new UpdateProfessionalCommand(body.MaxLoadKg, body.ImmediateAvailability));
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        }).RequireAuthorization("professional");

        v1.MapPatch("/professionals/me/location", async (UpdateLocationBody body, ISender sender) =>
        {
            var result = await sender.Send(new UpdateLocationCommand(body.Lat, body.Lng));
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        }).RequireAuthorization("professional");

        v1.MapPost("/privacy/data-subject-requests", async (DataSubjectRequestBody body, ISender sender) =>
        {
            var result = await sender.Send(new CreateDataSubjectRequestCommand(body.Kind, body.Details));
            return result.IsSuccess
                ? Results.Accepted($"/v1/privacy/data-subject-requests/{result.Value}")
                : result.Error.ToProblemResult();
        }).RequireAuthorization();

        v1.MapGet("/privacy/me/export", async (ISender sender) =>
        {
            var result = await sender.Send(new ExportMyDataQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        }).RequireAuthorization();

        return app;
    }

    private static IResult ToTokenResponse(this Result<AuthResult> result, int successStatus = StatusCodes.Status200OK)
    {
        if (result.IsFailure)
        {
            return result.Error.ToProblemResult();
        }

        var body = new
        {
            accessToken = result.Value.AccessToken,
            refreshToken = result.Value.RefreshToken,
            expiresInSeconds = result.Value.ExpiresInSeconds,
        };
        return Results.Json(body, statusCode: successStatus);
    }

    private static IResult ToProblemResult(this Error error)
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

public sealed record RegisterRequest(
    string Name,
    string Email,
    string Phone,
    string Password,
    List<string> Roles,
    decimal? MaxLoadKg);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record DataSubjectRequestBody(string Kind, string? Details);

public sealed record UpdateProfessionalBody(decimal? MaxLoadKg, bool? ImmediateAvailability);

public sealed record UpdateLocationBody(double Lat, double Lng);
