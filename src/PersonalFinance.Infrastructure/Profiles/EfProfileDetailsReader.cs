using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileDetailsReader(AccountIdentityDbContext database) : IProfileDetailsReader
{
    public async Task<AccessibleProfileDetails?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        return await database.BudgetProfiles.AsNoTracking()
            .Where(profile => profile.Id == profileId &&
                (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                    access.ProfileId == profile.Id && access.AccountId == accountId)))
            .Select(profile => new AccessibleProfileDetails(
                profile.Id,
                profile.Name,
                profile.MainCurrencyCode,
                database.ProfileCurrencies.Where(currency => currency.ProfileId == profile.Id)
                    .OrderBy(currency => currency.CurrencyCode).Select(currency => currency.CurrencyCode).ToList(),
                profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                    .Where(access => access.ProfileId == profile.Id && access.AccountId == accountId)
                    .Select(access => access.Role).First(),
                database.Categories.Where(category => category.ProfileId == profile.Id)
                    .OrderBy(category => category.Kind).Select(category => new ProfileCategoryDetails(category.Name, category.Kind)).ToList(),
                database.PaymentMethods.Where(method => method.ProfileId == profile.Id)
                    .OrderBy(method => method.Kind).ThenBy(method => method.Id)
                    .Select(method => new ProfilePaymentMethodDetails(method.Id, method.Kind)).ToList(),
                database.Tags.Where(tag => tag.ProfileId == profile.Id)
                    .OrderBy(tag => tag.SystemRole).ThenBy(tag => tag.Name)
                    .Select(tag => new ProfileTagDetails(tag.Name, tag.SystemRole)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
