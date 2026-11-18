using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record ProfileParticipant(string Email, string Role);

public interface IProfileParticipantsReader
{
    Task<IReadOnlyList<ProfileParticipant>?> ListAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken);
}

public sealed record ListProfileParticipantsQuery(Guid ProfileId) : IQuery<IReadOnlyList<ProfileParticipant>?>;

public sealed class ListProfileParticipantsHandler(ICurrentAccount currentAccount, IProfileParticipantsReader participants)
    : IQueryHandler<ListProfileParticipantsQuery, IReadOnlyList<ProfileParticipant>?>
{
    public async ValueTask<IReadOnlyList<ProfileParticipant>?> Handle(ListProfileParticipantsQuery query, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await participants.ListAsync(accountId, query.ProfileId, cancellationToken);
    }
}
