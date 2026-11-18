using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class GetAccessibleProfileTests
{
    [Fact]
    public async Task Participant_query_passes_current_account_and_profile_to_owner_reader()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var reader = new ParticipantReader();
        var handler = new ListProfileParticipantsHandler(new StubAccount(accountId), reader);
        var result = await handler.Handle(new ListProfileParticipantsQuery(profileId), TestContext.Current.CancellationToken);
        Assert.Equal(accountId, reader.AccountId);
        Assert.Equal(profileId, reader.ProfileId);
        Assert.Same(reader.Result, result);
    }

    [Fact]
    public async Task Unauthenticated_account_cannot_list_profile_participants()
    {
        var reader = new ParticipantReader();
        var handler = new ListProfileParticipantsHandler(new StubAccount(null), reader);
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(
            new ListProfileParticipantsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken).AsTask());
        Assert.Null(reader.AccountId);
    }

    [Fact]
    public async Task Authenticated_account_is_passed_with_the_requested_profile_to_current_access_reader()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var account = new StubAccount(accountId);
        var reader = new StubReader();
        var handler = new GetAccessibleProfileHandler(account, reader);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await handler.Handle(new GetAccessibleProfileQuery(profileId), cancellationToken);

        Assert.Equal(accountId, reader.AccountId);
        Assert.Equal(profileId, reader.ProfileId);
        Assert.Equal(cancellationToken, reader.CancellationToken);
        Assert.Same(reader.Details, result);
    }

    [Fact]
    public async Task Unauthenticated_account_cannot_query_profile_details()
    {
        var reader = new StubReader();
        var handler = new GetAccessibleProfileHandler(new StubAccount(null), reader);

        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(
            new GetAccessibleProfileQuery(Guid.NewGuid()), TestContext.Current.CancellationToken).AsTask());

        Assert.Null(reader.AccountId);
    }

    private sealed class StubAccount(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }

    private sealed class StubReader : IProfileDetailsReader
    {
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public AccessibleProfileDetails Details { get; } = new(Guid.NewGuid(), "Home", "GBP", [], "owner", [], [], []);

        public Task<AccessibleProfileDetails?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            CancellationToken = cancellationToken;
            return Task.FromResult<AccessibleProfileDetails?>(Details);
        }
    }

    private sealed class ParticipantReader : IProfileParticipantsReader
    {
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public IReadOnlyList<ProfileParticipant> Result { get; } = [new("editor@example.test", "editor")];
        public Task<IReadOnlyList<ProfileParticipant>?> ListAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
        { AccountId = accountId; ProfileId = profileId; return Task.FromResult<IReadOnlyList<ProfileParticipant>?>(Result); }
    }
}
