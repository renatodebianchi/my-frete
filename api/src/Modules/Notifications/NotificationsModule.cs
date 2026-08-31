using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyFrete.BuildingBlocks.Application;
using MyFrete.BuildingBlocks.Results;
using MyFrete.Modules.Notifications.Domain;
using MyFrete.Modules.Notifications.Sending;

namespace MyFrete.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var expoBaseUrl = configuration["Notifications:ExpoBaseUrl"];

        if (string.IsNullOrWhiteSpace(expoBaseUrl))
        {
            services.AddSingleton<INotificationSender, LoggingNotificationSender>();
        }
        else
        {
            services.AddHttpClient<INotificationSender, ExpoPushSender>(c =>
            {
                c.BaseAddress = new Uri(expoBaseUrl);
                c.Timeout = TimeSpan.FromSeconds(10);
            });
        }

        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/accounts/me/devices", async (RegisterDeviceRequest req, ISender sender) =>
        {
            var result = await sender.Send(new RegisterDeviceCommand(req.Platform, req.Token));
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Message, statusCode: 400);
        }).RequireAuthorization();

        return app;
    }
}

public sealed record RegisterDeviceRequest(string Platform, string Token);

public sealed record RegisterDeviceCommand(string Platform, string Token) : ICommand<Result>;

public sealed class RegisterDeviceHandler(DbContext db, IHttpContextAccessor http, TimeProvider clock)
    : IRequestHandler<RegisterDeviceCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceCommand cmd, CancellationToken ct)
    {
        var sub = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.HttpContext?.User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return Error.Unauthorized("notifications.not_authenticated", "Not authenticated.");
        }

        if (!Enum.TryParse<DevicePlatform>(cmd.Platform, ignoreCase: true, out var platform))
        {
            return Error.Validation("notifications.invalid_platform", "platform must be 'ios' or 'android'.");
        }

        var now = clock.GetUtcNow();
        var existing = await db.Set<DeviceToken>().FirstOrDefaultAsync(t => t.Token == cmd.Token, ct);
        if (existing is null)
        {
            db.Set<DeviceToken>().Add(new DeviceToken
            {
                UserId = userId,
                Platform = platform,
                Token = cmd.Token,
                LastSeenAt = now,
            });
        }
        else
        {
            existing.Platform = platform;
            existing.LastSeenAt = now;
        }

        return Result.Success();
    }
}
