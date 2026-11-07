namespace PersonalFinance.Application.Accounts;

// Comment ID: CLASS-001. Describes the authenticated account and server-held token lifetimes for one BFF session.
public sealed record BffSession(Guid AccountId, string AccessToken, string? RefreshToken,
    DateTimeOffset AccessTokenExpiresAt, DateTimeOffset SessionExpiresAt);

// Comment ID: CLASS-002. Defines persistence operations the application uses to create, resolve, and revoke BFF sessions.
public interface IBffSessionStore
{
    // Comment ID: METHOD-001. Creates a session and returns its opaque identifier for later server-side lookup.
    Task<string> CreateAsync(BffSession session, CancellationToken cancellationToken);

    // Comment ID: METHOD-002. Resolves an active session identifier to its protected server-side session data.
    Task<BffSession?> FindAsync(string sessionId, CancellationToken cancellationToken);

    // Comment ID: METHOD-033. Replaces the protected token payload for an existing live session after token refresh.
    Task UpdateAsync(string sessionId, BffSession session, CancellationToken cancellationToken);

    // Comment ID: METHOD-003. Revokes a session so its identifier can no longer resolve to session data.
    Task RevokeAsync(string sessionId, CancellationToken cancellationToken);
}

// Comment ID: CLASS-018. Resolves a valid server-held access token for a browser's opaque BFF session.
public interface IBffSessionTokenProvider
{
    // Comment ID: METHOD-034. Returns a current access token, refreshing and persisting the session when required.
    Task<string?> GetAccessTokenAsync(string sessionId, CancellationToken cancellationToken);
}

// Comment ID: CLASS-019. Carries replacement access and refresh tokens returned by the authorization server.
public sealed record BffTokenSet(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken);

// Comment ID: CLASS-020. Exchanges an expired session refresh token for a newly rotated token set.
public interface IBffTokenRefresher
{
    // Comment ID: METHOD-035. Requests a rotated token set from the configured OpenIddict issuer.
    Task<BffTokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface IAccountSessionEligibility
{
    Task<bool> IsActiveAsync(Guid accountId, CancellationToken cancellationToken);
}
