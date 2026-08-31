using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Matching;

[Collection(ApiCollection.Name)]
public sealed class ImmediateMatchingTests(ApiFactory factory)
{
    private static readonly object Origin = new { text = "Paulista", point = new { lat = -23.5613, lng = -46.6560 } };
    private static readonly object Destination = new { text = "Ibirapuera", point = new { lat = -23.5874, lng = -46.6576 } };

    // T046 — happy path: nearest available professional is offered, accepts within the window,
    // a trip is created, the request becomes hired and the professional becomes ineligible.

    [Fact]
    public async Task Offer_reaches_a_professional_who_accepts_and_a_trip_is_created()
    {
        await factory.ParkAllProfessionalsAsync();
        await factory.SetConfigAsync("offer_ttl_seconds", "30");
        await factory.SetConfigAsync("max_professionals_contacted", "8");

        var proEmail = $"pro-{Guid.NewGuid():N}@e.com";
        var pro = await MakeAvailableProfessionalAsync(proEmail, -23.5610, -46.6558, maxLoadKg: 200);
        var client = await ClientAsync();

        var requestId = await CreateRequestAsync(client, weightKg: 30);

        var offer = await WaitForOfferAsync(pro);
        offer.GetProperty("requestId").GetGuid().Should().Be(requestId);

        var offerId = offer.GetProperty("id").GetGuid();
        var accept = await pro.PostAsync($"/v1/offers/{offerId}/accept", null);
        accept.StatusCode.Should().Be(HttpStatusCode.OK);
        var tripId = (await accept.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tripId").GetGuid();

        var request = await client.GetFromJsonAsync<JsonElement>($"/v1/requests/{requestId}");
        request.GetProperty("status").GetString().Should().Be("hired");
        request.GetProperty("assignedProfessionalId").GetGuid().Should().Be(await factory.ResolveUserIdAsync(proEmail));

        var trip = await pro.GetFromJsonAsync<JsonElement>($"/v1/trips/{tripId}");
        trip.GetProperty("status").GetString().Should().Be("contratada");

        // Now ineligible for a second request (FR-011a).
        var second = await CreateRequestAsync(client, weightKg: 20);
        var noOffer = await OfferOrNullAsync(pro, within: TimeSpan.FromSeconds(8));
        noOffer.Should().BeNull("the professional already has an active trip");
        _ = second;
    }

    // T047 — the window expires, the search moves on, and after the professional cap the
    // request goes to awaiting_schedule_decision; a late accept is rejected.

    [Fact]
    public async Task Expired_offer_advances_and_then_exhausts_the_search()
    {
        await factory.ParkAllProfessionalsAsync();
        await factory.SetConfigAsync("offer_ttl_seconds", "3");
        await factory.SetConfigAsync("max_professionals_contacted", "1");

        var pro = await MakeAvailableProfessionalAsync($"slow-{Guid.NewGuid():N}@e.com", -23.5611, -46.6559, 200);
        var client = await ClientAsync();
        var requestId = await CreateRequestAsync(client, 25);

        var offer = await WaitForOfferAsync(pro);
        var offerId = offer.GetProperty("id").GetGuid();

        // Let the 3s window expire (poll cadence ~1s + dispatcher).
        await Task.Delay(TimeSpan.FromSeconds(8));

        var lateAccept = await pro.PostAsync($"/v1/offers/{offerId}/accept", null);
        lateAccept.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var request = await Poll(async () =>
        {
            var r = await client.GetFromJsonAsync<JsonElement>($"/v1/requests/{requestId}");
            return r.GetProperty("status").GetString() == "awaiting_schedule_decision" ? r : (JsonElement?)null;
        }, TimeSpan.FromSeconds(20));

        request.Should().NotBeNull("cap of 1 professional reached -> awaiting_schedule_decision");
    }

    // ---------------------------------------------------------------- helpers

    private async Task<HttpClient> ClientAsync()
    {
        var (token, _) = await factory.RegisterAsync($"c-{Guid.NewGuid():N}@e.com", ["client"]);
        return Authed(token);
    }

    private async Task<HttpClient> MakeAvailableProfessionalAsync(string email, double lat, double lng, decimal maxLoadKg)
    {
        var (token, _) = await factory.RegisterAsync(email, ["professional"], maxLoadKg);
        var client = Authed(token);
        (await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true }))
            .EnsureSuccessStatusCode();
        (await client.PatchAsJsonAsync("/v1/professionals/me/location", new { lat, lng }))
            .EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> CreateRequestAsync(HttpClient client, int weightKg)
    {
        var res = await client.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "Caixa", quantity = 1 } },
            estimatedWeightKg = weightKg,
            origin = Origin,
            destination = Destination,
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<JsonElement> WaitForOfferAsync(HttpClient pro) =>
        Poll(async () =>
            {
                var inbox = await pro.GetFromJsonAsync<JsonElement>("/v1/offers/inbox");
                return inbox.GetArrayLength() > 0 ? inbox[0] : (JsonElement?)null;
            }, TimeSpan.FromSeconds(20))
            .ContinueWith(t => t.Result ?? throw new Xunit.Sdk.XunitException("no offer arrived in time"));

    private static async Task<JsonElement?> OfferOrNullAsync(HttpClient pro, TimeSpan within)
    {
        var deadline = DateTime.UtcNow + within;
        while (DateTime.UtcNow < deadline)
        {
            var inbox = await pro.GetFromJsonAsync<JsonElement>("/v1/offers/inbox");
            if (inbox.GetArrayLength() > 0)
            {
                return inbox[0];
            }

            await Task.Delay(1000);
        }

        return null;
    }

    private static async Task<JsonElement?> Poll(Func<Task<JsonElement?>> probe, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(1000);
        }

        return null;
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
