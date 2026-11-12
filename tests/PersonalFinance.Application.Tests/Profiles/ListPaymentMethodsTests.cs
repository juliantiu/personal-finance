using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class ListPaymentMethodsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_account_and_requested_profile_are_passed_to_access_reader(bool unavailable)
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var reader = new StubReader { Result = unavailable ? null : new ProfilePaymentMethods(profileId, "Home", "owner", []) };
        var handler = new ListPaymentMethodsHandler(new StubAccount(accountId), reader);
        var result = await handler.Handle(new ListPaymentMethodsQuery(profileId), TestContext.Current.CancellationToken);
        Assert.Same(reader.Result, result);
        Assert.Equal(accountId, reader.AccountId);
        Assert.Equal(profileId, reader.ProfileId);
        Assert.Equal(TestContext.Current.CancellationToken, reader.Token);
    }

    [Fact]
    public async Task Missing_eligible_account_cannot_invoke_the_reader()
    {
        var reader = new StubReader();
        var handler = new ListPaymentMethodsHandler(new StubAccount(null), reader);
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(new ListPaymentMethodsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken).AsTask());
        Assert.Null(reader.AccountId);
    }

    private sealed class StubAccount(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }

    private sealed class StubReader : IProfilePaymentMethodReader
    {
        public ProfilePaymentMethods? Result { get; init; }
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<ProfilePaymentMethods?> GetAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            Token = cancellationToken;
            return Task.FromResult(Result);
        }
    }
}
