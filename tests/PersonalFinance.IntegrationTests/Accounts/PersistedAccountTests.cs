using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Contracts.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class PersistedAccountTests
{
    private static Task<HttpResponseMessage> Post<T>(HttpClient client, string path, T body) =>
        client.PostAsJsonAsync(path, body, TestContext.Current.CancellationToken);

    private const string Password = "abcdefghijkl";
    private static Task<HttpResponseMessage> Register(HttpClient client, string email) =>
        Post(client, "/api/accounts/register", new RegisterAccountRequest(email, Password, Password));

    [Fact]
    public async Task Registration_persists_a_hashed_unverified_account_and_confirmation_verifies_only_that_account()
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient();
        await host.MigrateAsync();
        Assert.Equal(HttpStatusCode.Accepted, (await Register(client, "first@example.test")).StatusCode);
        await Register(client, "second@example.test");
        var user = await host.WithDatabaseAsync(db => db.Users.SingleAsync(u => u.Email == "first@example.test"));
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.False(user.EmailConfirmed);
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<AccountIdentityUser>().VerifyHashedPassword(user, user.PasswordHash!, Password));
        var request = host.Email.ConfirmationFor("first@example.test");
        Assert.Equal(user.Id, request.AccountId);
        var response = await Post(client, "/api/accounts/confirm-email", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True((await response.Content.ReadFromJsonAsync<ConfirmEmailResponse>(TestContext.Current.CancellationToken))!.Confirmed);
        Assert.True(await host.WithDatabaseAsync(db => db.Users.AnyAsync(u => u.Id == user.Id && u.EmailConfirmed)));
        Assert.False(await host.WithDatabaseAsync(db => db.Users.AnyAsync(u => u.Email == "second@example.test" && u.EmailConfirmed)));
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/accounts/confirm-email", request)).StatusCode);
    }

    [Theory]
    [InlineData("", Password, Password)]
    [InlineData("invalid", Password, Password)]
    [InlineData("invalid@example.test", "short", "short")]
    [InlineData("invalid@example.test", Password, "different-password")]
    public async Task Invalid_input_returns_field_errors_without_persistence_or_email(string email, string password, string confirmation)
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient();
        await host.MigrateAsync();
        var response = await Post(client, "/api/accounts/register", new RegisterAccountRequest(email, password, confirmation));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("errors", body);
        Assert.DoesNotContain(password, body);
        Assert.Equal(0, await host.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.Empty(host.Email.Messages);
    }

    [Fact]
    public async Task Concurrent_and_repeated_normalized_emails_create_one_account_with_generic_acknowledgements()
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient();
        await host.MigrateAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Register(client,
            i % 2 == 0 ? "duplicate@example.test" : "DUPLICATE@EXAMPLE.TEST")));
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal("Request accepted.", (await response.Content.ReadFromJsonAsync<AccountRequestAcknowledgement>(TestContext.Current.CancellationToken))!.Message);
        }
        Assert.Equal(HttpStatusCode.Accepted, (await Register(client, "Duplicate@example.test")).StatusCode);
        Assert.Equal(1, await host.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.Single(host.Email.Messages);
        // Dot/plus variants must remain separate addresses.
        await Register(client, "du.plicate@example.test");
        await Register(client, "duplicate+tag@example.test");
        Assert.Equal(3, await host.WithDatabaseAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task Applied_sql_migration_enforces_normalized_email_uniqueness_independent_of_username()
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient();
        await host.MigrateAsync();
        await Register(client, "constraint@example.test");
        await Assert.ThrowsAsync<DbUpdateException>(() => host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = Guid.NewGuid(), Email = "CONSTRAINT@example.test",
                NormalizedEmail = "CONSTRAINT@EXAMPLE.TEST", UserName = "different", NormalizedUserName = "DIFFERENT" });
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(1, await host.WithDatabaseAsync(db => db.Users.CountAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tampered_wrong_account_and_expired_tokens_fail_even_after_verification(bool alreadyVerified)
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient();
        await host.MigrateAsync();
        await Register(client, "token@example.test");
        await Register(client, "other@example.test");
        var valid = host.Email.ConfirmationFor("token@example.test");
        var other = host.Email.ConfirmationFor("other@example.test");
        if (alreadyVerified)
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/accounts/confirm-email", valid)).StatusCode);

        // Identity's actual protected token format, with an issuance timestamp 25 hours ago.
        // This tests the configured 24-hour lifetime without sleeps or weakening production options.
        var user = await host.WithDatabaseAsync(db => db.Users.SingleAsync(u => u.Id == valid.AccountId));
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(DateTimeOffset.UtcNow.AddHours(-25).UtcTicks);
            writer.Write(user.Id.ToString());
            writer.Write("EmailConfirmation");
            writer.Write(user.SecurityStamp!);
        }
        var protector = host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("DataProtectorTokenProvider");
        var expired = Convert.ToBase64String(protector.Protect(stream.ToArray()));
        foreach (var invalid in new[] { valid with { Token = valid.Token + "tampered" },
                     valid with { AccountId = other.AccountId }, valid with { Token = expired } })
        {
            var response = await Post(client, "/api/accounts/confirm-email", invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain(invalid.Token, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
        Assert.Equal(alreadyVerified, await host.WithDatabaseAsync(db => db.Users.Where(u => u.Id == user.Id).Select(u => u.EmailConfirmed).SingleAsync()));
        Assert.False(await host.WithDatabaseAsync(db => db.Users.Where(u => u.Id == other.AccountId).Select(u => u.EmailConfirmed).SingleAsync()));
        // Positive control: identical protected payload with a still-valid timestamp must work.
        stream.Position = 0;
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            writer.Write(DateTimeOffset.UtcNow.AddHours(-23).UtcTicks);
        var recent = valid with { Token = Convert.ToBase64String(protector.Protect(stream.ToArray())) };
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/accounts/confirm-email", recent)).StatusCode);
    }

    [Fact]
    public async Task Delivery_failure_retains_account_and_resend_recovers_without_revealing_recipient_state()
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient();
        await host.MigrateAsync();
        host.Email.FailDelivery = true;
        Assert.Equal(HttpStatusCode.Accepted, (await Register(client, "retry@example.test")).StatusCode);
        Assert.Equal(1, await host.WithDatabaseAsync(db => db.Users.CountAsync(u => !u.EmailConfirmed)));
        Assert.Empty(host.Email.Messages);
        var failedResend = await Post(client, "/api/accounts/resend-confirmation", new ResendConfirmationRequest("retry@example.test"));
        Assert.Equal(HttpStatusCode.Accepted, failedResend.StatusCode);
        host.Email.FailDelivery = false;
        var resend = await Post(client, "/api/accounts/resend-confirmation", new ResendConfirmationRequest("retry@example.test"));
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Single(host.Email.Messages);
        await Post(client, "/api/accounts/confirm-email", host.Email.ConfirmationFor("retry@example.test"));
        foreach (var email in new[] { "unknown@example.test", "retry@example.test" })
        {
            var response = await Post(client, "/api/accounts/resend-confirmation", new ResendConfirmationRequest(email));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal(await resend.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        Assert.Single(host.Email.Messages);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Missing_configuration_keeps_all_account_routes_unavailable(bool missingDatabase, bool missingEmail)
    {
        await using var host = new AccountTestHost { MissingDatabase = missingDatabase, MissingEmail = missingEmail };
        using var client = host.CreateClient();
        foreach (var path in new[] { "register", "confirm-email", "resend-confirmation" })
            Assert.Equal(HttpStatusCode.NotFound, (await Post(client, "/api/accounts/" + path, new { })).StatusCode);
    }
}
