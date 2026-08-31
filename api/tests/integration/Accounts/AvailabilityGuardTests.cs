using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyFrete.BuildingBlocks.Contracts;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class AvailabilityGuardTests(ApiFactory factory)
{
    // T035 — FR-004: a professional with an active transport cannot go available.

    private sealed class AlwaysBusyGuard : IActiveTripGuard
    {
        public Task<bool> HasActiveTripAsync(Guid professionalId, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<IReadOnlySet<Guid>> WithActiveTripAsync(
            IReadOnlyCollection<Guid> professionalIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(professionalIds.ToHashSet());
    }

    [Fact]
    public async Task Cannot_go_available_while_a_transport_is_active()
    {
        var (token, _) = await factory.RegisterAsync(
            $"busy-{Guid.NewGuid():N}@example.com", ["professional"], maxLoadKg: 120);

        using var busyFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.Replace(
                ServiceDescriptor.Scoped<IActiveTripGuard, AlwaysBusyGuard>())));

        var client = busyFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Can_toggle_availability_off_even_with_an_active_trip()
    {
        var (token, _) = await factory.RegisterAsync(
            $"busyoff-{Guid.NewGuid():N}@example.com", ["professional"], maxLoadKg: 120);

        using var busyFactory = factory.WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.Replace(
                ServiceDescriptor.Scoped<IActiveTripGuard, AlwaysBusyGuard>())));

        var client = busyFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
