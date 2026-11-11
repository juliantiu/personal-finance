using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using DomainProfileSubcategory = PersonalFinance.Domain.Profiles.ProfileSubcategory;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileSubcategoryWriter(AccountIdentityDbContext database) : IProfileSubcategoryWriter
{
    public async Task<Guid?> CreateAsync(Guid accountId, Guid profileId, Guid categoryId,
        DomainProfileSubcategory subcategory, CancellationToken cancellationToken)
    {
        if (subcategory.CategoryId != categoryId) return null;
        if (!await CanEditAsync(accountId, profileId, cancellationToken)) return null;
        var category = await database.Categories.AsNoTracking().SingleOrDefaultAsync(value =>
            value.ProfileId == profileId && value.Id == categoryId, cancellationToken);
        if (category is null) return null;

        database.Subcategories.Add(new SubcategoryRecord
        {
            Id = subcategory.Id,
            CategoryId = category.Id,
            Name = subcategory.Name,
            Comment = subcategory.Comment
        });
        if (category.Kind == "savings")
            database.SavingsPlans.Add(new SavingsPlanRecord { SubcategoryId = subcategory.Id });

        await database.SaveChangesAsync(cancellationToken);
        return subcategory.Id;
    }

    public async Task<UpdateProfileSubcategoryResult> UpdateAsync(Guid accountId, Guid profileId, Guid categoryId,
        Guid subcategoryId, ProfileSubcategoryMetadata metadata, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(accountId, profileId, cancellationToken)) return UpdateProfileSubcategoryResult.NotFound;
        var record = await database.Subcategories.SingleOrDefaultAsync(value =>
            value.Id == subcategoryId && value.CategoryId == categoryId &&
            database.Categories.Any(category => category.Id == value.CategoryId && category.ProfileId == profileId), cancellationToken);
        if (record is null) return UpdateProfileSubcategoryResult.NotFound;
        if (!record.Version.SequenceEqual(expectedVersion)) return UpdateProfileSubcategoryResult.Conflict;

        record.Name = metadata.Name;
        record.Comment = metadata.Comment;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return UpdateProfileSubcategoryResult.Updated;
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateProfileSubcategoryResult.Conflict;
        }
    }

    // Comment ID: METHOD-074. Allows the profile owner or a current editor to mutate profile category data.
    private async Task<bool> CanEditAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken) =>
        await database.BudgetProfiles.AnyAsync(profile => profile.Id == profileId && profile.OwnerAccountId == accountId, cancellationToken)
        || await database.ProfileAccess.AnyAsync(access => access.ProfileId == profileId && access.AccountId == accountId && access.Role == "editor", cancellationToken);
}
