using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record SupportedProfileCurrency(string Code, string Name, string Symbol);

public sealed record ProfileCurrencySettings(
    Guid ProfileId,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    long ConfigurationVersion,
    IReadOnlyList<SupportedProfileCurrency> SupportedCurrencies);

public interface IProfileCurrencySettingsReader
{
    Task<ProfileCurrencySettings?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

public sealed record GetProfileCurrencySettingsQuery(Guid ProfileId) : IQuery<ProfileCurrencySettings?>;

public sealed class GetProfileCurrencySettingsHandler(
    ICurrentAccount currentAccount,
    IProfileCurrencySettingsReader profileCurrenySettings) : IQueryHandler<GetProfileCurrencySettingsQuery, ProfileCurrencySettings?>
{
    public async ValueTask<ProfileCurrencySettings?> Handle(GetProfileCurrencySettingsQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await profileCurrenySettings.GetAsync(accountId, query.ProfileId, cancellationToken);
    }
}
