using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class ListAccessibleProfilesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Only_a_resolved_current_account_can_reach_the_reader(bool authenticated)
    {
        var id = Guid.NewGuid();
        var account = new StubAccount(authenticated ? id : null);
        var reader = new StubReader();
        var handler = new ListAccessibleProfilesHandler(account, reader);
        if (authenticated)
        {
            var result = await handler.Handle(new ListAccessibleProfilesQuery(), TestContext.Current.CancellationToken);
            Assert.Same(reader.Profiles, result);
            Assert.Equal(id, reader.AccountId);
            Assert.Equal(TestContext.Current.CancellationToken, reader.CancellationToken);
        }
        else
        {
            await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(new ListAccessibleProfilesQuery(),
                TestContext.Current.CancellationToken).AsTask());
            Assert.Null(reader.AccountId);
        }
        Assert.Equal(TestContext.Current.CancellationToken, account.CancellationToken);
    }

    [Fact]
    public async Task Reader_failure_propagates_instead_of_becoming_an_empty_list()
    {
        var failure = new InvalidOperationException("synthetic store failure");
        var handler = new ListAccessibleProfilesHandler(new StubAccount(Guid.NewGuid()), new StubReader { Failure = failure });
        var result = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ListAccessibleProfilesQuery(),
            TestContext.Current.CancellationToken).AsTask());
        Assert.Same(failure, result);
    }

    private sealed class StubAccount(Guid? id) : ICurrentAccount
    {
        public CancellationToken CancellationToken { get; private set; }
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return Task.FromResult(id);
        }
    }

    private sealed class StubReader : IAccessibleProfileReader
    {
        public Guid? AccountId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Exception? Failure { get; init; }
        public IReadOnlyList<AccessibleProfile> Profiles { get; } = [new(Guid.NewGuid(), "Home", "GBP", "owner")];
        public Task<IReadOnlyList<AccessibleProfile>> ListAsync(Guid accountId, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            CancellationToken = cancellationToken;
            return Failure is null ? Task.FromResult(Profiles) : Task.FromException<IReadOnlyList<AccessibleProfile>>(Failure);
        }
    }
}
