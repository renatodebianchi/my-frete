using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace MyFrete.Modules.Notifications.Sending;

public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string>? Data = null);

public interface INotificationSender
{
    Task SendAsync(IReadOnlyList<string> deviceTokens, PushMessage message, CancellationToken ct = default);
}

/// <summary>
/// MVP implementation over the Expo Push Service (wraps FCM + APNs). Swap for direct FCM/APNs
/// later without touching callers (research.md §2).
/// </summary>
public sealed class ExpoPushSender(HttpClient http, ILogger<ExpoPushSender> logger) : INotificationSender
{
    public async Task SendAsync(IReadOnlyList<string> deviceTokens, PushMessage message, CancellationToken ct = default)
    {
        if (deviceTokens.Count == 0)
        {
            return;
        }

        var payload = deviceTokens.Select(token => new
        {
            to = token,
            title = message.Title,
            body = message.Body,
            data = message.Data,
        });

        using var response = await http.PostAsJsonAsync("/--/api/v2/push/send", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Expo push returned {Status}: {Detail}", (int)response.StatusCode, detail);
            response.EnsureSuccessStatusCode();
        }
    }
}

/// <summary>Used when no push provider is configured (local dev without Expo credentials).</summary>
public sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(IReadOnlyList<string> deviceTokens, PushMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("[push:noop] {Count} device(s) — {Title}: {Body}",
            deviceTokens.Count, message.Title, message.Body);
        return Task.CompletedTask;
    }
}
