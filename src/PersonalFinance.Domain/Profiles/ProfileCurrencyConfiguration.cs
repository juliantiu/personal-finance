namespace PersonalFinance.Domain.Profiles;

public sealed class ProfileCurrencyConfiguration
{
    public string MainCurrency { get; }
    public IReadOnlyList<string> AdditionalCurrencies { get; }

    private ProfileCurrencyConfiguration(string mainCurrency, string[] additionalCurrencies)
    {
        MainCurrency = mainCurrency;
        AdditionalCurrencies = Array.AsReadOnly(additionalCurrencies);
    }

    public static ProfileCurrencyConfiguration Create(string mainCurrency, IReadOnlyList<string>? additionalCurrencies)
    {
        if (!BudgetProfile.IsSupportedCurrency(mainCurrency))
            throw new ArgumentException("The main currency is not supported.", nameof(mainCurrency));

        var additional = additionalCurrencies?.ToArray() ?? [];
        if (additional.Length > 1 || additional.Any(currency => !BudgetProfile.IsSupportedCurrency(currency) || currency == mainCurrency)
            || additional.Distinct(StringComparer.Ordinal).Count() != additional.Length)
            throw new ArgumentException("Additional currencies must be supported, distinct, and different from the main currency.", nameof(additionalCurrencies));

        return new ProfileCurrencyConfiguration(mainCurrency, additional);
    }
}
