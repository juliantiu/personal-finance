using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Profiles;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Profiles;

public sealed class SaveCardTests
{
    [Fact]
    public async Task Card_persistence_rejects_cash_entities_without_writing_card_details()
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host);
        using (client)
        {
            await host.WithDatabaseAsync(async db =>
            {
                var store = new EfCardPaymentMethodStore(db);
                var cash = PaymentMethod.CreateCash(profile);
                await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(account, cash,
                    new ProtectedCard("synthetic ciphertext", new byte[32], new string('F', 64), "1111"), TestContext.Current.CancellationToken));
                return true;
            });
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \t ", null)]
    [InlineData("  Travel card  ", "Travel card")]
    public async Task Nickname_is_saved_and_returned_in_masked_projections(string? nickname, string? expected)
    {
        await using var host = NewHost();
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            using var saved = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111", nickname }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            using var savedJson = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(savedJson.RootElement.TryGetProperty("nickname", out var savedNickname));
            Assert.Equal(expected, savedNickname.GetString());
            using var list = await client.GetAsync(Route(profile), TestContext.Current.CancellationToken);
            using var listJson = JsonDocument.Parse(await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var card = listJson.RootElement.GetProperty("methods").EnumerateArray().Single(value => value.GetProperty("kind").GetString() == "credit_card");
            Assert.Equal(expected, card.GetProperty("nickname").GetString());
            Assert.Equal("•••• 1111", card.GetProperty("maskedIdentifier").GetString());
            using var detail = await client.GetAsync($"/api/budget-profiles/{profile}", TestContext.Current.CancellationToken);
            using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(expected, detailJson.RootElement.GetProperty("paymentMethods").EnumerateArray().Single(value => value.GetProperty("kind").GetString() == "credit_card").GetProperty("nickname").GetString());
        }
    }

    [Fact]
    public async Task New_profiles_create_cash_with_the_default_nickname()
    {
        await using var host = NewHost();
        var (client, _, _) = await Setup(host);
        using (client)
        {
            using var created = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "Cash nickname", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var list = await client.GetAsync(Route(body.RootElement.GetProperty("id").GetGuid()), TestContext.Current.CancellationToken);
            using var methods = JsonDocument.Parse(await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var cash = Assert.Single(methods.RootElement.GetProperty("methods").EnumerateArray());
            Assert.True(cash.TryGetProperty("nickname", out var nickname));
            Assert.Equal("Cash", nickname.GetString());
        }
    }

    [Fact]
    public async Task Nickname_migration_preserves_cards_and_defaults_existing_cash()
    {
        await using var host = NewHost();
        await host.WithDatabaseAsync(async db => { await db.GetService<IMigrator>().MigrateAsync("20261112103729_AddEncryptedCards", TestContext.Current.CancellationToken); return true; });
        var account = Guid.NewGuid(); var profile = Guid.NewGuid(); var cash = Guid.NewGuid(); var card = Guid.NewGuid();
        using var scope = host.Services.CreateScope();
        Assert.True(CardNumber.TryCreate("4111111111111111", out var number));
        var protectedCard = scope.ServiceProvider.GetRequiredService<ICardNumberProtection>().Protect(profile, card, number!);
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = account, UserName = account + "@example.test", Email = account + "@example.test", EmailConfirmed = true });
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profile, OwnerAccountId = account, Name = "Before nicknames", MainCurrencyCode = "GBP" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [PaymentMethods] ([Id], [ProfileId], [Kind]) VALUES ({cash}, {profile}, 'cash')", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [PaymentMethods] ([Id], [ProfileId], [Kind], [ProtectedNumber], [NumberFingerprint], [MatchingKeyId], [LastFour]) VALUES ({card}, {profile}, 'credit_card', {protectedCard.Ciphertext}, {protectedCard.Fingerprint}, {protectedCard.KeyId}, {protectedCard.LastFour})", TestContext.Current.CancellationToken);
            return true;
        });
        var beforeVersion = await host.WithDatabaseAsync(db => db.PaymentMethods.Where(value => value.Id == card).Select(value => value.Version).SingleAsync(TestContext.Current.CancellationToken));
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(new BffSession(account, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)), TestContext.Current.CancellationToken);
        await host.MigrateAsync();
        await host.WithDatabaseAsync(async db =>
        {
            Assert.Equal("Cash", (await db.PaymentMethods.SingleAsync(value => value.Id == cash, TestContext.Current.CancellationToken)).Nickname);
            var saved = await db.PaymentMethods.SingleAsync(value => value.Id == card, TestContext.Current.CancellationToken);
            Assert.Null(saved.Nickname);
            Assert.Equal(protectedCard.Ciphertext, saved.ProtectedNumber);
            Assert.Equal(protectedCard.Fingerprint, saved.NumberFingerprint);
            Assert.Equal(beforeVersion, saved.Version);
            Assert.Equal(profile, saved.ProfileId);
            Assert.False(db.Database.HasPendingModelChanges());
            return true;
        });
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().FindAsync(session, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(100, HttpStatusCode.Created)]
    [InlineData(101, HttpStatusCode.BadRequest)]
    public async Task Nickname_length_is_checked_after_trim_without_partial_saves(int length, HttpStatusCode expected)
    {
        await using var host = NewHost();
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            using var response = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111", nickname = "  " + new string('x', length) + "  " }, TestContext.Current.CancellationToken);
            Assert.Equal(expected, response.StatusCode);
            Assert.Equal(expected == HttpStatusCode.Created ? 2 : 1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
            if (expected == HttpStatusCode.BadRequest)
            {
                Assert.Contains("Nickname", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.DoesNotContain("4111111111111111", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public async Task Duplicate_nicknames_are_allowed_and_do_not_change_card_identity()
    {
        await using var host = NewHost();
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            foreach (var number in new[] { "4111111111111111", "4000000000061111" })
            {
                using var response = await client.PostAsJsonAsync(Route(profile), new { number, nickname = "Shared name" }, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }
            using var duplicate = await client.PostAsJsonAsync(Route(profile), new { number = "4111-1111 1111 1111", nickname = "Different name" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            await host.WithDatabaseAsync(async db =>
            {
                var saved = await db.PaymentMethods.Where(value => value.Kind == "credit_card").ToArrayAsync(TestContext.Current.CancellationToken);
                Assert.Equal(2, saved.Length);
                Assert.All(saved, value => Assert.Equal("Shared name", value.Nickname));
                return true;
            });
        }
    }

    [Theory]
    [InlineData("owner", HttpStatusCode.Created)]
    [InlineData("editor", HttpStatusCode.Created)]
    [InlineData("view_only", HttpStatusCode.Forbidden)]
    [InlineData("none", HttpStatusCode.NotFound)]
    public async Task Saving_uses_current_profile_permission_and_returns_only_masked_details(string role, HttpStatusCode expected)
    {
        await using var host = NewHost();
        var (client, profile, _) = await Setup(host, role);
        using (client)
        using (var response = await client.PostAsJsonAsync(Route(profile), new { number = "4111-1111 1111 1111" }, TestContext.Current.CancellationToken))
        {
            Assert.Equal(expected, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("4111111111111111", body);
            Assert.DoesNotContain("4111-1111", body);
            if (expected == HttpStatusCode.Created)
            {
                Assert.Contains("1111", body);
                using var list = await client.GetAsync(Route(profile), TestContext.Current.CancellationToken);
                Assert.Contains("1111", await list.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }
            Assert.Equal(expected == HttpStatusCode.Created ? 2 : 1,
                await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public async Task Invalid_and_equivalent_duplicate_numbers_have_safe_field_errors_and_no_extra_rows()
    {
        await using var host = NewHost();
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            using var invalid = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111112" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.DoesNotContain("4111111111111112", await invalid.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var first = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            using var duplicate = await client.PostAsJsonAsync(Route(profile), new { number = "4111 1111-1111 1111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Contains("Number", await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public async Task Antiforgery_and_removed_grants_prevent_writes()
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host, "editor");
        using (client)
        {
            var proof = client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            using var missing = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", proof);
            await host.WithDatabaseAsync(db => db.ProfileAccess.Where(value => value.AccountId == account).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
            using var removed = await client.PostAsJsonAsync(Route(profile), new { number = "bad" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public async Task Storage_is_encrypted_resource_bound_and_SQL_enforces_uniqueness_and_card_shape()
    {
        await using var host = NewHost();
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            using var saved = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            var card = await host.WithDatabaseAsync(db => db.PaymentMethods.AsNoTracking().SingleAsync(value => value.Kind == "credit_card", TestContext.Current.CancellationToken));
            Assert.DoesNotContain("4111111111111111", card.ProtectedNumber!);
            Assert.Equal(32, card.NumberFingerprint!.Length);
            Assert.Equal(64, card.MatchingKeyId!.Length);
            Assert.Equal("1111", card.LastFour);
            Assert.Equal(8, card.Version.Length);
            var provider = host.Services.GetRequiredService<IDataProtectionProvider>();
            Assert.Equal("4111111111111111", provider.CreateProtector("PersonalFinance.CardNumber", "v1", profile.ToString("N"), card.Id.ToString("N")).Unprotect(card.ProtectedNumber!));
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => provider.CreateProtector("PersonalFinance.CardNumber", "v1", profile.ToString("N"), Guid.NewGuid().ToString("N")).Unprotect(card.ProtectedNumber!));
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => provider.CreateProtector("PersonalFinance.CardNumber", "v1", Guid.NewGuid().ToString("N"), card.Id.ToString("N")).Unprotect(card.ProtectedNumber!));
            // Direct provider writes bypass the handler and still cannot duplicate active or retained identities.
            await host.WithDatabaseAsync(async db =>
            {
                card.Id = Guid.NewGuid(); card.Version = []; card.RetiredAt = DateTimeOffset.UtcNow;
                db.PaymentMethods.Add(card);
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
                Assert.Contains("IX_PaymentMethods_ProfileId_NumberFingerprint", error.InnerException!.Message);
                return true;
            });
            foreach (var kind in new[] { "cash", "credit_card" })
                await host.WithDatabaseAsync(async db =>
                {
                    db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profile, Kind = kind, LastFour = "abcd" });
                    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
                    return true;
                });
            using var detail = await client.GetAsync($"/api/budget-profiles/{profile}", TestContext.Current.CancellationToken);
            var body = await detail.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.DoesNotContain("4111111111111111", body);
            Assert.DoesNotContain(card.ProtectedNumber!, body);
            Assert.DoesNotContain("fingerprint", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("1111", body);
            await host.WithDatabaseAsync(async db =>
            {
                var failure = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync("20261106110747_AddProfileCreation", TestContext.Current.CancellationToken));
                Assert.Contains("Cannot downgrade while saved cards exist", failure.Message);
                return true;
            });
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(value => value.Kind == "credit_card", TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public async Task Concurrent_equivalent_saves_yield_one_card_while_last_four_and_other_profiles_are_not_identity()
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host);
        using (client)
        {
            var responses = await Task.WhenAll(new[] { "4111111111111111", "4111-1111 1111 1111" }.Select(number => client.PostAsJsonAsync(Route(profile), new { number }, TestContext.Current.CancellationToken)));
            try { Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, responses.Select(response => response.StatusCode).Order()); }
            finally { foreach (var response in responses) response.Dispose(); }
            // Both synthetic numbers pass Luhn and end in 1111, but their complete identities differ.
            using var different = await client.PostAsJsonAsync(Route(profile), new { number = "4000000000061111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, different.StatusCode);
            var other = Guid.NewGuid();
            await host.WithDatabaseAsync(async db =>
            {
                db.BudgetProfiles.Add(new BudgetProfileRecord { Id = other, OwnerAccountId = account, Name = "Other synthetic profile", MainCurrencyCode = "GBP" });
                db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = other, Kind = "cash" });
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                return true;
            });
            using var sameElsewhere = await client.PostAsJsonAsync(Route(other), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, sameElsewhere.StatusCode);
            Assert.Equal(3, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(value => value.Kind == "credit_card", TestContext.Current.CancellationToken)));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("short")]
    [InlineData("ephemeral")]
    [InlineData("failure")]
    public async Task Missing_keys_and_protection_failure_do_not_disable_existing_profile_or_account_features(string state)
    {
        var settings = new Dictionary<string, string?> { ["Cards:MatchingKey"] = state switch
        { "missing" => "", "malformed" => "bad-base64", "short" => Convert.ToBase64String(new byte[16]), _ => Convert.ToBase64String(new byte[32]) } };
        await using var host = new AccountTestHost
        {
            Settings = settings,
            ConfigureAdditionalServices = services =>
            {
                if (state == "ephemeral") services.AddSingleton(new CardKeyReadiness(false));
                if (state == "failure") services.AddScoped<ICardNumberProtection, FailedProtection>();
            }
        };
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            using var response = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.DoesNotContain("4111111111111111", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var existing = await client.GetAsync(Route(profile), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
            using var created = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "Still works", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var registration = await client.PostAsJsonAsync("/api/accounts/register", new { email = Guid.NewGuid() + "@example.test", password = "abcdefghijkl", confirmPassword = "abcdefghijkl" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, registration.StatusCode);
        }
    }

    [Fact]
    public async Task Mismatched_matching_key_fails_readiness_instead_of_bypassing_uniqueness()
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host);
        using (client)
        {
            using var saved = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            await host.WithDatabaseAsync(async db =>
            {
                var store = new EfCardPaymentMethodStore(db);
                var result = await store.SaveAsync(account, PaymentMethod.CreateCreditCard(profile, "4111111111111111"), new ProtectedCard("ciphertext", new byte[32], new string('F', 64), "1111"), TestContext.Current.CancellationToken);
                Assert.Equal(CardSaveStatus.Unavailable, result.Status);
                return true;
            });
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(value => value.Kind == "credit_card", TestContext.Current.CancellationToken)));
        }
    }

    [Theory]
    [InlineData("view_only", CardSaveStatus.Forbidden)]
    [InlineData("removed", CardSaveStatus.UnavailableProfile)]
    public async Task Store_rechecks_access_after_the_application_precheck(string role, CardSaveStatus expected)
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host, "editor");
        using (client)
        {
            await host.WithDatabaseAsync(async db =>
            {
                var store = new EfCardPaymentMethodStore(db);
                Assert.Equal("editor", await store.GetRoleAsync(account, profile, TestContext.Current.CancellationToken));
                if (role == "removed") await db.ProfileAccess.Where(value => value.AccountId == account).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                else await db.ProfileAccess.Where(value => value.AccountId == account).ExecuteUpdateAsync(update => update.SetProperty(value => value.Role, role), TestContext.Current.CancellationToken);
                var result = await store.SaveAsync(account, PaymentMethod.CreateCreditCard(profile, "4111111111111111"), new ProtectedCard("ciphertext", new byte[32], new string('F', 64), "1111"), TestContext.Current.CancellationToken);
                Assert.Equal(expected, result.Status);
                return true;
            });
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public async Task Failure_after_SQL_insert_rolls_back_all_card_fields()
    {
        await using var host = new AccountTestHost
        {
            Settings = new() { ["Cards:MatchingKey"] = Convert.ToBase64String(new byte[32]) },
            ConfigureAdditionalServices = services => services.AddDbContext<AccountIdentityDbContext>(options => options.AddInterceptors(new FailAfterCardInsert()))
        };
        var (client, profile, _) = await Setup(host);
        using (client)
        {
            using var response = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public async Task Migration_preserves_baseline_cash_ids_grants_sessions_and_profile_creation()
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host, "editor", baseline: true);
        using (client)
        {
            var cashId = await host.WithDatabaseAsync(db => db.Database.SqlQuery<Guid>($"SELECT [Id] AS [Value] FROM [PaymentMethods] WHERE [ProfileId] = {profile}").SingleAsync(TestContext.Current.CancellationToken));
            await host.MigrateAsync();
            await host.WithDatabaseAsync(async db =>
            {
                Assert.Equal(cashId, (await db.PaymentMethods.SingleAsync(TestContext.Current.CancellationToken)).Id);
                Assert.Equal("editor", (await db.ProfileAccess.SingleAsync(TestContext.Current.CancellationToken)).Role);
                Assert.True(await db.BffSessions.AnyAsync(value => value.AccountId == account, TestContext.Current.CancellationToken));
                Assert.False(db.Database.HasPendingModelChanges());
                return true;
            });
            using var saved = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            using var created = await client.PostAsJsonAsync("/api/budget-profiles", new { name = "After migration", mainCurrency = "GBP" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
    }

    [Fact]
    public async Task Persistent_protection_and_matching_keys_survive_a_host_restart()
    {
        using var keys = new TestKeyMaterial();
        var databaseName = "PersonalFinance_UC001_Test_" + Guid.NewGuid().ToString("N");
        Guid profile;
        PaymentMethodRecord card;
        await using (var initial = new AccountTestHost { DatabaseName = databaseName, UsePersistentKeys = true, Settings = keys.Settings, DeleteDatabaseOnDispose = false })
        {
            var setup = await Setup(initial);
            profile = setup.Profile;
            using var client = setup.Client;
            using var saved = await client.PostAsJsonAsync(Route(profile), new { number = "4111111111111111" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            card = await initial.WithDatabaseAsync(db => db.PaymentMethods.AsNoTracking().SingleAsync(value => value.Kind == "credit_card", TestContext.Current.CancellationToken));
        }
        await using var restarted = new AccountTestHost { DatabaseName = databaseName, UsePersistentKeys = true, Settings = keys.Settings };
        await restarted.MigrateAsync();
        var provider = restarted.Services.GetRequiredService<IDataProtectionProvider>();
        Assert.Equal("4111111111111111", provider.CreateProtector("PersonalFinance.CardNumber", "v1", profile.ToString("N"), card.Id.ToString("N")).Unprotect(card.ProtectedNumber!));
        using var scope = restarted.Services.CreateScope();
        Assert.True(CardNumber.TryCreate("4111-1111 1111 1111", out var number));
        var protectedAgain = scope.ServiceProvider.GetRequiredService<ICardNumberProtection>().Protect(profile, card.Id, number!);
        Assert.Equal(card.NumberFingerprint, protectedAgain.Fingerprint);
        Assert.Equal(card.MatchingKeyId, protectedAgain.KeyId);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("unverified")]
    public async Task Invalid_or_ineligible_sessions_cannot_save(string state)
    {
        await using var host = NewHost();
        var (client, profile, account) = await Setup(host);
        using (client)
        {
            if (state == "anonymous") client.DefaultRequestHeaders.Remove("Cookie");
            await host.WithDatabaseAsync(async db =>
            {
                if (state == "expired") await db.BffSessions.ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken);
                if (state == "revoked") await db.BffSessions.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
                if (state == "unverified") await db.Users.Where(user => user.Id == account).ExecuteUpdateAsync(update => update.SetProperty(user => user.EmailConfirmed, false), TestContext.Current.CancellationToken);
                return true;
            });
            using var response = await client.PostAsJsonAsync(Route(profile), new { number = "bad" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Null(response.Headers.Location);
            Assert.Equal(1, await host.WithDatabaseAsync(db => db.PaymentMethods.CountAsync(TestContext.Current.CancellationToken)));
        }
    }

    private sealed class FailedProtection : ICardNumberProtection
    {
        public ProtectedCard Protect(Guid profileId, Guid methodId, CardNumber number) => throw new CardSavingUnavailableException();
    }

    private sealed class FailAfterCardInsert : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<PaymentMethodRecord>().Any(entry => entry.Entity.Kind == "credit_card"))
                throw new DbUpdateException("Synthetic failure after SQL insert.");
            return ValueTask.FromResult(result);
        }
    }

    private static AccountTestHost NewHost() => new()
    {
        Settings = new() { ["Cards:MatchingKey"] = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray()) }
    };

    private static string Route(Guid profile) => $"/api/budget-profiles/{profile}/payment-methods";

    private static async Task<(HttpClient Client, Guid Profile, Guid Account)> Setup(AccountTestHost host, string role = "owner", bool baseline = false)
    {
        if (baseline) await host.WithDatabaseAsync(async db => { await db.GetService<IMigrator>().MigrateAsync("20261106110747_AddProfileCreation", TestContext.Current.CancellationToken); return true; });
        else await host.MigrateAsync();
        var account = Guid.NewGuid();
        var owner = role == "owner" ? account : Guid.NewGuid();
        var profile = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            foreach (var id in new[] { owner, account }.Distinct())
                db.Users.Add(new AccountIdentityUser { Id = id, UserName = id + "@example.test", Email = id + "@example.test", EmailConfirmed = true });
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profile, OwnerAccountId = owner, Name = "Synthetic cards", MainCurrencyCode = "GBP" });
            if (!baseline) db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profile, Kind = "cash" });
            if (role is "editor" or "view_only") db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profile, AccountId = account, Role = role });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            if (baseline) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [PaymentMethods] ([Id], [ProfileId], [Kind]) VALUES ({Guid.NewGuid()}, {profile}, 'cash')", TestContext.Current.CancellationToken);
            return true;
        });
        using var scope = host.Services.CreateScope();
        var session = await scope.ServiceProvider.GetRequiredService<IBffSessionStore>().CreateAsync(
            new BffSession(account, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)), TestContext.Current.CancellationToken);
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost:7207"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "__Host-PF.Session=" + session);
        using var proof = await client.GetAsync("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await proof.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.RootElement.GetProperty("requestToken").GetString());
        return (client, profile, account);
    }
}
