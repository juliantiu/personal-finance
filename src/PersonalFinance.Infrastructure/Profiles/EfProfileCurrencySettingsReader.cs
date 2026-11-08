using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

// Comment ID: CLASS-045. Reads saved currency settings and the catalog only for a currently accessible profile.
public sealed class EfProfileCurrencySettingsReader(AccountIdentityDbContext database) : IProfileCurrencySettingsReader
{
    // Comment ID: METHOD-061. Projects the profile, effective role, enabled alternatives, and supported catalog in one authorized read.
    public async Task<ProfileCurrencySettings?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        return await database.BudgetProfiles.AsNoTracking()
            .Where(profile => profile.Id == profileId &&
                (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                    access.ProfileId == profile.Id && access.AccountId == accountId)))
            .Select(profile => new ProfileCurrencySettings(
                profile.Id,
                profile.MainCurrencyCode,
                database.ProfileCurrencies.Where(currency => currency.ProfileId == profile.Id)
                    .OrderBy(currency => currency.CurrencyCode).Select(currency => currency.CurrencyCode).ToList(),
                profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                    .Where(access => access.ProfileId == profile.Id && access.AccountId == accountId)
                    .Select(access => access.Role).First(),
                database.Currencies.OrderBy(currency => currency.Code)
                    .Select(currency => new SupportedProfileCurrency(currency.Code, currency.Name, currency.Symbol)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
