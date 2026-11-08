using Mediator;

namespace PersonalFinance.Application.Profiles;

// Comment ID: CLASS-038. Describes one currency option supported by a profile's current configuration catalog.
public sealed record SupportedProfileCurrency(string Code, string Name, string Symbol);

// Comment ID: CLASS-039. Carries the current accessible profile currency settings and available currency choices.
public sealed record ProfileCurrencySettings(
    Guid ProfileId,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    IReadOnlyList<SupportedProfileCurrency> SupportedCurrencies);

// Comment ID: CLASS-040. Defines the access-scoped read boundary for profile currency settings.
public interface IProfileCurrencySettingsReader
{
    Task<ProfileCurrencySettings?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

// Comment ID: CLASS-041. Requests currency settings for a profile the current account can access.
public sealed record GetProfileCurrencySettingsQuery(Guid ProfileId) : IQuery<ProfileCurrencySettings?>;

// Comment ID: CLASS-042. Resolves the current account and delegates an authorized currency settings read.
public sealed class GetProfileCurrencySettingsHandler(
    ICurrentAccount currentAccount,
    IProfileCurrencySettingsReader profiles) : IQueryHandler<GetProfileCurrencySettingsQuery, ProfileCurrencySettings?>
{
    // Comment ID: METHOD-060. Rejects anonymous reads and returns settings only through the access-scoped reader.
    public async ValueTask<ProfileCurrencySettings?> Handle(GetProfileCurrencySettingsQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await profiles.GetAsync(accountId, query.ProfileId, cancellationToken);
    }
}
