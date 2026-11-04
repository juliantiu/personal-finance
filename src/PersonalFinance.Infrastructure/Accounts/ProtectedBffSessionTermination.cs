using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Accounts;

// Comment ID: CLASS-036. Atomically revokes one protected BFF session's grant and removes its database payload.
public sealed class ProtectedBffSessionTermination(
    AccountIdentityDbContext database,
    IDataProtectionProvider protectionProvider,
    IBffGrantRevoker grantRevoker) : IBffSessionTermination
{
    private readonly IDataProtector protector = protectionProvider.CreateProtector("PersonalFinance.BffSession.v1");

    // Comment ID: METHOD-047. Revokes the stored grant and deletes even an expired session in one database transaction.
    public async Task TerminateAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)));
        var row = await database.BffSessions.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (row is not null)
        {
            var session = JsonSerializer.Deserialize<BffSession>(protector.Unprotect(row.ProtectedPayload))
                ?? throw new InvalidOperationException("The session could not be resolved for sign-out.");
            if (session.AccountId != row.AccountId)
                throw new InvalidOperationException("The session could not be resolved for sign-out.");
            await grantRevoker.RevokeAsync(session, cancellationToken);
            await database.BffSessions.Where(value => value.Id == id).ExecuteDeleteAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
