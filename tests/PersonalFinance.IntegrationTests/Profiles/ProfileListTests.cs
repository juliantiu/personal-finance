using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.Identity;
using PersonalFinance.Contracts.Profiles;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class ProfileListTests
{
    [Fact]
    public async Task Migration_preserves_existing_accounts_and_sessions_and_adds_only_list_tables()
    {
        await using var host = new AccountTestHost();
        await host.WithDatabaseAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261102230402_AddOpaqueAuthenticationTickets", TestContext.Current.CancellationToken);
            return true;
        });
        var accountId = await AddAccount(host);
        var sessionId = await CreateSession(host, accountId);
        await host.WithDatabaseAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261105164916_AddProfileList", TestContext.Current.CancellationToken);
            return true;
        });
        using var scope = host.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().FindAsync(sessionId, TestContext.Current.CancellationToken);
        Assert.Equal(accountId, session!.AccountId);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.True(await db.Users.AnyAsync(user => user.Id == accountId, TestContext.Current.CancellationToken));
            Assert.Equal(new[] { "GBP", "USD" }, await db.Currencies.OrderBy(currency => currency.Code).Select(currency => currency.Code).ToArrayAsync(TestContext.Current.CancellationToken));
            var tables = await db.Database.SqlQueryRaw<string>("SELECT [name] AS [Value] FROM sys.tables").ToListAsync(TestContext.Current.CancellationToken);
            Assert.Contains("BudgetProfiles", tables);
            Assert.Contains("ProfileAccess", tables);
            Assert.DoesNotContain("Categories", tables);
            Assert.DoesNotContain("Tags", tables);
            Assert.False(db.Database.HasPendingModelChanges());
            return true;
        });
    }

    [Fact]
    public async Task Temporary_Identity_login_ticket_cannot_authorize_financial_requests()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
        Assert.True((await manager.CreateAsync(new AccountIdentityUser
        { UserName = "ticket@example.test", Email = "ticket@example.test", EmailConfirmed = true }, "abcdefghijkl")).Succeeded);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost:7207/"), AllowAutoRedirect = false, HandleCookies = true });
        var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        using var signIn = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        { Content = JsonContent.Create(new { email = "ticket@example.test", password = "abcdefghijkl" }) };
        signIn.Headers.Add("X-CSRF-TOKEN", token!.RequestToken);
        using var loginResponse = await client.SendAsync(signIn, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, loginResponse.StatusCode);
        Assert.Contains(loginResponse.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-PF.Login=", StringComparison.Ordinal));
        using var response = await client.GetAsync("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        using var creation = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "Ticket only", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, creation.StatusCode);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.False(await db.BudgetProfiles.AnyAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    private sealed record AntiforgeryToken(string RequestToken);

    [Fact]
    public async Task List_includes_owned_editor_and_view_only_profiles_once_and_removal_takes_effect_immediately()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        var otherId = await AddAccount(host);
        var ownedId = Guid.NewGuid();
        var editedId = Guid.NewGuid();
        var viewedId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.AddRange(
                new BudgetProfileRecord { Id = ownedId, OwnerAccountId = accountId, Name = "Home", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = editedId, OwnerAccountId = otherId, Name = "Home", MainCurrencyCode = "USD" },
                new BudgetProfileRecord { Id = viewedId, OwnerAccountId = otherId, Name = "Shared savings", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = Guid.NewGuid(), OwnerAccountId = otherId, Name = "Private profile", MainCurrencyCode = "USD" });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = editedId, AccountId = accountId, Role = "editor" },
                new ProfileAccessRecord { ProfileId = viewedId, AccountId = accountId, Role = "view_only" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var sessionId = await CreateSession(host, accountId);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        // A caller-supplied account ID must not replace the BFF account.
        var profiles = await client.GetFromJsonAsync<ProfileListItem[]>("/api/budget-profiles?accountId=" + otherId, TestContext.Current.CancellationToken);
        Assert.NotNull(profiles);
        Assert.Equal(3, profiles.Length);
        Assert.Equal(3, profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.Equal("owner", Assert.Single(profiles, profile => profile.Id == ownedId).Role);
        Assert.Equal("editor", Assert.Single(profiles, profile => profile.Id == editedId).Role);
        Assert.Equal("view_only", Assert.Single(profiles, profile => profile.Id == viewedId).Role);
        Assert.Equal("USD", Assert.Single(profiles, profile => profile.Id == editedId).MainCurrency);
        Assert.DoesNotContain(profiles, profile => profile.Name == "Private profile");

        await host.WithDatabaseAsync(db => db.ProfileAccess.Where(value => value.ProfileId == viewedId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        var updated = await client.GetFromJsonAsync<ProfileListItem[]>("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(2, updated!.Length);
        Assert.DoesNotContain(updated, profile => profile.Id == viewedId);
    }

    [Fact]
    public async Task Opening_a_profile_returns_current_role_settings_and_setup_defaults_only_while_access_is_current()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        var ownerId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var incomeId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var iouId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
            { Id = profileId, OwnerAccountId = ownerId, Name = "Shared home", MainCurrencyCode = "GBP" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profileId, AccountId = accountId, Role = "editor" });
            db.ProfileCurrencies.Add(new ProfileCurrencyRecord { ProfileId = profileId, CurrencyCode = "USD" });
            db.Categories.Add(new CategoryRecord { Id = incomeId, ProfileId = profileId, Name = "Income", Kind = "income" });
            db.PaymentMethods.Add(new PaymentMethodRecord { Id = cashId, ProfileId = profileId, Kind = "cash" });
            db.Tags.Add(new TagRecord { Id = iouId, ProfileId = profileId, Name = "IOU", SystemRole = "iou" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var sessionId = await CreateSession(host, accountId);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);

        var listedProfiles = await client.GetFromJsonAsync<ProfileListItem[]>("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Contains(listedProfiles!, value => value.Id == profileId && value.Role == "editor");
        using var response = await client.GetAsync($"/api/budget-profiles/{profileId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(profileId, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Shared home", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("GBP", body.RootElement.GetProperty("mainCurrency").GetString());
        Assert.Equal("editor", body.RootElement.GetProperty("role").GetString());
        Assert.Contains("USD", body.RootElement.GetProperty("additionalCurrencies").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains(body.RootElement.GetProperty("categories").EnumerateArray(), value => value.GetProperty("kind").GetString() == "income");
        Assert.Contains(body.RootElement.GetProperty("paymentMethods").EnumerateArray(), value => value.GetProperty("kind").GetString() == "cash");
        Assert.Contains(body.RootElement.GetProperty("tags").EnumerateArray(), value => value.GetProperty("role").GetString() == "iou");

        using (var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
        using (var anonymousResponse = await anonymous.GetAsync($"/api/budget-profiles/{profileId}", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
            Assert.Null(anonymousResponse.Headers.Location);
            Assert.True(anonymousResponse.Headers.CacheControl?.NoStore);
        }

        await host.WithDatabaseAsync(async db =>
        {
            await db.ProfileAccess.Where(value => value.ProfileId == profileId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.Role, "view_only"), TestContext.Current.CancellationToken);
            return true;
        });
        using var viewer = await client.GetAsync($"/api/budget-profiles/{profileId}", TestContext.Current.CancellationToken);
        using var viewerBody = JsonDocument.Parse(await viewer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("view_only", viewerBody.RootElement.GetProperty("role").GetString());

        var ownedProfileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
            { Id = ownedProfileId, OwnerAccountId = accountId, Name = "Owned profile", MainCurrencyCode = "USD" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var owner = await client.GetAsync($"/api/budget-profiles/{ownedProfileId}", TestContext.Current.CancellationToken);
        using var ownerBody = JsonDocument.Parse(await owner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("owner", ownerBody.RootElement.GetProperty("role").GetString());

        await host.WithDatabaseAsync(db => db.ProfileAccess.Where(value => value.ProfileId == profileId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        using var revoked = await client.GetAsync($"/api/budget-profiles/{profileId}", TestContext.Current.CancellationToken);
        using var missing = await client.GetAsync($"/api/budget-profiles/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await revoked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False((await revoked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains("Shared home", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("unverified")]
    [InlineData("deleted")]
    public async Task Unusable_sessions_cannot_list_profiles(string state)
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var id = await AddAccount(host);
        var sessionId = await CreateSession(host, id);
        using var scope = host.Services.CreateScope();
        if (state == "unknown") sessionId += "tampered";
        if (state == "revoked")
            await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().RevokeAsync(sessionId, TestContext.Current.CancellationToken);
        if (state == "expired")
            await host.WithDatabaseAsync(db => db.BffSessions.ExecuteUpdateAsync(
                update => update.SetProperty(value => value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken));
        if (state == "unverified")
            await host.WithDatabaseAsync(db => db.Users.Where(user => user.Id == id).ExecuteUpdateAsync(
                update => update.SetProperty(value => value.EmailConfirmed, false), TestContext.Current.CancellationToken));
        if (state == "deleted")
            await host.WithDatabaseAsync(db => db.Users.Where(user => user.Id == id).ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        using var response = await client.GetAsync("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Anonymous_requests_receive_401_without_a_login_redirect()
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Verified_BFF_session_receives_an_empty_non_cacheable_list()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        var sessionId = await CreateSession(host, accountId);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        using var response = await client.GetAsync("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Endpoint_is_unavailable_without_backend_or_authentication(bool missingDatabase, bool missingEmail, bool oidcDisabled)
    {
        await using var host = new AccountTestHost
        {
            MissingDatabase = missingDatabase, MissingEmail = missingEmail, OpenIddictEnabled = !oidcDisabled
        };
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> AddAccount(AccountTestHost host, bool confirmed = true)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser
            {
                Id = id, UserName = id + "@example.test", Email = id + "@example.test", EmailConfirmed = confirmed
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        return id;
    }

    private static async Task<string> CreateSession(AccountTestHost host, Guid accountId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);
    }
}
