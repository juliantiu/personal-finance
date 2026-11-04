namespace PersonalFinance.Application.Accounts;

// Comment ID: CLASS-022. Coordinates account checks, refresh-token rotation, and persistence for BFF resource requests.
public sealed class BffSessionTokenProvider(
    IBffSessionStore sessions,
    IBffTokenRefresher refresher,
    IAccountSessionEligibility accountEligibility,
    TimeProvider timeProvider) : IBffSessionTokenProvider
{
    // Comment ID: METHOD-038. Returns an active session token, rotating and persisting credentials when the access token expires.
    public async Task<string?> GetAccessTokenAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;

        var gate = BffSessionSynchronization.GetGate(sessionId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var session = await sessions.FindAsync(sessionId, cancellationToken);
            if (session is null) return null;
            if (!await accountEligibility.IsActiveAsync(session.AccountId, cancellationToken))
            {
                await sessions.RevokeAsync(sessionId, cancellationToken);
                return null;
            }

            var now = timeProvider.GetUtcNow();
            if (session.SessionExpiresAt <= now)
            {
                await sessions.RevokeAsync(sessionId, cancellationToken);
                return null;
            }

            if (session.AccessTokenExpiresAt > now) return session.AccessToken;
            if (string.IsNullOrWhiteSpace(session.RefreshToken))
            {
                await sessions.RevokeAsync(sessionId, cancellationToken);
                return null;
            }

            var tokenSet = await refresher.RefreshAsync(session.RefreshToken, cancellationToken);
            if (tokenSet is null || string.IsNullOrWhiteSpace(tokenSet.AccessToken) ||
                string.IsNullOrWhiteSpace(tokenSet.RefreshToken) || tokenSet.AccessTokenExpiresAt <= now)
            {
                await sessions.RevokeAsync(sessionId, cancellationToken);
                return null;
            }

            var rotated = session with
            {
                AccessToken = tokenSet.AccessToken,
                RefreshToken = tokenSet.RefreshToken,
                AccessTokenExpiresAt = tokenSet.AccessTokenExpiresAt
            };
            await sessions.UpdateAsync(sessionId, rotated, cancellationToken);
            var persisted = await sessions.FindAsync(sessionId, cancellationToken);
            return persisted is not null && persisted.SessionExpiresAt == session.SessionExpiresAt &&
                persisted.AccessToken == rotated.AccessToken && persisted.RefreshToken == rotated.RefreshToken
                ? persisted.AccessToken
                : null;
        }
        finally
        {
            gate.Release();
        }
    }
}
