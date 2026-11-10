using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class CreateProfileTests
{
    [Fact]
    public async Task Verified_account_can_create_and_list_a_profile()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = accountId, UserName = "create@example.test", Email = "create@example.test", EmailConfirmed = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var scope = host.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddDays(1)),
            TestContext.Current.CancellationToken);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + session);
        var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.RequestToken);
        using var response = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "  Home  ", mainCurrency = "GBP", additionalCurrencies = new[] { "USD" } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedProfile>(TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, created!.Id);
        await host.WithDatabaseAsync(async db =>
        {
            var saved = await db.BudgetProfiles.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(created.Id, saved.Id);
            Assert.Equal("Home", saved.Name);
            Assert.Equal("GBP", saved.MainCurrencyCode);
            Assert.Equal(accountId, saved.OwnerAccountId);
            Assert.Empty(await db.ProfileAccess.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("USD", (await db.ProfileCurrencies.SingleAsync(TestContext.Current.CancellationToken)).CurrencyCode);
            var categories = await db.Categories.OrderBy(value => value.Kind).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "debt", "income", "savings" }, categories.Select(value => value.Kind));
            Assert.Equal(new[] { "Debt", "Income", "Savings" }, categories.Select(value => value.Name));
            Assert.All(categories, value => { Assert.Equal(created.Id, value.ProfileId); Assert.NotEqual(Guid.Empty, value.Id); });
            Assert.Equal("cash", (await db.PaymentMethods.SingleAsync(TestContext.Current.CancellationToken)).Kind);
            var tags = await db.Tags.OrderBy(value => value.SystemRole).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "iou", "iou_repayment" }, tags.Select(value => value.SystemRole));
            Assert.Equal(new[] { "IOU", "IOU REPAYMENT" }, tags.Select(value => value.Name));
            Assert.All(tags, value => Assert.Equal(created.Id, value.ProfileId));
            return true;
        });
        var list = await client.GetFromJsonAsync<PersonalFinance.Contracts.Profiles.ProfileListItem[]>("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(created.Id, Assert.Single(list!).Id);
        Assert.Equal("owner", list![0].Role);
        Assert.Equal($"/api/budget-profiles/{created.Id}", response.Headers.Location!.OriginalString);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task Validation_rejects_bad_names_and_currency_memberships_without_writes()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = await SignedInClient(host);
        foreach (var input in new object[]
        {
            new { mainCurrency = "GBP" },
            new { name = "", mainCurrency = "GBP" },
            new { name = " \t ", mainCurrency = "GBP" },
            new { name = new string('x', 101), mainCurrency = "GBP" },
            new { name = "Home" },
            new { name = "Home", mainCurrency = "EUR" },
            new { name = "Home", mainCurrency = "GBP", additionalCurrencies = new[] { "GBP" } },
            new { name = "Home", mainCurrency = "GBP", additionalCurrencies = new[] { "EUR" } },
            new { name = "Home", mainCurrency = "GBP", additionalCurrencies = new[] { "USD", "USD" } },
            new { name = "Home", mainCurrency = "GBP", additionalCurrencies = new string?[] { null } }
        })
        {
            using var response = await client.PostAsJsonAsync("/api/budget-profiles", input, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>(TestContext.Current.CancellationToken);
            Assert.NotEmpty(problem!.Errors);
        }
        await AssertNoProfiles(host);
    }

    [Fact]
    public async Task Both_main_currencies_optional_alternatives_and_duplicate_names_are_saved()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = await SignedInClient(host);
        foreach (var main in new[] { "GBP", "USD" })
        foreach (var enableAdditional in new[] { false, true })
        {
            using var response = await client.PostAsJsonAsync("/api/budget-profiles", new
            {
                name = " " + new string('x', 100) + " ", mainCurrency = main,
                additionalCurrencies = enableAdditional ? new[] { main == "GBP" ? "USD" : "GBP" } : []
            }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal(4, await db.BudgetProfiles.CountAsync(TestContext.Current.CancellationToken));
            Assert.All(await db.BudgetProfiles.ToArrayAsync(TestContext.Current.CancellationToken), profile => Assert.Equal(new string('x', 100), profile.Name));
            Assert.Equal(2, await db.ProfileCurrencies.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(12, await db.Categories.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(4, await db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(8, await db.Tags.CountAsync(TestContext.Current.CancellationToken));
            return true;
        });
    }

    [Fact]
    public async Task Posted_owner_cannot_assign_ownership_to_another_account()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = await SignedInClient(host);
        var spoofed = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync("/api/budget-profiles", new
        { name = "Home", mainCurrency = "GBP", ownerAccountId = spoofed, accountId = spoofed }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await host.WithDatabaseAsync(async db =>
        {
            var profile = await db.BudgetProfiles.SingleAsync(TestContext.Current.CancellationToken);
            Assert.NotEqual(spoofed, profile.OwnerAccountId);
            Assert.Equal((await db.Users.SingleAsync(TestContext.Current.CancellationToken)).Id, profile.OwnerAccountId);
            return true;
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_invalid_antiforgery_proof_causes_no_write(bool invalid)
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = await SignedInClient(host);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        if (invalid) client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
        using var response = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "Home", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoProfiles(host);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("tampered")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("unverified")]
    [InlineData("deleted")]
    public async Task Invalid_sessions_cannot_create(string state)
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = state == "anonymous" ? host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false }) : await SignedInClient(host, state);
        using var response = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "Home", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        await AssertNoProfiles(host);
    }

    [Fact]
    public async Task Sql_failure_during_default_insertion_rolls_back_the_whole_creation()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = await SignedInClient(host);
        await host.WithDatabaseAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [Tags] ADD CONSTRAINT [CK_Test_RejectRepayment] CHECK ([SystemRole] <> 'iou_repayment')", TestContext.Current.CancellationToken);
            return true;
        });
        using var response = await client.PostAsJsonAsync("/api/budget-profiles", new
        { name = "Rollback", mainCurrency = "GBP", additionalCurrencies = new[] { "USD" } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("CK_Test_RejectRepayment", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        await AssertNoProfiles(host);
        await host.WithDatabaseAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE [Tags] DROP CONSTRAINT [CK_Test_RejectRepayment]", TestContext.Current.CancellationToken);
            return true;
        });
        using var retry = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "Rollback", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Creation_migration_preserves_existing_profile_grant_and_session()
    {
        await using var host = new AccountTestHost();
        await host.WithDatabaseAsync(async db =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261105164916_AddProfileList", TestContext.Current.CancellationToken);
            return true;
        });
        using var client = await SignedInClient(host);
        var id = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            var owner = (await db.Users.SingleAsync(TestContext.Current.CancellationToken)).Id;
            var participant = Guid.NewGuid();
            db.Users.Add(new AccountIdentityUser { Id = participant, UserName = "shared@example.test", Email = "shared@example.test", EmailConfirmed = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [BudgetProfiles] ([Id], [OwnerAccountId], [Name], [MainCurrencyCode]) VALUES ({id}, {owner}, {"Existing"}, {"USD"})",
                TestContext.Current.CancellationToken);
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = id, AccountId = participant, Role = "view_only" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        await host.MigrateAsync();
        var list = await client.GetFromJsonAsync<PersonalFinance.Contracts.Profiles.ProfileListItem[]>("/api/budget-profiles", TestContext.Current.CancellationToken);
        Assert.Equal(id, Assert.Single(list!).Id);
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal("view_only", (await db.ProfileAccess.SingleAsync(TestContext.Current.CancellationToken)).Role);
            Assert.Equal(0, (await db.BudgetProfiles.SingleAsync(value => value.Id == id, TestContext.Current.CancellationToken)).CurrencySettingsVersion);
            Assert.Equal(2, await db.Users.CountAsync(TestContext.Current.CancellationToken));
            Assert.False(db.Database.HasPendingModelChanges());
            return true;
        });
        using var created = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "New", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Sql_constraints_reject_duplicate_initial_kinds_roles_and_memberships()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = await SignedInClient(host);
        using var response = await client.PostAsJsonAsync("/api/budget-profiles", new
        { name = "Constraints", mainCurrency = "GBP", additionalCurrencies = new[] { "USD" } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<CreatedProfile>(TestContext.Current.CancellationToken);
        foreach (Action<AccountIdentityDbContext> duplicate in new Action<AccountIdentityDbContext>[]
        {
            db => db.Categories.Add(new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profile!.Id, Name = "Another label", Kind = "income" }),
            db => db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profile!.Id, Kind = "cash" }),
            db => db.Tags.Add(new TagRecord { Id = Guid.NewGuid(), ProfileId = profile!.Id, Name = "Another label", SystemRole = "iou" }),
            db => db.ProfileCurrencies.Add(new ProfileCurrencyRecord { ProfileId = profile!.Id, CurrencyCode = "USD" })
        })
        {
            await host.WithDatabaseAsync(async db =>
            {
                duplicate(db);
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
                return true;
            });
        }
    }

    private static async Task<HttpClient> SignedInClient(AccountTestHost host, string state = "valid")
    {
        var accountId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = accountId, UserName = accountId + "@example.test", Email = accountId + "@example.test", EmailConfirmed = state != "unverified" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        using var scope = host.Services.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var session = await sessions.CreateAsync(new BffSession(accountId, "synthetic-access", "synthetic-refresh",
            DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddDays(1)), TestContext.Current.CancellationToken);
        if (state == "expired") await host.WithDatabaseAsync(async db =>
        {
            await db.BffSessions.Where(value => value.AccountId == accountId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken);
            return true;
        });
        if (state == "revoked") await sessions.RevokeAsync(session, TestContext.Current.CancellationToken);
        if (state == "deleted") await host.WithDatabaseAsync(async db =>
        {
            await db.Users.Where(user => user.Id == accountId).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + (state == "tampered" ? "invalid" : session));
        var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token!.RequestToken);
        return client;
    }

    private static async Task AssertNoProfiles(AccountTestHost host) => await host.WithDatabaseAsync(async db =>
    {
        Assert.Empty(await db.BudgetProfiles.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.ProfileCurrencies.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Categories.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.PaymentMethods.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Tags.ToArrayAsync(TestContext.Current.CancellationToken));
        return true;
    });

    private sealed record AntiforgeryToken(string RequestToken);
    private sealed record CreatedProfile(Guid Id);
}
