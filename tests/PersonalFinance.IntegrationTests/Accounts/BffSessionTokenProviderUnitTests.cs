using System.Net;
using System.Net.Http.Headers;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Application.Accounts;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class BffSessionTokenProviderUnitTests
{
    [Fact]
    public async Task Current_access_token_is_reused_without_refresh()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStore(CreateSession(now, "access-current", "refresh-current"));
        var refresher = new FakeRefresher(null);
        var provider = CreateProvider(store, refresher, new FakeEligibility(true), now);

        var token = await provider.GetAccessTokenAsync("opaque-session", CancellationToken.None);

        Assert.Equal("access-current", token);
        Assert.Equal(0, refresher.Calls);
    }

    [Fact]
    public async Task Expired_access_token_rotates_and_persists_tokens_without_extending_absolute_session_expiry()
    {
        var now = DateTimeOffset.UtcNow;
        var session = CreateSession(now.AddMinutes(-16), "access-expired", "refresh-current") with
        {
            SessionExpiresAt = now.AddDays(6)
        };
        var store = new InMemorySessionStore(session);
        var refresher = new FakeRefresher(new BffTokenSet("access-rotated", now.AddMinutes(15), "refresh-rotated"));
        var provider = CreateProvider(store, refresher, new FakeEligibility(true), now);

        var token = await provider.GetAccessTokenAsync("opaque-session", CancellationToken.None);

        Assert.Equal("access-rotated", token);
        Assert.Equal("refresh-rotated", store.Session?.RefreshToken);
        Assert.Equal(session.SessionExpiresAt, store.Session?.SessionExpiresAt);
    }

    [Fact]
    public async Task Inactive_account_invalidates_session_without_attempting_refresh()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStore(CreateSession(now.AddMinutes(-16), "expired", "refresh-current"));
        var refresher = new FakeRefresher(new BffTokenSet("access-rotated", now.AddMinutes(15), "refresh-rotated"));
        var provider = CreateProvider(store, refresher, new FakeEligibility(false), now);

        var token = await provider.GetAccessTokenAsync("opaque-session", CancellationToken.None);

        Assert.Null(token);
        Assert.Null(store.Session);
        Assert.Equal(0, refresher.Calls);
    }

    [Fact]
    public async Task Rejected_refresh_invalidates_session()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStore(CreateSession(now.AddMinutes(-16), "expired", "refresh-replayed"));
        var refresher = new FakeRefresher(null);
        var provider = CreateProvider(store, refresher, new FakeEligibility(true), now);

        var token = await provider.GetAccessTokenAsync("opaque-session", CancellationToken.None);

        Assert.Null(token);
        Assert.Null(store.Session);
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task Resource_client_attaches_bearer_token_only_to_relative_server_requests()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemorySessionStore(CreateSession(now, "server-only-access", "refresh-current"));
        var provider = CreateProvider(store, new FakeRefresher(null), new FakeEligibility(true), now);
        var handler = new CapturingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://synthetic-resource.example.test/") };
        var client = new BffProtectedResourceClient(http, provider);

        using var response = await client.SendAsync("opaque-session",
            new HttpRequestMessage(HttpMethod.Get, "protected"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response?.StatusCode);
        Assert.Equal("Bearer", handler.Authorization?.Scheme);
        Assert.Equal("server-only-access", handler.Authorization?.Parameter);
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync("opaque-session",
            new HttpRequestMessage(HttpMethod.Get, "https://attacker.example.test/resource"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => client.SendAsync("opaque-session",
            new HttpRequestMessage(HttpMethod.Get, "//attacker.example.test/resource"), CancellationToken.None));
    }

    private static BffSessionTokenProvider CreateProvider(
        InMemorySessionStore store,
        IBffTokenRefresher refresher,
        IAccountSessionEligibility eligibility,
        DateTimeOffset now) => new(store, refresher, eligibility, new TestClock(now));

    private static BffSession CreateSession(DateTimeOffset now, string accessToken, string? refreshToken) =>
        new(Guid.NewGuid(), accessToken, refreshToken, now.AddMinutes(15), now.AddDays(7));

    private sealed class InMemorySessionStore(BffSession? session) : IBffSessionStore
    {
        public BffSession? Session { get; private set; } = session;

        public Task<string> CreateAsync(BffSession value, CancellationToken cancellationToken) => Task.FromResult("opaque-session");
        public Task<BffSession?> FindAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(Session);
        public Task UpdateAsync(string sessionId, BffSession value, CancellationToken cancellationToken)
        {
            Session = value;
            return Task.CompletedTask;
        }
        public Task RevokeAsync(string sessionId, CancellationToken cancellationToken)
        {
            Session = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefresher(BffTokenSet? result) : IBffTokenRefresher
    {
        public int Calls { get; private set; }
        public Task<BffTokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeEligibility(bool isActive) : IAccountSessionEligibility
    {
        public Task<bool> IsActiveAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(isActive);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
