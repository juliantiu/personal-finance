using PersonalFinance.Application.Accounts;
using PersonalFinance.Application.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class UpdateProfileCurrencySettingsTests
{
    [Fact]
    public async Task Request_validation_accepts_zero_or_one_supported_additional_currency()
    {
        var validator = new UpdateProfileCurrencySettingsValidator();
        Assert.True((await validator.ValidateAsync(new UpdateProfileCurrencySettingsCommand(Guid.NewGuid(), [], 0), TestContext.Current.CancellationToken)).IsValid);
        Assert.True((await validator.ValidateAsync(new UpdateProfileCurrencySettingsCommand(Guid.NewGuid(), ["USD"], 0), TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await validator.ValidateAsync(new UpdateProfileCurrencySettingsCommand(Guid.NewGuid(), ["EUR"], 0), TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await validator.ValidateAsync(new UpdateProfileCurrencySettingsCommand(Guid.NewGuid(), ["USD", "GBP"], 0), TestContext.Current.CancellationToken)).IsValid);
        Assert.False((await validator.ValidateAsync(new UpdateProfileCurrencySettingsCommand(Guid.NewGuid(), [], -1), TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task Handler_uses_current_account_and_forwards_profile_and_version()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var updater = new Updater();
        var command = new UpdateProfileCurrencySettingsCommand(profileId, ["USD"], 7);
        var result = await new UpdateProfileCurrencySettingsHandler(new CurrentAccount(accountId), updater)
            .Handle(command, TestContext.Current.CancellationToken);

        Assert.Equal(ProfileCurrencySettingsUpdateResult.Updated, result);
        Assert.Equal(accountId, updater.AccountId);
        Assert.Equal(profileId, updater.ProfileId);
        Assert.Equal(new[] { "USD" }, updater.AdditionalCurrencies);
        Assert.Equal(7, updater.Version);
    }

    [Fact]
    public async Task Anonymous_update_never_reaches_the_store()
    {
        var updater = new Updater();
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() =>
            new UpdateProfileCurrencySettingsHandler(new CurrentAccount(null), updater)
                .Handle(new(Guid.NewGuid(), [], 0), TestContext.Current.CancellationToken).AsTask());
        Assert.Null(updater.AccountId);
    }

    private sealed class CurrentAccount(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }

    private sealed class Updater : IProfileCurrencySettingsUpdater
    {
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public IReadOnlyList<string>? AdditionalCurrencies { get; private set; }
        public long Version { get; private set; }

        public Task<ProfileCurrencySettingsUpdateResult> UpdateAsync(Guid accountId, Guid profileId,
            IReadOnlyList<string>? additionalCurrencies, long expectedVersion, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            AdditionalCurrencies = additionalCurrencies;
            Version = expectedVersion;
            return Task.FromResult(ProfileCurrencySettingsUpdateResult.Updated);
        }
    }
}
