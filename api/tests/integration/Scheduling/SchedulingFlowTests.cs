using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace MyFrete.Tests.Integration.Scheduling;

[Collection(ApiCollection.Name)]
public sealed class SchedulingFlowTests(ApiFactory factory)
{
    private static readonly object Origin = new { text = "Paulista", point = new { lat = -23.5613, lng = -46.6560 } };
    private static readonly object Destination = new { text = "Ibirapuera", point = new { lat = -23.5874, lng = -46.6576 } };

    // T083 — V5: broadcast to available professionals, first accept wins, others filled_by_other (SC-005).

    [Fact]
    public async Task First_professional_to_accept_wins_the_schedule()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);
        var (client, requestId) = await ExhaustedRequestAsync();

        var schedule = await client.PostAsJsonAsync($"/v1/requests/{requestId}/schedule-decision",
            new { decision = "schedule", scheduledDate = date.ToString("yyyy-MM-dd") });
        schedule.StatusCode.Should().Be(HttpStatusCode.OK);

        var proA = await AvailableProAsync(date, maxLoadKg: 200);
        var proB = await AvailableProAsync(date, maxLoadKg: 200);

        var offerA = await WaitForScheduledOfferAsync(proA);
        var offerB = await WaitForScheduledOfferAsync(proB);

        var acceptA = proA.PostAsync($"/v1/schedule-offers/{offerA}/accept", null);
        var acceptB = proB.PostAsync($"/v1/schedule-offers/{offerB}/accept", null);
        var results = await Task.WhenAll(acceptA, acceptB);

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var request = await client.GetFromJsonAsync<JsonElement>($"/v1/requests/{requestId}");
        request.GetProperty("status").GetString().Should().Be("scheduled");
    }

    // T084 — the per-date limit hides an already-loaded professional; decline -> unfulfilled.

    [Fact]
    public async Task Declining_the_schedule_offer_marks_the_request_unfulfilled()
    {
        var (client, requestId) = await ExhaustedRequestAsync();

        var decline = await client.PostAsJsonAsync($"/v1/requests/{requestId}/schedule-decision",
            new { decision = "decline" });

        decline.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await decline.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("unfulfilled");
    }

    [Fact]
    public async Task Availability_dates_round_trip()
    {
        var (token, _) = await factory.RegisterAsync($"av-{Guid.NewGuid():N}@e.com", ["professional"], 100);
        var pro = Authed(token);
        var d1 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5).ToString("yyyy-MM-dd");
        var d2 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(9).ToString("yyyy-MM-dd");

        (await pro.PutAsJsonAsync("/v1/professionals/me/schedule-availability", new[] { d1, d2 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var got = await pro.GetFromJsonAsync<string[]>("/v1/professionals/me/schedule-availability");
        got.Should().BeEquivalentTo([d1, d2]);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(HttpClient Client, Guid RequestId)> ExhaustedRequestAsync()
    {
        await factory.ParkAllProfessionalsAsync();
        await factory.SetConfigAsync("max_professionals_contacted", "1");
        await factory.SetConfigAsync("max_search_duration_seconds", "5");
        await factory.SetConfigAsync("scheduling_window_days", "30");
        await factory.SetConfigAsync("max_schedules_per_date", "1");

        var (token, _) = await factory.RegisterAsync($"sc-{Guid.NewGuid():N}@e.com", ["client"]);
        var client = Authed(token);

        var create = await client.PostAsJsonAsync("/v1/requests", new
        {
            items = new[] { new { description = "Caixa", quantity = 1 } },
            estimatedWeightKg = 25,
            origin = Origin,
            destination = Destination,
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await PollUntil(async () =>
        {
            var r = await client.GetFromJsonAsync<JsonElement>($"/v1/requests/{id}");
            return r.GetProperty("status").GetString() == "awaiting_schedule_decision";
        }, TimeSpan.FromSeconds(25));

        return (client, id);
    }

    private async Task<HttpClient> AvailableProAsync(DateOnly date, decimal maxLoadKg)
    {
        var (token, _) = await factory.RegisterAsync($"sp-{Guid.NewGuid():N}@e.com", ["professional"], maxLoadKg);
        var pro = Authed(token);
        await pro.PutAsJsonAsync("/v1/professionals/me/schedule-availability", new[] { date.ToString("yyyy-MM-dd") });
        return pro;
    }

    private static async Task<Guid> WaitForScheduledOfferAsync(HttpClient pro)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var inbox = await pro.GetFromJsonAsync<JsonElement>("/v1/schedule-offers/inbox");
            if (inbox.GetArrayLength() > 0)
            {
                return inbox[0].GetProperty("id").GetGuid();
            }

            await Task.Delay(1000);
        }

        throw new Xunit.Sdk.XunitException("no scheduled offer arrived in time");
    }

    private static async Task PollUntil(Func<Task<bool>> probe, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await probe())
            {
                return;
            }

            await Task.Delay(1000);
        }

        throw new Xunit.Sdk.XunitException("condition not met in time");
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
