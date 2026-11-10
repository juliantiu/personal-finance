using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class ProfileCurrencySettingsTests
{
    [Fact]
    public async Task Owner_can_update_additional_currency_and_stale_or_view_only_writes_are_rejected()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host);
        var viewerId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
                { Id = profileId, OwnerAccountId = ownerId, Name = "Currency updates", MainCurrencyCode = "GBP" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profileId, AccountId = viewerId, Role = "view_only" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        using var owner = await ClientFor(host, ownerId);
        var initial = await owner.GetFromJsonAsync<JsonElement>($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
        var version = initial.GetProperty("configurationVersion").GetInt64();
        var proof = await GetAntiforgeryProof(owner);
        var saved = await UpdateCurrencies(owner, profileId, proof, version, ["USD"]);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        using (saved)
        {
            var refreshed = await owner.GetFromJsonAsync<JsonElement>($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
            Assert.Equal("USD", Assert.Single(refreshed.GetProperty("additionalCurrencies").EnumerateArray()).GetString());
            Assert.Equal(version + 1, refreshed.GetProperty("configurationVersion").GetInt64());
        }

        var sameSettings = await UpdateCurrencies(owner, profileId, proof, version + 1, ["USD"]);
        using (sameSettings) Assert.Equal(HttpStatusCode.NoContent, sameSettings.StatusCode);

        var stale = await UpdateCurrencies(owner, profileId, proof, version, []);
        using (stale) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var viewer = await ClientFor(host, viewerId);
        var viewerProof = await GetAntiforgeryProof(viewer);
        var denied = await UpdateCurrencies(viewer, profileId, viewerProof, version + 1, []);
        using (denied) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var unchanged = await owner.GetFromJsonAsync<JsonElement>($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
        Assert.Equal("USD", Assert.Single(unchanged.GetProperty("additionalCurrencies").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Currency_update_requires_antiforgery_proof_and_invalid_configuration_is_not_saved()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
                { Id = profileId, OwnerAccountId = ownerId, Name = "Currency updates", MainCurrencyCode = "GBP" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        using var owner = await ClientFor(host, ownerId);
        using var missingProofRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/budget-profiles/{profileId}/currency-settings")
        {
            Content = JsonContent.Create(new { additionalCurrencies = new[] { "GBP" }, configurationVersion = 0L })
        };
        var missingProof = await owner.SendAsync(missingProofRequest, TestContext.Current.CancellationToken);
        using (missingProof) Assert.Equal(HttpStatusCode.BadRequest, missingProof.StatusCode);

        var proof = await GetAntiforgeryProof(owner);
        var invalid = await UpdateCurrencies(owner, profileId, proof, 0, ["GBP"]);
        using (invalid) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var unchanged = await owner.GetFromJsonAsync<JsonElement>($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
        Assert.Empty(unchanged.GetProperty("additionalCurrencies").EnumerateArray());
    }

    [Fact]
    public async Task Persistence_failure_rolls_back_currency_memberships_and_configuration_version()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
                { Id = profileId, OwnerAccountId = ownerId, Name = "Rollback", MainCurrencyCode = "GBP" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER TR_ProfileCurrencies_RejectInsert ON ProfileCurrencies AFTER INSERT AS BEGIN THROW 51000, 'Synthetic currency write failure', 1; END",
                TestContext.Current.CancellationToken);
            return true;
        });

        using var owner = await ClientFor(host, ownerId);
        var proof = await GetAntiforgeryProof(owner);
        var failed = await UpdateCurrencies(owner, profileId, proof, 0, ["USD"]);
        using (failed)
        {
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            Assert.True(failed.Headers.CacheControl?.NoStore);
        }

        var unchanged = await owner.GetFromJsonAsync<JsonElement>($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
        Assert.Empty(unchanged.GetProperty("additionalCurrencies").EnumerateArray());
        Assert.Equal(0, unchanged.GetProperty("configurationVersion").GetInt64());
    }

    [Fact]
    public async Task Current_participants_can_review_profile_currencies_and_catalog_without_leaking_inaccessible_settings()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var ownerId = await AddAccount(host);
        var editorId = await AddAccount(host);
        var viewerId = await AddAccount(host);
        var outsiderId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
                { Id = profileId, OwnerAccountId = ownerId, Name = "Private currencies", MainCurrencyCode = "GBP" });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editorId, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewerId, Role = "view_only" });
            db.ProfileCurrencies.Add(new ProfileCurrencyRecord { ProfileId = profileId, CurrencyCode = "USD" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        foreach (var (accountId, role) in new[]
        {
            (ownerId, "owner"), (editorId, "editor"), (viewerId, "view_only")
        })
        {
            using var client = await ClientFor(host, accountId);
            using var response = await client.GetAsync($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var root = json.RootElement;
            Assert.Equal(profileId, root.GetProperty("profileId").GetGuid());
            Assert.Equal("GBP", root.GetProperty("mainCurrency").GetString());
            Assert.Equal("USD", Assert.Single(root.GetProperty("additionalCurrencies").EnumerateArray()).GetString());
            Assert.Equal(role, root.GetProperty("role").GetString());
            var choices = root.GetProperty("supportedCurrencies").EnumerateArray().ToArray();
            Assert.Equal(new[] { "GBP", "USD" }, choices.Select(choice => choice.GetProperty("code").GetString()).ToArray());
            Assert.Equal("Pound sterling", choices[0].GetProperty("name").GetString());
            Assert.Equal("£", choices[0].GetProperty("symbol").GetString());
            Assert.Equal("US dollar", choices[1].GetProperty("name").GetString());
            Assert.Equal("$", choices[1].GetProperty("symbol").GetString());
        }

        using var outsider = await ClientFor(host, outsiderId);
        using var inaccessible = await outsider.GetAsync($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
        using var nonexistent = await outsider.GetAsync($"/api/budget-profiles/{Guid.NewGuid()}/currency-settings", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistent.StatusCode);
        Assert.Equal(await nonexistent.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await inaccessible.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False((await inaccessible.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Contains("Private currencies", StringComparison.Ordinal));

        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousResponse = await anonymous.GetAsync($"/api/budget-profiles/{profileId}/currency-settings", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Null(anonymousResponse.Headers.Location);
        Assert.True(anonymousResponse.Headers.CacheControl?.NoStore);
    }

    private static async Task<HttpClient> ClientFor(AccountTestHost host, Guid accountId)
    {
        using var scope = host.Services.CreateScope();
        var sessionId = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        return client;
    }

    private static async Task<string> GetAntiforgeryProof(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("requestToken").GetString()!;
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        var currentCookie = client.DefaultRequestHeaders.GetValues("Cookie").Single();
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", currentCookie + "; " + cookie.Split(';', 2)[0]);
        return token;
    }

    private static async Task<HttpResponseMessage> UpdateCurrencies(HttpClient client, Guid profileId, string token, long version, string[] currencies)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/budget-profiles/{profileId}/currency-settings")
        {
            Content = JsonContent.Create(new { additionalCurrencies = currencies, configurationVersion = version })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> AddAccount(AccountTestHost host)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser
                { Id = id, UserName = id + "@example.test", Email = id + "@example.test", EmailConfirmed = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        return id;
    }
}
