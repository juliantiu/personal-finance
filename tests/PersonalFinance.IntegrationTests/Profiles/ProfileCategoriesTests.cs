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

public sealed class ProfileCategoriesTests
{
    [Fact]
    public async Task Current_participants_read_persisted_category_identity_names_kinds_and_current_role_without_leaks()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var owner = await AddAccount(host);
        var editor = await AddAccount(host);
        var viewer = await AddAccount(host);
        var outsider = await AddAccount(host);
        var profileId = Guid.NewGuid();
        var privateProfileId = Guid.NewGuid();
        var categories = new[]
        {
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Earnings", Kind = "income" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Rainy day", Kind = "savings" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Loans", Kind = "debt" }
        };
        await host.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.AddRange(
                new BudgetProfileRecord { Id = profileId, OwnerAccountId = owner, Name = "Shared budget", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = privateProfileId, OwnerAccountId = outsider, Name = "Private budget", MainCurrencyCode = "GBP" });
            db.Categories.AddRange(categories);
            db.Categories.Add(new CategoryRecord { Id = Guid.NewGuid(), ProfileId = privateProfileId, Name = "Private category", Kind = "income" });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = profileId, AccountId = editor, Role = "editor" },
                new ProfileAccessRecord { ProfileId = profileId, AccountId = viewer, Role = "view_only" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        foreach (var (accountId, role) in new[] { (owner, "owner"), (editor, "editor"), (viewer, "view_only") })
        {
            using var client = await ClientFor(host, accountId);
            for (var read = 0; read < 2; read++)
            {
                using var response = await client.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.True(response.Headers.CacheControl?.NoStore);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                var root = json.RootElement;
                Assert.Equal(profileId, root.GetProperty("profileId").GetGuid());
                Assert.Equal(role, root.GetProperty("role").GetString());
                var items = root.GetProperty("categories").EnumerateArray().ToArray();
                Assert.Equal(3, items.Length);
                foreach (var expected in categories)
                {
                    var item = Assert.Single(items, item => item.GetProperty("id").GetGuid() == expected.Id);
                    Assert.Equal(expected.Name, item.GetProperty("name").GetString());
                    Assert.Equal(expected.Kind, item.GetProperty("kind").GetString());
                }
                Assert.DoesNotContain("Private category", root.ToString());
            }
        }

        using var editorClient = await ClientFor(host, editor);
        await host.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == profileId && access.AccountId == editor)
            .ExecuteUpdateAsync(update => update.SetProperty(access => access.Role, "view_only"), TestContext.Current.CancellationToken));
        using (var changedRole = await editorClient.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken))
        {
            using var json = JsonDocument.Parse(await changedRole.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("view_only", json.RootElement.GetProperty("role").GetString());
        }
        await host.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == profileId && access.AccountId == editor)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        using var revoked = await editorClient.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        using var inaccessible = await editorClient.GetAsync($"/api/budget-profiles/{privateProfileId}/categories", TestContext.Current.CancellationToken);
        using var missing = await editorClient.GetAsync($"/api/budget-profiles/{Guid.NewGuid()}/categories", TestContext.Current.CancellationToken);
        foreach (var response in new[] { revoked, inaccessible, missing })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal(await missing.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        using var outsiderClient = await ClientFor(host, outsider);
        using var outsiderResponse = await outsiderClient.GetAsync($"/api/budget-profiles/{profileId}/categories", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("unknown-session")]
    [InlineData("expired-session")]
    [InlineData("unconfirmed")]
    [InlineData("removed-account")]
    public async Task Missing_or_ineligible_sessions_cannot_read_categories(string scenario)
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = await AddAccount(host);
        using var client = scenario == "anonymous" || scenario == "unknown-session"
            ? host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })
            : await ClientFor(host, accountId);
        if (scenario == "unknown-session") client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=unknown");
        if (scenario == "expired-session")
            await host.WithDatabaseAsync(db => db.BffSessions.Where(session => session.AccountId == accountId)
                .ExecuteUpdateAsync(update => update.SetProperty(session => session.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken));
        if (scenario == "unconfirmed")
            await host.WithDatabaseAsync(db => db.Users.Where(user => user.Id == accountId)
                .ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, false), TestContext.Current.CancellationToken));
        if (scenario == "removed-account")
            await host.WithDatabaseAsync(db => db.Users.Where(user => user.Id == accountId).ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        using var response = await client.GetAsync($"/api/budget-profiles/{Guid.NewGuid()}/categories", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain("categories", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<HttpClient> ClientFor(AccountTestHost host, Guid accountId)
    {
        using var scope = host.Services.CreateScope();
        var sessionId = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15),
                DateTimeOffset.UtcNow.AddHours(1)), TestContext.Current.CancellationToken);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + sessionId);
        return client;
    }

    private static async Task<Guid> AddAccount(AccountTestHost host)
    {
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = id, UserName = id + "@example.test", Email = id + "@example.test", EmailConfirmed = true });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });
        return id;
    }
}
