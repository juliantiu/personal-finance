using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class CreateProfileTests
{
    [Theory]
    [InlineData(null, "GBP", "Name")]
    [InlineData("  ", "GBP", "Name")]
    [InlineData("Home", null, "MainCurrency")]
    [InlineData("Home", "EUR", "MainCurrency")]
    public async Task Invalid_fields_are_reported(string? name, string? main, string field)
    {
        var result = await new CreateProfileValidator().ValidateAsync(new CreateProfileCommand(name, main, []), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public async Task Validation_uses_trimmed_length_and_checks_additional_memberships()
    {
        var validator = new CreateProfileValidator();
        Assert.True((await validator.ValidateAsync(new CreateProfileCommand(" " + new string('x', 100) + " ", "USD", ["GBP"]), TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await validator.ValidateAsync(new CreateProfileCommand(new string('x', 101), "GBP", []), TestContext.Current.CancellationToken)).IsValid);
        foreach (string[] currencies in new[] { new[] { "GBP" }, new[] { "USD", "USD" }, new[] { "EUR" }, new string[] { null! } })
        {
            var result = await validator.ValidateAsync(new CreateProfileCommand("Home", "GBP", currencies), TestContext.Current.CancellationToken);
            Assert.Contains(result.Errors, error => error.PropertyName == "AdditionalCurrencies");
        }
        Assert.True((await validator.ValidateAsync(new CreateProfileCommand("Home", "GBP", null), TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task Handler_uses_current_account_and_saves_complete_profile_with_cancellation()
    {
        var accountId = Guid.NewGuid();
        var account = new CurrentAccount(accountId);
        var store = new Store();
        var id = await new CreateProfileHandler(account, store).Handle(new(" Home ", "USD", ["GBP"]), TestContext.Current.CancellationToken);
        Assert.Equal(id, store.Saved!.Id);
        Assert.Equal(accountId, store.Saved.OwnerAccountId);
        Assert.Equal("Home", store.Saved.Name);
        Assert.Equal(3, store.Saved.Categories.Count);
        Assert.Equal(2, store.Saved.Tags.Count);
        Assert.Equal(TestContext.Current.CancellationToken, store.Token);
        Assert.Equal(TestContext.Current.CancellationToken, account.Token);
    }

    [Fact]
    public async Task Anonymous_or_invalid_domain_input_cannot_reach_persistence()
    {
        var store = new Store();
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => new CreateProfileHandler(new CurrentAccount(null), store)
            .Handle(new("Home", "GBP", []), TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => new CreateProfileHandler(new CurrentAccount(Guid.NewGuid()), store)
            .Handle(new("Home", "GBP", ["GBP"]), TestContext.Current.CancellationToken).AsTask());
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task Store_failure_is_not_reported_as_success()
    {
        var store = new Store { Fail = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CreateProfileHandler(new CurrentAccount(Guid.NewGuid()), store)
            .Handle(new("Home", "GBP", []), TestContext.Current.CancellationToken).AsTask());
    }

    private sealed class CurrentAccount(Guid? id) : ICurrentAccount
    {
        public CancellationToken Token { get; private set; }
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken)
        { Token = cancellationToken; return Task.FromResult(id); }
    }

    private sealed class Store : IProfileCreationStore
    {
        public bool Fail { get; init; }
        public BudgetProfile? Saved { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task SaveAsync(BudgetProfile profile, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            if (Fail) throw new InvalidOperationException("Synthetic store failure");
            Saved = profile;
            return Task.CompletedTask;
        }
    }
}
