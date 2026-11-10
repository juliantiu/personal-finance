using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileCategoriesReader(AccountIdentityDbContext database) : IProfileCategoriesReader
{
    public async Task<ProfileCategories?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        return await database.BudgetProfiles.AsNoTracking()
            .Where(profile => profile.Id == profileId &&
                (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                    access.ProfileId == profile.Id && access.AccountId == accountId)))
            .Select(profile => new ProfileCategories(
                profile.Id,
                profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                    .Where(access => access.ProfileId == profile.Id && access.AccountId == accountId)
                    .Select(access => access.Role).First(),
                database.Categories.Where(category => category.ProfileId == profile.Id)
                    .OrderBy(category => category.Kind).ThenBy(category => category.Id)
                    .Select(category => new ProfileCategory(category.Id, category.Name, category.Kind)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
