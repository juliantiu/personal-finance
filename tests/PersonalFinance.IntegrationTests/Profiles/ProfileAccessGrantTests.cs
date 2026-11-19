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

public sealed class ProfileAccessGrantTests
{
    [Fact]
    public async Task Owner_grants_verified_account_and_changes_role_on_the_existing_row()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host, "owner@example.test");
        var recipientId = await AddAccount(host, "recipient@example.test");
        var profileId = await AddProfile(host, ownerId);
        using var owner = await ClientFor(host, ownerId);

        using var grant = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = " RECIPIENT@example.test ", role = "editor" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        Assert.True(grant.Headers.CacheControl?.NoStore);
        await AssertGrant(host, profileId, recipientId, "editor", expectedCount: 1);

        using var change = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "recipient@example.test", role = "view_only" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.True(change.Headers.CacheControl?.NoStore);
        await AssertGrant(host, profileId, recipientId, "view_only", expectedCount: 1);
    }

    [Fact]
    public async Task Missing_and_unverified_recipients_have_the_same_result_and_no_grant()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host, "owner@example.test");
        await AddAccount(host, "unverified@example.test", confirmed: false);
        var profileId = await AddProfile(host, ownerId);
        using var owner = await ClientFor(host, ownerId);

        using var missing = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "missing@example.test", role = "editor" }, TestContext.Current.CancellationToken);
        using var unverified = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "unverified@example.test", role = "editor" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unverified.StatusCode);
        using var missingDetails = await System.Text.Json.JsonDocument.ParseAsync(
            await missing.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        using var unverifiedDetails = await System.Text.Json.JsonDocument.ParseAsync(
            await unverified.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(missingDetails.RootElement.GetProperty("title").GetString(), unverifiedDetails.RootElement.GetProperty("title").GetString());
        Assert.Equal("No eligible account found.", missingDetails.RootElement.GetProperty("title").GetString());
        Assert.True(missing.Headers.CacheControl?.NoStore);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Empty(await db.ProfileAccess.Where(access => access.ProfileId == profileId).ToArrayAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    [Fact]
    public async Task Owner_cannot_grant_access_to_their_own_account()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host, "owner@example.test");
        var profileId = await AddProfile(host, ownerId);
        using var owner = await ClientFor(host, ownerId);

        using var response = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "owner@example.test", role = "editor" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("full access as the profile owner", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl?.NoStore);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Empty(await db.ProfileAccess.Where(access => access.ProfileId == profileId).ToArrayAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    [Fact]
    public async Task Only_owner_with_antiforgery_proof_can_assign_supported_roles()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host, "owner@example.test");
        var editorId = await AddAccount(host, "editor@example.test");
        var viewerId = await AddAccount(host, "viewer@example.test");
        var unrelatedId = await AddAccount(host, "unrelated@example.test");
        var recipientId = await AddAccount(host, "recipient@example.test");
        var profileId = await AddProfile(host, ownerId);
        await host.WithDatabaseAsync(async db =>
        {
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editorId, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewerId, Role = "view_only" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        using var owner = await ClientFor(host, ownerId);
        using var editor = await ClientFor(host, editorId);
        using var viewer = await ClientFor(host, viewerId);
        using var unrelated = await ClientFor(host, unrelatedId);
        using var noProof = await ClientFor(host, ownerId, includeAntiforgery: false);
        var deniedResponses = new List<HttpResponseMessage>();
        foreach (var client in new[] { editor, viewer, unrelated })
            deniedResponses.Add(await client.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
                new { email = "recipient@example.test", role = "editor" }, TestContext.Current.CancellationToken));
        using var rejectedProof = await noProof.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "recipient@example.test", role = "editor" }, TestContext.Current.CancellationToken);
        using var unsupportedRole = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "recipient@example.test", role = "owner" }, TestContext.Current.CancellationToken);
        using var anonymous = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        using var anonymousResponse = await anonymous.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "recipient@example.test", role = "editor" }, TestContext.Current.CancellationToken);

        Assert.All(deniedResponses, response =>
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            response.Dispose();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.True(anonymousResponse.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedProof.StatusCode);
        Assert.True(rejectedProof.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, unsupportedRole.StatusCode);
        Assert.True(unsupportedRole.Headers.CacheControl?.NoStore);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Empty(await db.ProfileAccess.Where(access => access.ProfileId == profileId && access.AccountId == recipientId)
                .ToArrayAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    [Fact]
    public async Task Persistence_failure_does_not_report_a_saved_grant_or_leave_a_row()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host, "owner@example.test");
        var recipientId = await AddAccount(host, "recipient@example.test");
        var profileId = await AddProfile(host, ownerId);
        await host.WithDatabaseAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER [RejectProfileAccessGrant] ON [ProfileAccess] AFTER INSERT, UPDATE AS BEGIN THROW 51000, N'Controlled grant persistence failure', 1; END",
                TestContext.Current.CancellationToken);
            return true;
        });
        using var owner = await ClientFor(host, ownerId);

        using var response = await owner.PostAsJsonAsync($"/api/budget-profiles/{profileId}/participants",
            new { email = "recipient@example.test", role = "editor" }, TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccessStatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        await host.WithDatabaseAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [RejectProfileAccessGrant]", TestContext.Current.CancellationToken);
            Assert.Empty(await db.ProfileAccess.Where(access => access.ProfileId == profileId && access.AccountId == recipientId)
                .ToArrayAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    private static async Task<Guid> AddAccount(AccountTestHost host, string email, bool confirmed = true)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser
            {
                Id = id,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = confirmed
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        return id;
    }

    private static async Task<Guid> AddProfile(AccountTestHost host, Guid ownerId)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
            { Id = id, OwnerAccountId = ownerId, Name = "Shared home", MainCurrencyCode = "GBP" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        return id;
    }

    private static async Task AssertGrant(AccountTestHost host, Guid profileId, Guid recipientId, string role, int expectedCount) =>
        await host.WithDatabaseAsync(async db =>
        {
            var grants = await db.ProfileAccess.Where(access => access.ProfileId == profileId)
                .ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expectedCount, grants.Length);
            Assert.Equal(role, Assert.Single(grants, grant => grant.AccountId == recipientId).Role);
            return true;
        });

    private static async Task<HttpClient> ClientFor(AccountTestHost host, Guid accountId, bool includeAntiforgery = true)
    {
        using var scope = host.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);
        var client = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + session);
        if (includeAntiforgery)
        {
            var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.RequestToken);
        }
        return client;
    }

    private sealed record AntiforgeryToken(string RequestToken);
}
