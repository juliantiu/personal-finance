using PersonalFinance.Application.Accounts;
using PersonalFinance.Application.Profiles;

namespace PersonalFinance.Api.Profiles;

// Comment ID: CLASS-044. Resolves finance request identity from the opaque BFF cookie rather than the temporary login ticket.
public sealed class BffCurrentAccount(
    IHttpContextAccessor httpContext,
    IBffSessionStore sessions,
    IAccountSessionEligibility eligibility,
    TimeProvider timeProvider) : ICurrentAccount
{
    // Comment ID: METHOD-063. Resolves an unexpired BFF session and rechecks that its account still exists with verified email.
    public async Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken)
    {
        var sessionId = httpContext.HttpContext?.Request.Cookies["__Host-PF.Session"];
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var session = await sessions.FindAsync(sessionId, cancellationToken);
        if (session is null || session.SessionExpiresAt <= timeProvider.GetUtcNow()) return null;
        return await eligibility.IsActiveAsync(session.AccountId, cancellationToken) ? session.AccountId : null;
    }
}
