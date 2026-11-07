using PersonalFinance.Application.Accounts;
using PersonalFinance.Application.Profiles;

namespace PersonalFinance.Api.Profiles;

public sealed class BffCurrentAccount(
    IHttpContextAccessor httpContext,
    IBffSessionStore sessions,
    IAccountSessionEligibility eligibility,
    TimeProvider timeProvider) : ICurrentAccount
{
    public async Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken)
    {
        var sessionId = httpContext.HttpContext?.Request.Cookies["__Host-PF.Session"];
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var session = await sessions.FindAsync(sessionId, cancellationToken);
        if (session is null || session.SessionExpiresAt <= timeProvider.GetUtcNow()) return null;
        return await eligibility.IsActiveAsync(session.AccountId, cancellationToken) ? session.AccountId : null;
    }
}
