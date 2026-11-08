using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
