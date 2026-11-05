using Mediator;

namespace PersonalFinance.Application.Profiles;

public interface ICurrentAccount
{
    Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken);
}

public sealed record AccessibleProfile(Guid Id, string Name, string MainCurrency, string Role);

public interface IAccessibleProfileReader
{
    Task<IReadOnlyList<AccessibleProfile>> ListAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed record ListAccessibleProfilesQuery : IQuery<IReadOnlyList<AccessibleProfile>>;

public sealed class UnauthenticatedAccountException : Exception;

public sealed class ListAccessibleProfilesHandler(ICurrentAccount currentAccount, IAccessibleProfileReader profiles)
    : IQueryHandler<ListAccessibleProfilesQuery, IReadOnlyList<AccessibleProfile>>
{
    public async ValueTask<IReadOnlyList<AccessibleProfile>> Handle(ListAccessibleProfilesQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await profiles.ListAsync(accountId, cancellationToken);
    }
}
