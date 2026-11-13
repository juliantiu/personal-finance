using PersonalFinance.Application.Common;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class SaveCardTests
{
    [Fact]
    public async Task Oversized_nickname_is_a_safe_field_error_before_protection_or_persistence()
    {
        var store = new Store();
        var protection = new Protection();
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => new SaveCardHandler(new Account(Guid.NewGuid()), store, protection)
            .Handle(new(Guid.NewGuid(), "4111111111111111", new string('x', 101)), TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("Nickname", error.Errors.Keys);
        Assert.Null(protection.Number);
        Assert.Null(store.Card);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("editor")]
    public async Task Writer_saves_canonical_protected_details_with_current_account_and_stable_identity(string role)
    {
        var account = Guid.NewGuid();
        var profile = Guid.NewGuid();
        var store = new Store { Role = role };
        var protection = new Protection();
        var result = await new SaveCardHandler(new Account(account), store, protection).Handle(new(profile, "4111-1111 1111 1111", "  Travel card  "), TestContext.Current.CancellationToken);
        Assert.Equal(CardSaveStatus.Saved, result.Status);
        Assert.Equal(account, store.Account);
        Assert.Equal(profile, protection.Profile);
        Assert.Equal(protection.Method, result.Method!.Id);
        Assert.Equal("4111111111111111", protection.Number);
        Assert.Equal("1111", store.Card!.LastFour);
        Assert.NotNull(store.Method);
        Assert.Equal(protection.Method, store.Method.Id);
        Assert.Equal(profile, store.Method.ProfileId);
        Assert.Equal(PaymentMethodKind.CreditCard, store.Method.Kind);
        Assert.Equal(protection.Number, store.Method.Number!.Value);
        Assert.Equal("Travel card", store.Method.Nickname);
        Assert.Equal("Travel card", result.Method.Nickname);
        Assert.DoesNotContain("4111111111111111", new SaveCardCommand(profile, protection.Number).ToString());
    }

    [Theory]
    [InlineData(null, CardSaveStatus.UnavailableProfile)]
    [InlineData("view_only", CardSaveStatus.Forbidden)]
    public async Task Unauthorized_input_is_not_validated_or_protected(string? role, CardSaveStatus expected)
    {
        var store = new Store { Role = role };
        var protection = new Protection();
        var result = await new SaveCardHandler(new Account(Guid.NewGuid()), store, protection).Handle(new(Guid.NewGuid(), "bad"), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Status);
        Assert.Null(store.Card);
        Assert.Null(protection.Number);
    }

    [Fact]
    public async Task Invalid_input_and_anonymous_accounts_never_reach_protection_or_writes()
    {
        var store = new Store();
        var protection = new Protection();
        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => new SaveCardHandler(new Account(null), store, protection).Handle(new(Guid.NewGuid(), "bad"), TestContext.Current.CancellationToken).AsTask());
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => new SaveCardHandler(new Account(Guid.NewGuid()), store, protection).Handle(new(Guid.NewGuid(), "4111111111111112"), TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("Number", error.Errors.Keys);
        Assert.DoesNotContain("4111111111111112", error.ToString());
        Assert.Null(protection.Number);
        Assert.Null(store.Card);
    }

    [Fact]
    public async Task Protection_failure_is_safe_and_does_not_write()
    {
        var store = new Store();
        var result = await new SaveCardHandler(new Account(Guid.NewGuid()), store, new Protection { Fail = true }).Handle(new(Guid.NewGuid(), "4111111111111111"), TestContext.Current.CancellationToken);
        Assert.Equal(CardSaveStatus.Unavailable, result.Status);
        Assert.Null(store.Card);
    }

    private sealed class Account(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }
    private sealed class Protection : ICardNumberProtection
    {
        public Guid Profile; public Guid Method; public string? Number; public bool Fail;
        public ProtectedCard Protect(Guid profileId, Guid methodId, CardNumber number)
        {
            if (Fail) throw new CardSavingUnavailableException();
            Profile = profileId; Method = methodId; Number = number.Value;
            return new("synthetic ciphertext", new byte[32], "test-key", number.LastFour);
        }
    }
    private sealed class Store : ICardPaymentMethodStore
    {
        public string? Role = "owner"; public Guid Account; public ProtectedCard? Card; public PaymentMethod? Method;
        public Task<string?> GetRoleAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken) => Task.FromResult(Role);
        public Task<CardSaveResult> SaveAsync(Guid accountId, PaymentMethod method, ProtectedCard card, CancellationToken cancellationToken)
        {
            Account = accountId; Card = card; Method = method;
            return Task.FromResult(new CardSaveResult(CardSaveStatus.Saved, new(method.Id, "credit_card", Nickname: method.Nickname)));
        }
    }
}
