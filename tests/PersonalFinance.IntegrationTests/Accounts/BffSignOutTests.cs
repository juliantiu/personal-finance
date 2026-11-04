using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class BffSignOutTests
{
    [Fact]
    public async Task Registration_still_starts_when_OpenIddict_is_disabled()
    {
        await using var host = new AccountTestHost { OpenIddictEnabled = false };
        await host.MigrateAsync();
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/accounts/register",
            new { email = "without-oidc@example.test", password = "abcdefghijkl", confirmPassword = "abcdefghijkl" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_requires_antiforgery_and_cannot_be_triggered_by_get()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = host.CreateClient();
        using var post = await client.PostAsync("/api/auth/sign-out", null, TestContext.Current.CancellationToken);
        using var get = await client.GetAsync("/api/auth/sign-out", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.False(post.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Missing_session_sign_out_is_repeatable_and_clears_the_cookie()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out");
            request.Headers.Add("X-CSRF-TOKEN", token!.RequestToken);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-PF.Session=", StringComparison.Ordinal));
            Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        }
    }

    private sealed record AntiforgeryToken(string RequestToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Sign_out_revokes_only_the_current_session_and_retains_it_on_failure(bool fail, bool expired)
    {
        var revokedSessions = new List<BffSession>();
        await using var host = new AccountTestHost
        {
            ConfigureAdditionalServices = services =>
            {
                services.RemoveAll<IBffGrantRevoker>();
                services.AddScoped<IBffGrantRevoker>(provider => new CapturingRevoker(fail,
                    provider.GetRequiredService<AccountIdentityDbContext>(), revokedSessions));
            }
        };
        await host.MigrateAsync();
        var accountId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = accountId, UserName = "signout@example.test", EmailConfirmed = true });
            db.Set<OpenIddictEntityFrameworkCoreAuthorization>().Add(new()
            {
                Subject = accountId.ToString(), Status = Statuses.Valid, Type = AuthorizationTypes.AdHoc
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        string first;
        string second;
        using (var scope = host.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
            var session = new BffSession(accountId, "access-first", "refresh-first", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddDays(7));
            first = await store.CreateAsync(session, TestContext.Current.CancellationToken);
            second = await store.CreateAsync(session with { AccessToken = "access-second" }, TestContext.Current.CancellationToken);
        }
        if (expired)
        {
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(first)));
            await host.WithDatabaseAsync(db => db.BffSessions.Where(value => value.Id == hash)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken));
        }
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        using (var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out"))
        {
            invalidRequest.Headers.Add("Cookie", "__Host-PF.Session=" + first);
            using var invalidResponse = await client.SendAsync(invalidRequest, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
            Assert.Empty(revokedSessions);
            Assert.Equal(2, await host.WithDatabaseAsync(db => db.BffSessions.CountAsync(TestContext.Current.CancellationToken)));
        }
        var token = await client.GetFromJsonAsync<AntiforgeryToken>("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out");
        request.Headers.Add("X-CSRF-TOKEN", token!.RequestToken);
        request.Headers.Add("Cookie", "__Host-PF.Session=" + first);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("access-first", Assert.Single(revokedSessions).AccessToken);
        using var verification = host.Services.CreateScope();
        var sessions = verification.ServiceProvider.GetRequiredService<IBffSessionStore>();
        Assert.Equal(fail && !expired, await sessions.FindAsync(first, TestContext.Current.CancellationToken) is not null);
        Assert.NotNull(await sessions.FindAsync(second, TestContext.Current.CancellationToken));
        Assert.Equal(fail ? 2 : 1, await host.WithDatabaseAsync(db => db.BffSessions.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(fail ? Statuses.Valid : Statuses.Revoked, await host.WithDatabaseAsync(db =>
            db.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(value => value.Subject == accountId.ToString())
                .Select(value => value.Status).SingleAsync(TestContext.Current.CancellationToken)));
        if (fail)
        {
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.DoesNotContain("sensitive", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    private sealed class CapturingRevoker(bool fail, AccountIdentityDbContext database, List<BffSession> sessions) : IBffGrantRevoker
    {
        public async Task RevokeAsync(BffSession session, CancellationToken cancellationToken)
        {
            sessions.Add(session);
            await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().Where(value => value.Subject == session.AccountId.ToString())
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.Status, Statuses.Revoked), cancellationToken);
            if (fail) throw new InvalidOperationException("sensitive provider details");
        }
    }
}
