using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record ProfilePaymentMethods(Guid ProfileId, string ProfileName, string Role, IReadOnlyList<ProfilePaymentMethodDetails> Methods);

public interface IProfilePaymentMethodReader
{
    Task<ProfilePaymentMethods?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

public sealed record ListPaymentMethodsQuery(Guid ProfileId) : IQuery<ProfilePaymentMethods?>;

public sealed class ListPaymentMethodsHandler(ICurrentAccount currentAccount, IProfilePaymentMethodReader methods)
    : IQueryHandler<ListPaymentMethodsQuery, ProfilePaymentMethods?>
{
    public async ValueTask<ProfilePaymentMethods?> Handle(ListPaymentMethodsQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await methods.GetAsync(accountId, query.ProfileId, cancellationToken);
    }
}
