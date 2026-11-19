using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileAccessRemovalStore(AccountIdentityDbContext database, ILookupNormalizer normalizer)
    : IProfileAccessRemovalStore
{
    public async Task<RemoveProfileAccessResult> RemoveAsync(Guid accountId, Guid profileId, string email,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var ownerAccountId = await database.BudgetProfiles
            .Where(profile => profile.Id == profileId)
            .Select(profile => (Guid?)profile.OwnerAccountId)
            .SingleOrDefaultAsync(cancellationToken);
        if (ownerAccountId != accountId) return RemoveProfileAccessResult.NotOwner;

        var normalizedEmail = normalizer.NormalizeEmail(email.Trim());
        if (string.IsNullOrWhiteSpace(normalizedEmail)) return RemoveProfileAccessResult.NoActiveAccess;

        var recipient = await database.Users.AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (recipient is null || recipient == ownerAccountId) return RemoveProfileAccessResult.NoActiveAccess;

        var access = await database.ProfileAccess.SingleOrDefaultAsync(
            grant => grant.ProfileId == profileId && grant.AccountId == recipient, cancellationToken);
        if (access is null) return RemoveProfileAccessResult.NoActiveAccess;

        database.ProfileAccess.Remove(access);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RemoveProfileAccessResult.Removed;
    }
}
