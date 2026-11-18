using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class ProfileParticipantsTests
{
    [Fact]
    public async Task Only_owner_can_review_saved_participants_and_response_is_not_cached()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host, "owner@example.test");
        var editorId = await AddAccount(host, "editor@example.test");
        var viewerId = await AddAccount(host, "viewer@example.test");
        var unrelatedId = await AddAccount(host, "unrelated@example.test");
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
            { Id = profileId, OwnerAccountId = ownerId, Name = "Shared home", MainCurrencyCode = "GBP" });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editorId, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewerId, Role = "view_only" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        using var owner = await ClientFor(host, ownerId);
        using var response = await owner.GetAsync($"/api/budget-profiles/{profileId}/participants", TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected OK but received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        Assert.True(response.Headers.CacheControl?.NoStore);
        var participants = await response.Content.ReadFromJsonAsync<ParticipantResponse[]>(TestContext.Current.CancellationToken);
        Assert.NotNull(participants);
        Assert.Equal(
            [new ParticipantResponse("editor@example.test", "editor"), new ParticipantResponse("viewer@example.test", "view_only")],
            participants);

        foreach (var accountId in new[] { editorId, viewerId, unrelatedId })
        {
            using var client = await ClientFor(host, accountId);
            using var denied = await client.GetAsync($"/api/budget-profiles/{profileId}/participants", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Assert.True(denied.Headers.CacheControl?.NoStore);
        }

        using var anonymous = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousResponse = await anonymous.GetAsync($"/api/budget-profiles/{profileId}/participants", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.True(anonymousResponse.Headers.CacheControl?.NoStore);
    }

    private static async Task<Guid> AddAccount(AccountTestHost host, string email)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = id, UserName = email, Email = email, EmailConfirmed = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        return id;
    }

    private static async Task<HttpClient> ClientFor(AccountTestHost host, Guid accountId)
    {
        using var scope = host.Services.CreateScope();
        var sessionId = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        return client;
    }

    private sealed record ParticipantResponse(string Email, string Role);
}
