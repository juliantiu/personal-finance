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

    // Comment ID: METHOD-003. Revokes a session so its identifier can no longer resolve to session data.
    Task RevokeAsync(string sessionId, CancellationToken cancellationToken);
}
