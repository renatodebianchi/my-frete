using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyFrete.Modules.Trips.Domain;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class AvailabilityGuardTests(ApiFactory factory)
{
    // T035 — FR-004: a professional with an active transport cannot go available.

    [Fact]
    public async Task Cannot_go_available_while_a_transport_is_active()
    {
        var email = $"busy-{Guid.NewGuid():N}@e.com";
        var (token, _) = await factory.RegisterAsync(email, ["professional"], maxLoadKg: 120);
        var professionalId = await factory.ResolveUserIdAsync(email);
        await InsertActiveTripAsync(professionalId);

        var client = Authed(token);
        var response = await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Can_toggle_availability_off_even_with_an_active_trip()
    {
        var email = $"busyoff-{Guid.NewGuid():N}@e.com";
        var (token, _) = await factory.RegisterAsync(email, ["professional"], maxLoadKg: 120);
        var professionalId = await factory.ResolveUserIdAsync(email);
        await InsertActiveTripAsync(professionalId);

        var client = Authed(token);
        var response = await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task InsertActiveTripAsync(Guid professionalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        db.Add(Trip.Create(Guid.NewGuid(), Guid.NewGuid(), professionalId, 50m, "BRL", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private HttpClient Authed(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
