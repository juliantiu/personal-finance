using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Domain.Tests.Profiles;

public sealed class BudgetProfileTests
{
    [Theory]
    [InlineData("GBP", false)]
    [InlineData("GBP", true)]
    [InlineData("USD", false)]
    [InlineData("USD", true)]
    public void Creation_builds_complete_settings_and_stable_defaults(string main, bool additional)
    {
        var owner = Guid.NewGuid();
        var alternative = main == "GBP" ? "USD" : "GBP";
        var profile = BudgetProfile.Create(owner, "  Home  ", main, additional ? [alternative] : []);
        Assert.Equal(owner, profile.OwnerAccountId);
        Assert.Equal("Home", profile.Name);
        Assert.Equal(main, profile.MainCurrency);
        Assert.Equal(additional ? [alternative] : Array.Empty<string>(), profile.AdditionalCurrencies);
        Assert.Equal(new[] { "Income", "Savings", "Debt" }, profile.Categories.Select(category => category.Name));
        Assert.Equal(new[] { SystemCategoryKind.Income, SystemCategoryKind.Savings, SystemCategoryKind.Debt }, profile.Categories.Select(category => category.Kind));
        Assert.Equal(new[] { "IOU", "IOU REPAYMENT" }, profile.Tags.Select(tag => tag.Name));
        Assert.Equal(new[] { SystemTagRole.Iou, SystemTagRole.IouRepayment }, profile.Tags.Select(tag => tag.Role));
        var ids = profile.Categories.Select(category => category.Id).Concat(profile.Tags.Select(tag => tag.Id)).Append(profile.Id).Append(profile.CashPaymentMethodId).ToArray();
        Assert.Equal(7, ids.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, ids);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void Missing_names_are_rejected(string? name) =>
        Assert.Throws<ArgumentException>(() => BudgetProfile.Create(Guid.NewGuid(), name, "GBP", []));

    [Fact]
    public void Name_length_is_checked_after_trimming_and_duplicate_names_are_allowed()
    {
        var name = new string('x', 100);
        var first = BudgetProfile.Create(Guid.NewGuid(), " " + name + " ", "GBP", []);
        var second = BudgetProfile.Create(first.OwnerAccountId, name, "GBP", null);
        Assert.Equal(name, first.Name);
        Assert.Equal(first.Name, second.Name);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Throws<ArgumentException>(() => BudgetProfile.Create(first.OwnerAccountId, new string('x', 101), "GBP", []));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EUR")]
    [InlineData("gbp")]
    public void Missing_or_unsupported_main_currency_is_rejected(string? main) =>
        Assert.Throws<ArgumentException>(() => BudgetProfile.Create(Guid.NewGuid(), "Home", main, []));

    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("")]
    [InlineData(null)]
    public void Additional_currency_must_be_supported_and_different_from_main(string? additional) =>
        Assert.Throws<ArgumentException>(() => BudgetProfile.Create(Guid.NewGuid(), "Home", "GBP", [additional!]));

    [Fact]
    public void Repeated_additional_currency_and_missing_owner_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => BudgetProfile.Create(Guid.NewGuid(), "Home", "GBP", ["USD", "USD"]));
        Assert.Throws<ArgumentException>(() => BudgetProfile.Create(Guid.Empty, "Home", "GBP", []));
    }

    [Fact]
    public void Caller_cannot_mutate_saved_additional_settings()
    {
        var currencies = new[] { "USD" };
        var profile = BudgetProfile.Create(Guid.NewGuid(), "Home", "GBP", currencies);
        currencies[0] = "GBP";
        Assert.Equal("USD", Assert.Single(profile.AdditionalCurrencies));
        Assert.False(profile.AdditionalCurrencies is string[]);
    }
}
