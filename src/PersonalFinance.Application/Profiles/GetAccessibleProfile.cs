using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record ProfileCategoryDetails(string Name, string Kind);
public sealed record ProfilePaymentMethodDetails(Guid Id, string Kind, string? MaskedIdentifier = null, byte[]? Version = null, bool IsRetired = false, string? Nickname = null);
public sealed record ProfileTagDetails(string Name, string? Role);
public sealed record AccessibleProfileDetails(
    Guid Id,
    string Name,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    IReadOnlyList<ProfileCategoryDetails> Categories,
    IReadOnlyList<ProfilePaymentMethodDetails> PaymentMethods,
    IReadOnlyList<ProfileTagDetails> Tags);

public interface IProfileDetailsReader
{
    Task<AccessibleProfileDetails?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

public sealed record GetAccessibleProfileQuery(Guid ProfileId) : IQuery<AccessibleProfileDetails?>;

public sealed class GetAccessibleProfileHandler(ICurrentAccount currentAccount, IProfileDetailsReader profiles)
    : IQueryHandler<GetAccessibleProfileQuery, AccessibleProfileDetails?>
{
    public async ValueTask<AccessibleProfileDetails?> Handle(GetAccessibleProfileQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await profiles.GetAsync(accountId, query.ProfileId, cancellationToken);
    }
}
