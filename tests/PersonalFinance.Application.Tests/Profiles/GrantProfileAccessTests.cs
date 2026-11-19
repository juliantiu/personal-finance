using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class GrantProfileAccessTests
{
    [Theory]
    [InlineData("editor")]
    [InlineData("view_only")]
    public async Task Owner_identity_profile_email_and_role_reach_the_store(string role)
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var store = new StubGrantStore();
        var handler = new GrantProfileAccessHandler(new StubCurrentAccount(accountId), store);
        var result = await handler.Handle(new GrantProfileAccessCommand(profileId, "recipient@example.test", role),
            TestContext.Current.CancellationToken);

        Assert.Equal(GrantProfileAccessResult.Granted, result);
        Assert.Equal(accountId, store.AccountId);
        Assert.Equal(profileId, store.ProfileId);
        Assert.Equal("recipient@example.test", store.Email);
        Assert.Equal(role, store.Role);
    }

    [Fact]
    public async Task Unauthenticated_account_cannot_request_a_grant()
    {
        var store = new StubGrantStore();
        var handler = new GrantProfileAccessHandler(new StubCurrentAccount(null), store);

        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(
            new GrantProfileAccessCommand(Guid.NewGuid(), "recipient@example.test", "editor"),
            TestContext.Current.CancellationToken).AsTask());

        Assert.Null(store.AccountId);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("viewer")]
    [InlineData("")]
    public async Task Unsupported_roles_fail_validation(string role)
    {
        var validator = new GrantProfileAccessValidator();
        var result = await validator.ValidateAsync(
            new GrantProfileAccessCommand(Guid.NewGuid(), "recipient@example.test", role),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GrantProfileAccessCommand.Role));
    }

    private sealed class StubCurrentAccount(Guid? accountId) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(accountId);
    }

    private sealed class StubGrantStore : IProfileAccessGrantStore
    {
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public string? Email { get; private set; }
        public string? Role { get; private set; }

        public Task<GrantProfileAccessResult> GrantAsync(Guid accountId, Guid profileId, string email, string role,
            CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            Email = email;
            Role = role;
            return Task.FromResult(GrantProfileAccessResult.Granted);
        }
    }
}
