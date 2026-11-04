namespace PersonalFinance.Application.Accounts;

// Comment ID: CLASS-027. Defines atomic persistence and grant revocation for one browser session.
public interface IBffSessionTermination
{
    // Comment ID: METHOD-043. Revokes the session's grant and deletes its persisted payload together.
    Task TerminateAsync(string sessionId, CancellationToken cancellationToken);
}

// Comment ID: CLASS-028. Defines the authorization-server boundary for revoking a stored session's token chain.
public interface IBffGrantRevoker
{
    // Comment ID: METHOD-044. Identifies and revokes only the authorization attached to the supplied session.
    Task RevokeAsync(BffSession session, CancellationToken cancellationToken);
}

// Comment ID: CLASS-029. Serializes sign-out with refresh for the same opaque browser session.
public sealed class SignOutBffSession(IBffSessionTermination termination)
{
    // Comment ID: METHOD-045. Ends one session after any in-flight refresh has completed.
    public async Task ExecuteAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var gate = BffSessionSynchronization.GetGate(sessionId);
        await gate.WaitAsync(cancellationToken);
        try { await termination.TerminateAsync(sessionId, cancellationToken); }
        finally { gate.Release(); }
    }
}
