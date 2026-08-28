using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFrete.BuildingBlocks.Messaging;

/// <summary>Canonical JSON settings for event payloads and the outbox (camelCase, matches contracts/events.md).</summary>
public static class MyFreteJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
