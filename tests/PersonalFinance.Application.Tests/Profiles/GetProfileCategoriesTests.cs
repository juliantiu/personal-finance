using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class GetProfileCategoriesTests
{
    [Fact]
    public async Task Current_account_requested_profile_and_cancellation_are_passed_to_reader_without_replacing_category_identity()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var reader = new StubReader { Result = new(profileId, "editor", [new(Guid.NewGuid(), "Earnings", "income", null, [1, 2, 3], null, [])]) };
        var handler = new GetProfileCategoriesHandler(new StubAccount(accountId), reader);

        var result = await handler.Handle(new GetProfileCategoriesQuery(profileId), TestContext.Current.CancellationToken);

        Assert.Equal(accountId, reader.AccountId);
        Assert.Equal(profileId, reader.ProfileId);
        Assert.Equal(TestContext.Current.CancellationToken, reader.CancellationToken);
        Assert.Same(reader.Result, result);
    }

    [Fact]
    public async Task Missing_account_is_rejected_before_reading_categories()
    {
        var reader = new StubReader();
        var handler = new GetProfileCategoriesHandler(new StubAccount(null), reader);
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(new GetProfileCategoriesQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken).AsTask());
        Assert.Null(reader.AccountId);
    }

    [Fact]
    public async Task Missing_or_inaccessible_profile_returns_no_categories()
    {
        var handler = new GetProfileCategoriesHandler(new StubAccount(Guid.NewGuid()), new StubReader());
        Assert.Null(await handler.Handle(new GetProfileCategoriesQuery(Guid.NewGuid()), TestContext.Current.CancellationToken));
    }

    private sealed class StubAccount(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }

    private sealed class StubReader : IProfileCategoriesReader
    {
        public ProfileCategories? Result { get; init; }
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<ProfileCategories?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            CancellationToken = cancellationToken;
            return Task.FromResult(Result);
        }
    }
}
