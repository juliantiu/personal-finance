using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Accounts;

// Comment ID: CLASS-003. Persists opaque BFF session identifiers and Data Protection-encrypted token data in the identity database.
public sealed class ProtectedBffSessionStore(
    AccountIdentityDbContext database,
    IDataProtectionProvider protectionProvider,
    TimeProvider timeProvider) : IBffSessionStore
{
    private readonly IDataProtector protector = protectionProvider.CreateProtector("PersonalFinance.BffSession.v1");

    // Comment ID: METHOD-004. Protects and persists the session payload, returning a random identifier whose hash is stored in the database.
    public async Task<string> CreateAsync(BffSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(session.AccessToken))
            throw new ArgumentException("An access token is required.", nameof(session));
        if (session.AccessTokenExpiresAt <= timeProvider.GetUtcNow() || session.SessionExpiresAt <= timeProvider.GetUtcNow())
            throw new ArgumentException("Session and access-token expirations must be in the future.", nameof(session));

        var sessionId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var id = HashSessionId(sessionId);
        var now = timeProvider.GetUtcNow();
        var payload = JsonSerializer.SerializeToUtf8Bytes(session);
        database.BffSessions.Add(new BffSessionEntity
        {
            Id = id,
            AccountId = session.AccountId,
            ProtectedPayload = protector.Protect(payload),
            CreatedAt = now,
            ExpiresAt = session.SessionExpiresAt
        });
        await database.SaveChangesAsync(cancellationToken);
        return sessionId;
    }

    // Comment ID: METHOD-005. Looks up an unexpired session by hashed identifier and decrypts its payload when protection succeeds.
    public async Task<BffSession?> FindAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var id = HashSessionId(sessionId);
        var row = await database.BffSessions.AsNoTracking()
            .SingleOrDefaultAsync(session => session.Id == id, cancellationToken);
        if (row is null || row.ExpiresAt <= timeProvider.GetUtcNow()) return null;

        try
        {
            var json = protector.Unprotect(row.ProtectedPayload);
            return JsonSerializer.Deserialize<BffSession>(json);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // Comment ID: METHOD-006. Deletes the row associated with the supplied opaque session identifier.
    public async Task RevokeAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var id = HashSessionId(sessionId);
        await database.BffSessions.Where(session => session.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    // Comment ID: METHOD-007. Hashes a session identifier so the raw lookup secret is not stored in the database.
    private static string HashSessionId(string sessionId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)));
}
