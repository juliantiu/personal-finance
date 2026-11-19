using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class RemoveProfileAccessTests
{
    [Fact]
    public async Task Signed_in_account_profile_and_recipient_email_reach_the_store()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var store = new StubRemovalStore();
        var handler = new RemoveProfileAccessHandler(new StubCurrentAccount(accountId), store);

        var result = await handler.Handle(new RemoveProfileAccessCommand(profileId, "recipient@example.test"),
            TestContext.Current.CancellationToken);

        Assert.Equal(RemoveProfileAccessResult.Removed, result);
        Assert.Equal(accountId, store.AccountId);
        Assert.Equal(profileId, store.ProfileId);
        Assert.Equal("recipient@example.test", store.Email);
    }

    [Fact]
    public async Task Unauthenticated_account_cannot_request_removal()
    {
        var store = new StubRemovalStore();
        var handler = new RemoveProfileAccessHandler(new StubCurrentAccount(null), store);

        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(
            new RemoveProfileAccessCommand(Guid.NewGuid(), "recipient@example.test"),
            TestContext.Current.CancellationToken).AsTask());

        Assert.Null(store.AccountId);
    }

    [Fact]
    public async Task Missing_recipient_email_fails_validation()
    {
        var validator = new RemoveProfileAccessValidator();
        var result = await validator.ValidateAsync(new RemoveProfileAccessCommand(Guid.NewGuid(), " "),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(RemoveProfileAccessCommand.Email));
    }

    private sealed class StubCurrentAccount(Guid? accountId) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(accountId);
    }

    private sealed class StubRemovalStore : IProfileAccessRemovalStore
    {
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public string? Email { get; private set; }

        public Task<RemoveProfileAccessResult> RemoveAsync(Guid accountId, Guid profileId, string email,
            CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            Email = email;
            return Task.FromResult(RemoveProfileAccessResult.Removed);
        }
    }
}
