using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

// Comment ID: CLASS-045. Reads an exact profile card with current access evaluated in the same SQL query.
public sealed class EfCardRevealReader(AccountIdentityDbContext database) : ICardRevealReader
{
    // Comment ID: METHOD-063. Projects ciphertext only for a current writer, excluding Cash and cross-profile method IDs.
    public async Task<CardRevealLookup> GetAsync(Guid accountId, Guid profileId, Guid methodId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await database.BudgetProfiles.AsNoTracking()
                .Where(profile => profile.Id == profileId && (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                    access.ProfileId == profile.Id && access.AccountId == accountId)))
                .Select(profile => new
                {
                    Writer = profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                        access.ProfileId == profile.Id && access.AccountId == accountId && access.Role == "editor"),
                    Ciphertext = database.PaymentMethods.Where(method => method.Id == methodId && method.ProfileId == profile.Id && method.Kind == "credit_card"
                        && (profile.OwnerAccountId == accountId || database.ProfileAccess.Any(access =>
                            access.ProfileId == profile.Id && access.AccountId == accountId && access.Role == "editor")))
                        .Select(method => method.ProtectedNumber).FirstOrDefault()
                }).SingleOrDefaultAsync(cancellationToken);
            if (result is null) return new(CardRevealStatus.UnavailableMethod);
            if (!result.Writer) return new(CardRevealStatus.Forbidden);
            return result.Ciphertext is null ? new(CardRevealStatus.UnavailableMethod) : new(CardRevealStatus.Revealed, result.Ciphertext);
        }
        catch (SqlException) { throw new CardRevealUnavailableException(); }
    }
}
