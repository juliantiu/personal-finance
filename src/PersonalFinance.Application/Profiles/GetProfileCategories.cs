using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record ProfileCategory(Guid Id, string Name, string Kind);

public sealed record ProfileCategories(Guid ProfileId, string Role, IReadOnlyList<ProfileCategory> Categories);

public interface IProfileCategoriesReader
{
    Task<ProfileCategories?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

public sealed record GetProfileCategoriesQuery(Guid ProfileId) : IQuery<ProfileCategories?>;

public sealed class GetProfileCategoriesHandler(ICurrentAccount currentAccount, IProfileCategoriesReader categories)
    : IQueryHandler<GetProfileCategoriesQuery, ProfileCategories?>
{
    public async ValueTask<ProfileCategories?> Handle(GetProfileCategoriesQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await categories.GetAsync(accountId, query.ProfileId, cancellationToken);
    }
}
