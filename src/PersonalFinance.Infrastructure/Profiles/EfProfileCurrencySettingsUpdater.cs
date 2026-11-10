using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileCurrencySettingsUpdater(AccountIdentityDbContext database) : IProfileCurrencySettingsUpdater
{
    public async Task<ProfileCurrencySettingsUpdateResult> UpdateAsync(
        Guid accountId,
        Guid profileId,
        IReadOnlyList<string>? additionalCurrencies,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var profile = await database.BudgetProfiles.SingleOrDefaultAsync(value => value.Id == profileId, cancellationToken);
        if (profile is null) return ProfileCurrencySettingsUpdateResult.NotFound;

        if (profile.OwnerAccountId != accountId)
        {
            var role = await database.ProfileAccess.Where(access => access.ProfileId == profileId && access.AccountId == accountId)
                .Select(access => access.Role).SingleOrDefaultAsync(cancellationToken);
            
            if (role is null) return ProfileCurrencySettingsUpdateResult.NotFound;
            if (role != "editor") return ProfileCurrencySettingsUpdateResult.Forbidden;
        }

        if (profile.CurrencySettingsVersion != expectedVersion)
            return ProfileCurrencySettingsUpdateResult.Conflict;

        ProfileCurrencyConfiguration configuration;
        try
        { 
            configuration = ProfileCurrencyConfiguration.Create(profile.MainCurrencyCode, additionalCurrencies);
        }
        catch (ArgumentException)
        { 
            return ProfileCurrencySettingsUpdateResult.Invalid;
        }
        
        var currentCurrencies = await database.ProfileCurrencies.Where(value => value.ProfileId == profileId).ToListAsync(cancellationToken);
        var desired = configuration.AdditionalCurrencies.ToHashSet(StringComparer.Ordinal);
        var current = currentCurrencies.Select(value => value.CurrencyCode).ToHashSet(StringComparer.Ordinal);
        
        database.ProfileCurrencies.RemoveRange(currentCurrencies.Where(value => !desired.Contains(value.CurrencyCode)));
        database.ProfileCurrencies.AddRange(configuration.AdditionalCurrencies.Where(currency => !current.Contains(currency))
                                                                              .Select(currency => new ProfileCurrencyRecord { ProfileId = profileId, CurrencyCode = currency }));
        
        profile.CurrencySettingsVersion++;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ProfileCurrencySettingsUpdateResult.Updated;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ProfileCurrencySettingsUpdateResult.Conflict;
        }
    }
}
