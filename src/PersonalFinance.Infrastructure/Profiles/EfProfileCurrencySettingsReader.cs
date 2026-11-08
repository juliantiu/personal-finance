using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileCurrencySettingsReader(AccountIdentityDbContext database) : IProfileCurrencySettingsReader
{
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
