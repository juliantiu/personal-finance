using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class RevealCardTests
{
    [Theory]
    [InlineData("owner", HttpStatusCode.OK)]
    [InlineData("editor", HttpStatusCode.OK)]
    [InlineData("view_only", HttpStatusCode.Forbidden)]
    [InlineData("none", HttpStatusCode.NotFound)]
    public async Task Reveal_requires_current_writer_permission_and_never_changes_the_card(string role, HttpStatusCode expected)
    {
        await using var host = NewHost();
        var (client, profile, card, account) = await Setup(host);
        using (client)
        {
            if (role != "owner") await SetRole(host, profile, account, role);
            var before = await host.WithDatabaseAsync(db => db.PaymentMethods.AsNoTracking().SingleAsync(x => x.Id == card, TestContext.Current.CancellationToken));
            using var response = await client.PostAsync(Route(profile, card), null, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            if (expected == HttpStatusCode.OK)
            {
                using var json = JsonDocument.Parse(body);
                Assert.Equal("4111111111111111", json.RootElement.GetProperty("number").GetString());
                Assert.Single(json.RootElement.EnumerateObject());
            }
            else Assert.DoesNotContain("4111111111111111", body);
            var after = await host.WithDatabaseAsync(db => db.PaymentMethods.AsNoTracking().SingleAsync(x => x.Id == card, TestContext.Current.CancellationToken));
            Assert.Equal(before.Version, after.Version);
            Assert.Equal(before.ProtectedNumber, after.ProtectedNumber);
            foreach (var path in new[] { $"/api/budget-profiles/{profile}/payment-methods", $"/api/budget-profiles/{profile}" })
            {
                using var masked = await client.GetAsync(path, TestContext.Current.CancellationToken);
                Assert.DoesNotContain("4111111111111111", await masked.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("cash")]
    [InlineData("other_profile")]
    public async Task Unavailable_method_identifiers_have_the_same_safe_response(string target)
    {
        await using var host = NewHost();
        var (client, profile, card, account) = await Setup(host);
        using (client)
        {
            if (target == "missing") card = Guid.NewGuid();
            if (target == "cash") card = await host.WithDatabaseAsync(db => db.PaymentMethods.Where(x => x.Kind == "cash").Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken));
            if (target == "other_profile")
            {
                profile = Guid.NewGuid();
                await host.WithDatabaseAsync(async db =>
                {
                    db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profile, OwnerAccountId = account, Name = "Other accessible profile", MainCurrencyCode = "GBP" });
                    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                    return true;
                });
            }
            using var response = await client.PostAsync(Route(profile, card), null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("missing_proof", HttpStatusCode.BadRequest)]
    [InlineData("invalid_proof", HttpStatusCode.BadRequest)]
    [InlineData("missing_session", HttpStatusCode.Unauthorized)]
    [InlineData("expired_session", HttpStatusCode.Unauthorized)]
    [InlineData("ineligible_account", HttpStatusCode.Unauthorized)]
    public async Task Reveal_requires_a_live_eligible_session_and_antiforgery(string failure, HttpStatusCode expected)
    {
        await using var host = NewHost();
        var (client, profile, card, account) = await Setup(host);
        using (client)
        {
            if (failure.EndsWith("proof")) client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            if (failure == "invalid_proof") client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "invalid");
            if (failure == "missing_session") client.DefaultRequestHeaders.Remove("Cookie");
            if (failure == "expired_session") await host.WithDatabaseAsync(db => db.BffSessions.ExecuteUpdateAsync(x => x.SetProperty(s => s.ExpiresAt, DateTimeOffset.UtcNow.AddDays(-1)), TestContext.Current.CancellationToken));
            if (failure == "ineligible_account") await host.WithDatabaseAsync(db => db.Users.Where(x => x.Id == account).ExecuteUpdateAsync(x => x.SetProperty(u => u.EmailConfirmed, false), TestContext.Current.CancellationToken));
            using var response = await client.PostAsync(Route(profile, card), null, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("4111111111111111", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("other_method")]
    [InlineData("other_profile")]
    [InlineData("missing_key")]
    public async Task Invalid_or_unavailable_protection_returns_safe_dependency_failure(string failure)
    {
        await using var host = NewHost();
        var (client, profile, card, _) = await Setup(host);
        using (client)
        {
            string ciphertext;
            using (var scope = host.Services.CreateScope())
            {
                var provider = failure == "missing_key" ? new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()
                    : scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
                ciphertext = Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions.Protect(
                    provider.CreateProtector("PersonalFinance.CardNumber", "v1", (failure == "other_profile" ? Guid.NewGuid() : profile).ToString("N"),
                        (failure == "other_method" ? Guid.NewGuid() : card).ToString("N")), "4111111111111111");
            }
            if (failure == "corrupt") ciphertext = "corrupt synthetic ciphertext";
            await host.WithDatabaseAsync(db => db.PaymentMethods.Where(x => x.Id == card).ExecuteUpdateAsync(x => x.SetProperty(m => m.ProtectedNumber, ciphertext), TestContext.Current.CancellationToken));
            using var response = await client.PostAsync(Route(profile, card), null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("4111111111111111", body);
            Assert.DoesNotContain(ciphertext, body);
        }
    }

    [Fact]
    public async Task Reveal_returns_the_requested_number_when_two_cards_share_a_masked_suffix()
    {
        await using var host = NewHost();
        var (client, profile, first, _) = await Setup(host);
        using (client)
        {
            using var saved = await client.PostAsJsonAsync($"/api/budget-profiles/{profile}/payment-methods", new { number = "4000000000061111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            using var created = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var second = created.RootElement.GetProperty("id").GetGuid();
            foreach (var pair in new[] { (first, "4111111111111111"), (second, "4000000000061111") })
            {
                using var response = await client.PostAsync(Route(profile, pair.Item1), null, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var number = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Equal(pair.Item2, number.RootElement.GetProperty("number").GetString());
            }
        }
    }

    [Fact]
    public async Task Reveal_remains_available_without_a_matching_key_but_requires_persistent_protection_readiness()
    {
        await using var host = NewHost();
        var (client, profile, card, _) = await Setup(host);
        using (client)
        {
            host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Cards:MatchingKey"] = null;
            using var response = await client.PostAsync(Route(profile, card), null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope = host.Services.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
            var protection = new PersonalFinance.Infrastructure.Profiles.CardNumberProtection(provider,
                host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(), new(false));
            var ciphertext = await host.WithDatabaseAsync(db => db.PaymentMethods.Where(x => x.Id == card).Select(x => x.ProtectedNumber).SingleAsync(TestContext.Current.CancellationToken));
            Assert.Throws<PersonalFinance.Application.Profiles.CardRevealUnavailableException>(() => protection.Unprotect(profile, card, ciphertext!));
        }
    }

    [Fact]
    public async Task Persistent_keys_reveal_the_same_card_through_http_after_restart()
    {
        using var keys = new TestKeyMaterial();
        var database = "PersonalFinance_UC001_Test_" + Guid.NewGuid().ToString("N");
        Guid profile, card, account;
        await using (var first = new AccountTestHost { DatabaseName = database, UsePersistentKeys = true, Settings = keys.Settings, DeleteDatabaseOnDispose = false })
        {
            var setup = await Setup(first);
            using var client = setup.Client;
            profile = setup.Profile; card = setup.Card; account = setup.Account;
        }
        await using var restarted = new AccountTestHost { DatabaseName = database, UsePersistentKeys = true, Settings = keys.Settings };
        using var next = await Client(restarted, account);
        using var response = await next.PostAsync(Route(profile, card), null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("4111111111111111", json.RootElement.GetProperty("number").GetString());
    }

    private static AccountTestHost NewHost() => new() { Settings = new() { ["Cards:MatchingKey"] = Convert.ToBase64String(new byte[32]) } };
    private static string Route(Guid profile, Guid card) => $"/api/budget-profiles/{profile}/payment-methods/{card}/reveal";

    private static async Task SetRole(AccountTestHost host, Guid profile, Guid account, string role)
    {
        await host.WithDatabaseAsync(async db =>
        {
            var owner = Guid.NewGuid();
            db.Users.Add(new AccountIdentityUser { Id = owner, UserName = owner + "@example.test", EmailConfirmed = true });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.BudgetProfiles.Where(x => x.Id == profile).ExecuteUpdateAsync(x => x.SetProperty(p => p.OwnerAccountId, owner), TestContext.Current.CancellationToken);
            await db.ProfileAccess.Where(x => x.ProfileId == profile && x.AccountId == account).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            if (role != "none") db.ProfileAccess.Add(new() { ProfileId = profile, AccountId = account, Role = role });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
    }

    private static async Task<(HttpClient Client, Guid Profile, Guid Card, Guid Account)> Setup(AccountTestHost host)
    {
        await host.MigrateAsync();
        var account = Guid.NewGuid(); var profile = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = account, UserName = account + "@example.test", EmailConfirmed = true });
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profile, OwnerAccountId = account, Name = "Synthetic reveal", MainCurrencyCode = "GBP" });
            db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profile, Kind = "cash" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var client = await Client(host, account);
        using var saved = await client.PostAsJsonAsync($"/api/budget-profiles/{profile}/payment-methods", new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        using var json = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (client, profile, json.RootElement.GetProperty("id").GetGuid(), account);
    }

    private static async Task<HttpClient> Client(AccountTestHost host, Guid account)
    {
        using var scope = host.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(new BffSession(account, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)), TestContext.Current.CancellationToken);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + session);
        using var proof = await client.GetAsync("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await proof.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.RootElement.GetProperty("requestToken").GetString());
        return client;
    }
}
