using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfAccessibleProfileReader(AccountIdentityDbContext database) : IAccessibleProfileReader
{
    public async Task<IReadOnlyList<AccessibleProfile>> ListAsync(Guid accountId, CancellationToken cancellationToken) =>
        await database.BudgetProfiles.AsNoTracking()
            .Where(profile => profile.OwnerAccountId == accountId || database.ProfileAccess.Any(
                access => access.ProfileId == profile.Id && access.AccountId == accountId))
            .Select(profile => new AccessibleProfile(profile.Id, profile.Name, profile.MainCurrencyCode,
                profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                    .Where(access => access.ProfileId == profile.Id && access.AccountId == accountId)
                    .Select(access => access.Role).First()))
            .ToListAsync(cancellationToken);
}
