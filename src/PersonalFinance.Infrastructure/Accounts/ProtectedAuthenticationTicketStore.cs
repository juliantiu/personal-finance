using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Accounts;

// Comment ID: CLASS-012. Keeps temporary Identity authentication tickets in protected server storage while cookies carry opaque handles.
public sealed class ProtectedAuthenticationTicketStore(
    IServiceScopeFactory scopeFactory,
    IDataProtectionProvider protectionProvider,
    TimeProvider timeProvider) : ITicketStore
{
    private readonly IDataProtector protector = protectionProvider.CreateProtector("PersonalFinance.LoginTicket.v1");

    // Comment ID: METHOD-018. Protects a ticket in the identity database and returns a random opaque cookie handle.
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var handle = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var row = new AuthenticationTicketEntity
        {
            Id = Hash(handle),
            ProtectedPayload = protector.Protect(TicketSerializer.Default.Serialize(ticket)),
            ExpiresAt = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddMinutes(10)
        };
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>();
        database.AuthenticationTickets.Add(row);
        await database.SaveChangesAsync();
        return handle;
    }

    // Comment ID: METHOD-019. Resolves and decrypts a live ticket referenced by an opaque cookie handle.
    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>();
        var row = await database.AuthenticationTickets.AsNoTracking().SingleOrDefaultAsync(ticket => ticket.Id == Hash(key));
        if (row is null || row.ExpiresAt <= timeProvider.GetUtcNow()) return null;
        try { return TicketSerializer.Default.Deserialize(protector.Unprotect(row.ProtectedPayload)); }
        catch (CryptographicException) { return null; }
    }

    // Comment ID: METHOD-020. Replaces the protected ticket payload and expiry for an existing opaque handle.
    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>();
        var row = await database.AuthenticationTickets.SingleOrDefaultAsync(value => value.Id == Hash(key));
        if (row is null) return;
        row.ProtectedPayload = protector.Protect(TicketSerializer.Default.Serialize(ticket));
        row.ExpiresAt = ticket.Properties.ExpiresUtc ?? timeProvider.GetUtcNow().AddMinutes(10);
        await database.SaveChangesAsync();
    }

    // Comment ID: METHOD-021. Deletes a temporary authentication ticket when its browser cookie is cleared.
    public async Task RemoveAsync(string key)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>();
        await database.AuthenticationTickets.Where(value => value.Id == Hash(key)).ExecuteDeleteAsync();
    }

    // Comment ID: METHOD-023. Hashes the raw cookie handle so only non-reversible lookup data is persisted.
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
