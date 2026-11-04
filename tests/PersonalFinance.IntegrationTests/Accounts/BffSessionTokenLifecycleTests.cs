using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class BffSessionTokenLifecycleTests
{
    [Fact]
    public async Task Valid_access_token_is_reused_without_refresh()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var refresher = new FakeTokenRefresher();
        await using var host = CreateHost(clock, refresher);
        await host.MigrateAsync();
        var accountId = await AddUser(host, confirmed: true);
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var sessionId = await store.CreateAsync(CreateSession(accountId, clock.GetUtcNow(), "access-current", "refresh-current"),
            TestContext.Current.CancellationToken);

        var provider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
        var token = await provider.GetAccessTokenAsync(sessionId, TestContext.Current.CancellationToken);

        Assert.Equal("access-current", token);
        Assert.Equal(0, refresher.Calls);
    }

    [Fact]
    public async Task Expired_access_token_is_refreshed_rotated_and_persisted_protected_without_extending_session()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var refresher = new FakeTokenRefresher(new BffTokenSet("access-rotated", clock.GetUtcNow().AddMinutes(31), "refresh-rotated"));
        await using var host = CreateHost(clock, refresher);
        await host.MigrateAsync();
        var accountId = await AddUser(host, confirmed: true);
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var session = CreateSession(accountId, clock.GetUtcNow(), "access-expired", "refresh-current");
        var sessionId = await store.CreateAsync(session, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(16));

        var provider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
        var token = await provider.GetAccessTokenAsync(sessionId, TestContext.Current.CancellationToken);
        var persisted = await store.FindAsync(sessionId, TestContext.Current.CancellationToken);
        var row = await host.WithDatabaseAsync(db => db.BffSessions.AsNoTracking().SingleAsync());
        var protectedPayload = System.Text.Encoding.UTF8.GetString(row.ProtectedPayload);

        Assert.Equal("access-rotated", token);
        Assert.Equal(1, refresher.Calls);
        Assert.Equal("refresh-current", refresher.LastRefreshToken);
        Assert.NotNull(persisted);
        Assert.Equal("access-rotated", persisted.AccessToken);
        Assert.Equal("refresh-rotated", persisted.RefreshToken);
        Assert.Equal(session.SessionExpiresAt, persisted.SessionExpiresAt);
        Assert.DoesNotContain("access-rotated", protectedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh-rotated", protectedPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unconfirmed_account_cannot_use_or_refresh_a_session()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var refresher = new FakeTokenRefresher(new BffTokenSet("access-rotated", clock.GetUtcNow().AddMinutes(15), "refresh-rotated"));
        await using var host = CreateHost(clock, refresher);
        await host.MigrateAsync();
        var accountId = await AddUser(host, confirmed: false);
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var sessionId = await store.CreateAsync(CreateSession(accountId, clock.GetUtcNow(), "expired", "refresh-current"),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(16));

        var provider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
        var token = await provider.GetAccessTokenAsync(sessionId, TestContext.Current.CancellationToken);

        Assert.Null(token);
        Assert.Equal(0, refresher.Calls);
        Assert.Null(await store.FindAsync(sessionId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failed_or_replayed_refresh_invalidates_the_session()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var refresher = new FakeTokenRefresher();
        await using var host = CreateHost(clock, refresher);
        await host.MigrateAsync();
        var accountId = await AddUser(host, confirmed: true);
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var sessionId = await store.CreateAsync(CreateSession(accountId, clock.GetUtcNow(), "expired", "refresh-replayed"),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(16));

        var provider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
        var token = await provider.GetAccessTokenAsync(sessionId, TestContext.Current.CancellationToken);

        Assert.Null(token);
        Assert.Equal(1, refresher.Calls);
        Assert.Null(await store.FindAsync(sessionId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Protected_resource_request_receives_only_the_server_held_bearer_token()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var refresher = new FakeTokenRefresher();
        await using var host = CreateHost(clock, refresher);
        await host.MigrateAsync();
        var accountId = await AddUser(host, confirmed: true);
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var sessionId = await store.CreateAsync(CreateSession(accountId, clock.GetUtcNow(), "access-to-resource", "refresh-current"),
            TestContext.Current.CancellationToken);
        var tokenProvider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
        var handler = new CapturingResourceHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://synthetic-resource.example.test/") };
        var resourceClient = new BffProtectedResourceClient(http, tokenProvider);

        using var response = await resourceClient.SendAsync(sessionId,
            new HttpRequestMessage(HttpMethod.Get, "synthetic/protected"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response?.StatusCode);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("access-to-resource", handler.Authorization?.Parameter);
    }

    private static AccountTestHost CreateHost(TestClock clock, FakeTokenRefresher refresher)
    {
        var host = new AccountTestHost
        {
            ConfigureAdditionalServices = services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.RemoveAll<IBffTokenRefresher>();
                services.AddSingleton<IBffTokenRefresher>(refresher);
            }
        };
        return host;
    }

    private static async Task<Guid> AddUser(AccountTestHost host, bool confirmed)
    {
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
        var email = $"lifecycle-{Guid.NewGuid():N}@example.test";
        var user = new AccountIdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = confirmed
        };
        Assert.True((await manager.CreateAsync(user, "abcdefghijkl")).Succeeded);
        return user.Id;
    }

    private static BffSession CreateSession(Guid accountId, DateTimeOffset now, string accessToken, string? refreshToken)
    {
        return new BffSession(accountId, accessToken, refreshToken, now.AddMinutes(15), now.AddDays(7));
    }

    private sealed class FakeTokenRefresher(BffTokenSet? result = null) : IBffTokenRefresher
    {
        public int Calls { get; private set; }
        public string? LastRefreshToken { get; private set; }

        public Task<BffTokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            LastRefreshToken = refreshToken;
            return Task.FromResult(result);
        }
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan duration) => current += duration;
    }

    private sealed class CapturingResourceHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
