using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileAccessGrantStore(AccountIdentityDbContext database, ILookupNormalizer normalizer)
    : IProfileAccessGrantStore
{
    public async Task<GrantProfileAccessResult> GrantAsync(Guid accountId, Guid profileId, string email, string role,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var isOwner = await database.BudgetProfiles.AnyAsync(
            profile => profile.Id == profileId && profile.OwnerAccountId == accountId, cancellationToken);
        if (!isOwner) return GrantProfileAccessResult.NotOwner;

        var normalizedEmail = normalizer.NormalizeEmail(email.Trim());
        if (string.IsNullOrWhiteSpace(normalizedEmail)) return GrantProfileAccessResult.RecipientNotEligible;

        var recipient = await database.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail && user.EmailConfirmed, cancellationToken);
        if (recipient is null) return GrantProfileAccessResult.RecipientNotEligible;
        if (recipient.Id == accountId) return GrantProfileAccessResult.OwnerCannotReceiveGrant;

        var access = await database.ProfileAccess.SingleOrDefaultAsync(
            grant => grant.ProfileId == profileId && grant.AccountId == recipient.Id, cancellationToken);
        if (access is null)
        {
            database.ProfileAccess.Add(new ProfileAccessRecord
            {
                ProfileId = profileId,
                AccountId = recipient.Id,
                Role = role
            });
        }
        else
        {
            access.Role = role;
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return GrantProfileAccessResult.Granted;
    }
}
