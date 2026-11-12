using System.Net;
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

public sealed class PaymentMethodListTests
{
    [Theory]
    [InlineData("owner")]
    [InlineData("editor")]
    [InlineData("view_only")]
    public async Task Participants_read_only_the_requested_profiles_existing_methods_and_stable_ids(string role)
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        var ownerId = role == "owner" ? accountId : await AddAccount(host);
        var profileId = Guid.NewGuid();
        var otherProfileId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.AddRange(
                new BudgetProfileRecord { Id = profileId, OwnerAccountId = ownerId, Name = "Requested home", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = otherProfileId, OwnerAccountId = accountId, Name = "Other home", MainCurrencyCode = "USD" });
            if (role != "owner") db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profileId, AccountId = accountId, Role = role });
            db.PaymentMethods.AddRange(
                new PaymentMethodRecord { Id = cashId, ProfileId = profileId, Kind = "cash" },
                new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = otherProfileId, Kind = "cash" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var client = await AuthenticatedClient(host, accountId);
        var route = $"/api/budget-profiles/{profileId}/payment-methods";
        using var response = await client.GetAsync(route, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(profileId, body.RootElement.GetProperty("profileId").GetGuid());
        Assert.Equal("Requested home", body.RootElement.GetProperty("profileName").GetString());
        Assert.Equal(role, body.RootElement.GetProperty("role").GetString());
        var method = Assert.Single(body.RootElement.GetProperty("methods").EnumerateArray());
        Assert.Equal(cashId, method.GetProperty("id").GetGuid());
        Assert.Equal("cash", method.GetProperty("kind").GetString());
        Assert.Equal(new[] { "id", "kind" }, method.EnumerateObject().Select(property => property.Name).Order().ToArray());

        using var details = await client.GetAsync($"/api/budget-profiles/{profileId}", TestContext.Current.CancellationToken);
        using var detailsBody = JsonDocument.Parse(await details.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(cashId, Assert.Single(detailsBody.RootElement.GetProperty("paymentMethods").EnumerateArray()).GetProperty("id").GetGuid());
        using var reload = await client.GetAsync(route, TestContext.Current.CancellationToken);
        Assert.Equal(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await reload.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        if (role != "owner")
        {
            await host.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == profileId)
                .ExecuteUpdateAsync(update => update.SetProperty(access => access.Role, "view_only"), TestContext.Current.CancellationToken));
            using var changed = await client.GetAsync(route, TestContext.Current.CancellationToken);
            using var changedBody = JsonDocument.Parse(await changed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("view_only", changedBody.RootElement.GetProperty("role").GetString());
            await host.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == profileId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
            using var revoked = await client.GetAsync(route, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
            Assert.True(revoked.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("Requested home", await revoked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Private_and_nonexistent_profiles_are_indistinguishable_and_caller_account_ids_are_ignored()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        var ownerId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = ownerId, Name = "Secret home", MainCurrencyCode = "GBP" });
            db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profileId, Kind = "cash" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var client = await AuthenticatedClient(host, accountId);
        using var inaccessible = await client.GetAsync($"/api/budget-profiles/{profileId}/payment-methods?accountId={ownerId}", TestContext.Current.CancellationToken);
        using var missing = await client.GetAsync($"/api/budget-profiles/{Guid.NewGuid()}/payment-methods", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.True(inaccessible.Headers.CacheControl?.NoStore);
        Assert.True(missing.Headers.CacheControl?.NoStore);
        Assert.Equal(await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await inaccessible.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain("Secret home", await inaccessible.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Accessible_profile_with_no_methods_returns_a_genuine_empty_list_without_creating_cash()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        var profileId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = accountId, Name = "Empty fixture", MainCurrencyCode = "GBP" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var client = await AuthenticatedClient(host, accountId);
        using var response = await client.GetAsync($"/api/budget-profiles/{profileId}/payment-methods", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(body.RootElement.GetProperty("methods").EnumerateArray());
        await host.WithDatabaseAsync(async db =>
        {
            Assert.False(await db.PaymentMethods.AnyAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("unknown")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("unverified")]
    [InlineData("deleted")]
    public async Task Invalid_or_ineligible_sessions_cannot_read_methods(string state)
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        using var client = await AuthenticatedClient(host, accountId);
        if (state == "anonymous") client.DefaultRequestHeaders.Remove("Cookie");
        if (state == "unknown")
        {
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=unknown");
        }
        await host.WithDatabaseAsync(async db =>
        {
            if (state == "expired") await db.BffSessions.ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken);
            if (state == "revoked") await db.BffSessions.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            if (state == "unverified") await db.Users.Where(user => user.Id == accountId).ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, false), TestContext.Current.CancellationToken);
            if (state == "deleted") await db.Users.Where(user => user.Id == accountId).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var response = await client.GetAsync($"/api/budget-profiles/{Guid.NewGuid()}/payment-methods", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    private static async Task<Guid> AddAccount(AccountTestHost host)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = id, UserName = id + "@example.test", Email = id + "@example.test", EmailConfirmed = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        return id;
    }

    private static async Task<HttpClient> AuthenticatedClient(AccountTestHost host, Guid accountId)
    {
        using var scope = host.Services.CreateScope();
        var sessionId = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)), TestContext.Current.CancellationToken);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        return client;
    }
}
