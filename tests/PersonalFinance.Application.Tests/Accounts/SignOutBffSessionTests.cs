using PersonalFinance.Application.Accounts;
using Xunit;

namespace PersonalFinance.Application.Tests.Accounts;

public sealed class SignOutBffSessionTests
{
    [Fact]
    public async Task Sign_out_waits_for_inflight_refresh_and_prevents_subsequent_token_use()
    {
        var store = new SessionStore();
        var refresher = new BlockingRefresher();
        var tokens = new BffSessionTokenProvider(store, refresher, new EligibleAccount(), TimeProvider.System);
        var signOut = new SignOutBffSession(store);
        var id = Guid.NewGuid().ToString("N");
        var refreshing = tokens.GetAccessTokenAsync(id, TestContext.Current.CancellationToken);
        await refresher.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        var ending = signOut.ExecuteAsync(id, TestContext.Current.CancellationToken);
        Assert.False(store.Terminated);
        Assert.False(ending.IsCompleted);
        refresher.Release.SetResult();
        await refreshing;
        await ending;

        Assert.True(store.Terminated);
        Assert.Null(store.Session);
        Assert.Null(await tokens.GetAccessTokenAsync(id, TestContext.Current.CancellationToken));
        Assert.Equal(1, refresher.Calls);
    }

    [Fact]
    public async Task Failed_termination_is_retryable_and_releases_the_session_gate()
    {
        var store = new SessionStore { Fail = true };
        var signOut = new SignOutBffSession(store);
        var id = Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() => signOut.ExecuteAsync(id, TestContext.Current.CancellationToken));
        Assert.NotNull(store.Session);
        store.Fail = false;
        await signOut.ExecuteAsync(id, TestContext.Current.CancellationToken);
        Assert.True(store.Terminated);
    }

    private sealed class SessionStore : IBffSessionStore, IBffSessionTermination
    {
        public BffSession? Session { get; private set; } = new(Guid.NewGuid(), "expired", "refresh", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
        public bool Terminated { get; private set; }
        public bool Fail { get; set; }
        public Task<string> CreateAsync(BffSession session, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BffSession?> FindAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(Session);
        public Task UpdateAsync(string sessionId, BffSession session, CancellationToken cancellationToken)
        {
            Assert.False(Terminated);
            Session = session;
            return Task.CompletedTask;
        }
        public Task RevokeAsync(string sessionId, CancellationToken cancellationToken)
        {
            Session = null;
            return Task.CompletedTask;
        }
        public Task TerminateAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (Fail) throw new InvalidOperationException("revocation failed");
            Session = null;
            Terminated = true;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingRefresher : IBffTokenRefresher
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public async Task<BffTokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            Calls++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new BffTokenSet("rotated-access", DateTimeOffset.UtcNow.AddMinutes(15), "rotated-refresh");
        }
    }

    private sealed class EligibleAccount : IAccountSessionEligibility
    {
        public Task<bool> IsActiveAsync(Guid accountId, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
