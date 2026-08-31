using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Trips;

[Collection(ApiCollection.Name)]
public sealed class TripLifecycleTests(ApiFactory factory)
{
    private static readonly object Origin = new { text = "Paulista", point = new { lat = -23.5613, lng = -46.6560 } };
    private static readonly object Destination = new { text = "Ibirapuera", point = new { lat = -23.5874, lng = -46.6576 } };

    // T045/T046 — deliver -> request completed -> client confirms; disputes are recorded.

    [Fact]
    public async Task Delivery_completes_the_request_and_client_can_confirm()
    {
        var (client, pro, requestId, tripId) = await AssignedTripAsync();

        var deliver = await pro.PostAsync($"/v1/trips/{tripId}/deliver", null);
        deliver.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deliver.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("entregue");

        var request = await client.GetFromJsonAsync<JsonElement>($"/v1/requests/{requestId}");
        request.GetProperty("status").GetString().Should().Be("completed");

        var confirm = await client.PostAsJsonAsync($"/v1/trips/{tripId}/client-response", new { response = "confirm" });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK);
        (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("confirmada");
    }

    [Fact]
    public async Task Client_can_dispute_a_delivered_trip()
    {
        var (client, pro, _, tripId) = await AssignedTripAsync();

        await pro.PostAsync($"/v1/trips/{tripId}/deliver", null);
        var dispute = await client.PostAsJsonAsync($"/v1/trips/{tripId}/client-response", new { response = "dispute", note = "wrong address" });

        dispute.StatusCode.Should().Be(HttpStatusCode.OK);
        (await dispute.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("contestada");
    }

    [Fact]
    public async Task Agreed_amount_can_be_edited_before_completion_but_not_after()
    {
        var (client, pro, _, tripId) = await AssignedTripAsync();

        var edit = await client.PatchAsJsonAsync($"/v1/trips/{tripId}/agreed-amount", new { amount = 55.5m });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await edit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("agreedAmount").GetDecimal().Should().Be(55.5m);

        await pro.PostAsync($"/v1/trips/{tripId}/deliver", null);
        var afterDeliver = await client.PatchAsJsonAsync($"/v1/trips/{tripId}/agreed-amount", new { amount = 10m });
        afterDeliver.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<(HttpClient Client, HttpClient Pro, Guid RequestId, Guid TripId)> AssignedTripAsync()
    {
        await factory.ParkAllProfessionalsAsync();
        await factory.SetConfigAsync("offer_ttl_seconds", "30");
        await factory.SetConfigAsync("max_professionals_contacted", "8");

        var (proToken, _) = await factory.RegisterAsync($"tpro-{Guid.NewGuid():N}@e.com", ["professional"], 200);
        var pro = Authed(proToken);
        await pro.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true });
        await pro.PatchAsJsonAsync("/v1/professionals/me/location", new { lat = -23.5613, lng = -46.6560 });

        var (clientToken, _) = await factory.RegisterAsync($"tcli-{Guid.NewGuid():N}@e.com", ["client"]);
        var client = Authed(clientToken);

        var create = await client.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "Caixa", quantity = 1 } },
            estimatedWeightKg = 20,
            origin = Origin,
            destination = Destination,
        });
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var offerId = await PollOfferAsync(pro);
        var accept = await pro.PostAsync($"/v1/offers/{offerId}/accept", null);
        accept.EnsureSuccessStatusCode();
        var tripId = (await accept.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tripId").GetGuid();

        return (client, pro, requestId, tripId);
    }

    private static async Task<Guid> PollOfferAsync(HttpClient pro)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var inbox = await pro.GetFromJsonAsync<JsonElement>("/v1/offers/inbox");
            if (inbox.GetArrayLength() > 0)
            {
                return inbox[0].GetProperty("id").GetGuid();
            }

            await Task.Delay(1000);
        }

        throw new Xunit.Sdk.XunitException("no offer arrived in time");
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
