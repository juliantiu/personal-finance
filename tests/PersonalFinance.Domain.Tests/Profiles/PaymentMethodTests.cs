using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Domain.Tests.Profiles;

public sealed class PaymentMethodTests
{
    [Fact]
    public void Profile_creation_owns_its_initial_cash_method_and_derives_the_same_cash_identity()
    {
        var profile = BudgetProfile.Create(Guid.NewGuid(), "Home", "GBP", []);
        Assert.NotNull(profile.CashPaymentMethod);
        Assert.Equal(profile.Id, profile.CashPaymentMethod.ProfileId);
        Assert.Equal(profile.CashPaymentMethod.Id, profile.CashPaymentMethodId);
        Assert.Equal(PaymentMethodKind.Cash, profile.CashPaymentMethod.Kind);
        Assert.Equal("Cash", profile.CashPaymentMethod.Nickname);
        Assert.Null(profile.CashPaymentMethod.Number);
    }

    [Fact]
    public void Cash_has_profile_identity_default_nickname_and_no_card_details()
    {
        var profile = Guid.NewGuid();
        var method = PaymentMethod.CreateCash(profile);
        Assert.NotEqual(Guid.Empty, method.Id);
        Assert.Equal(profile, method.ProfileId);
        Assert.Equal(PaymentMethodKind.Cash, method.Kind);
        Assert.Equal("Cash", method.Nickname);
        Assert.Null(method.Number);
    }

    [Fact]
    public void Card_creation_groups_identity_normalized_nickname_and_validated_number()
    {
        var profile = Guid.NewGuid();
        var method = PaymentMethod.CreateCreditCard(profile, "4111-1111 1111 1111", "  Travel card  ");
        Assert.NotEqual(Guid.Empty, method.Id);
        Assert.Equal(profile, method.ProfileId);
        Assert.Equal(PaymentMethodKind.CreditCard, method.Kind);
        Assert.Equal("Travel card", method.Nickname);
        Assert.Equal("4111111111111111", method.Number!.Value);
        Assert.Equal("1111", method.Number.LastFour);
        Assert.DoesNotContain(method.Number.Value, method.ToString());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \t\r\n ", null)]
    [InlineData("  Travel card \t", "Travel card")]
    [InlineData("Family  spending", "Family  spending")]
    public void Optional_nicknames_are_normalized_by_the_entity(string? input, string? expected)
    {
        var method = PaymentMethod.CreateCreditCard(Guid.NewGuid(), "4111111111111111", input);
        Assert.Equal(expected, method.Nickname);
    }

    [Fact]
    public void Nickname_length_accepts_100_after_trimming_and_rejects_101_without_echoing_input()
    {
        var valid = new string('x', 100);
        Assert.Equal(valid, PaymentMethod.CreateCreditCard(Guid.NewGuid(), "4111111111111111", " \t" + valid + "\n ").Nickname);
        var invalid = new string('x', 101);
        var error = Assert.Throws<PaymentMethodValidationException>(() => PaymentMethod.CreateCreditCard(Guid.NewGuid(), "4111111111111111", invalid));
        Assert.Equal("Nickname", error.Field);
        Assert.DoesNotContain(invalid, error.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("4111111111111112")]
    public void Credit_cards_cannot_exist_without_a_valid_number(string? number)
    {
        var error = Assert.Throws<PaymentMethodValidationException>(() => PaymentMethod.CreateCreditCard(Guid.NewGuid(), number));
        Assert.Equal("Number", error.Field);
        if (!string.IsNullOrEmpty(number)) Assert.DoesNotContain(number, error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Methods_require_a_profile(bool card)
    {
        Assert.Throws<ArgumentException>(() => card
            ? PaymentMethod.CreateCreditCard(Guid.Empty, "4111111111111111") : PaymentMethod.CreateCash(Guid.Empty));
    }

    [Fact]
    public void Duplicate_nicknames_do_not_identify_methods_and_leading_zeros_remain_text()
    {
        var profile = Guid.NewGuid();
        var first = PaymentMethod.CreateCreditCard(profile, "000000000000", "Shared");
        var second = PaymentMethod.CreateCreditCard(profile, "4111111111111111", "Shared");
        Assert.Equal("000000000000", first.Number!.Value);
        Assert.Equal(first.Nickname, second.Nickname);
        Assert.NotEqual(first.Id, second.Id);
    }
}
