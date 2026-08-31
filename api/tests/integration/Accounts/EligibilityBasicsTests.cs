using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyFrete.Modules.Accounts.Domain;
using MyFrete.Modules.Accounts.Professionals;
using Xunit;

namespace MyFrete.Tests.Integration.Accounts;

[Collection(ApiCollection.Name)]
public sealed class EligibilityBasicsTests(ApiFactory factory)
{
    // T034 — a professional is eligible for an immediate request only when
    // available AND max load >= estimated weight (FR-011).

    [Fact]
    public async Task Eligible_only_when_available_and_capacity_covers_the_weight()
    {
        var email = $"elig-{Guid.NewGuid():N}@example.com";
        var (token, _) = await factory.RegisterAsync(email, ["professional"], maxLoadKg: 100);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var professionalId = await ResolveUserIdAsync(email);

        (await EligibleAsync(50_000)).Should().NotContain(professionalId, "not available yet");

        await client.PatchAsJsonAsync("/v1/professionals/me", new { immediateAvailability = true });

        (await EligibleAsync(80_000)).Should().Contain(professionalId, "80kg <= 100kg capacity");
        (await EligibleAsync(150_000)).Should().NotContain(professionalId, "150kg > 100kg capacity");
    }

    private async Task<IReadOnlyList<Guid>> EligibleAsync(int weightGrams)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IProfessionalDirectory>()
            .GetEligibleForImmediateAsync(weightGrams);
    }

    private async Task<Guid> ResolveUserIdAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        return await db.Set<User>().Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }
}
