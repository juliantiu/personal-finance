using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfilePaymentMethodReader(AccountIdentityDbContext database) : IProfilePaymentMethodReader
{
    public Task<ProfilePaymentMethods?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        return database.BudgetProfiles.AsNoTracking()
            .Where(profile => profile.Id == profileId &&
                (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                    access.ProfileId == profile.Id && access.AccountId == accountId)))
            .Select(profile => new ProfilePaymentMethods(
                profile.Id,
                profile.Name,
                profile.OwnerAccountId == accountId ? "owner" : database.ProfileAccess
                    .Where(access => access.ProfileId == profile.Id && access.AccountId == accountId)
                    .Select(access => access.Role).First(),
                database.PaymentMethods.Where(method => method.ProfileId == profile.Id)
                    .OrderBy(method => method.Kind).ThenBy(method => method.Id)
                    .Select(method => new ProfilePaymentMethodDetails(method.Id, method.Kind, method.Kind == "credit_card" ? "•••• " + method.LastFour : null, method.Version, method.RetiredAt != null, method.Nickname)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
