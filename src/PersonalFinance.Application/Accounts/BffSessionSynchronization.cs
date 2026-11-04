using System.Collections.Concurrent;

namespace PersonalFinance.Application.Accounts;

// Comment ID: CLASS-030. Shares per-session synchronization between token refresh and browser sign-out.
internal static class BffSessionSynchronization
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    // Comment ID: METHOD-046. Returns the common gate for lifecycle operations on one opaque session.
    internal static SemaphoreSlim GetGate(string sessionId) => Gates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
}
