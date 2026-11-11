using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileCategoriesReader(AccountIdentityDbContext database) : IProfileCategoriesReader
{
    public async Task<ProfileCategories?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await database.BudgetProfiles.AsNoTracking()
            .Where(profile => profile.Id == profileId &&
                (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                    access.ProfileId == profile.Id && access.AccountId == accountId)))
            .Select(profile => new
            {
                profile.Id,
                Role = profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                    .Where(access => access.ProfileId == profile.Id && access.AccountId == accountId)
                    .Select(access => access.Role).First(),
                Categories = database.Categories.Where(category => category.ProfileId == profile.Id)
                    .OrderBy(category => category.Kind).ThenBy(category => category.Id)
                    .Select(category => new
                    {
                        category.Id,
                        category.Name,
                        category.Kind,
                        category.Comment,
                        category.Version,
                        Subcategories = database.Subcategories.Where(subcategory => subcategory.CategoryId == category.Id)
                            .OrderBy(subcategory => subcategory.Name).ThenBy(subcategory => subcategory.Id)
                            .Select(subcategory => new
                            {
                                subcategory.Id,
                                subcategory.CategoryId,
                                subcategory.Name,
                                subcategory.Comment,
                                subcategory.Version
                            }).ToList()
                    }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return profile is null ? null : new ProfileCategories(profile.Id, profile.Role,
            profile.Categories.Select(category => new ProfileCategory(category.Id, category.Name, category.Kind,
                category.Comment, category.Version, category.Subcategories.Select(subcategory =>
                    new ProfileSubcategoryDetails(subcategory.Id, subcategory.CategoryId, subcategory.Name,
                        subcategory.Comment, subcategory.Version)).ToArray())).ToArray());
    }
}
