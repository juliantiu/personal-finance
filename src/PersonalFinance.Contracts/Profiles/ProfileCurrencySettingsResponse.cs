namespace PersonalFinance.Contracts.Profiles;

// Comment ID: CLASS-043. Describes a browser-safe supported currency choice without persistence details.
public sealed record SupportedCurrencyResponse(string Code, string Name, string Symbol);

// Comment ID: CLASS-044. Returns the profile's saved currencies, current role, and supported choices to the browser.
public sealed record ProfileCurrencySettingsResponse(
    Guid ProfileId,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    IReadOnlyList<SupportedCurrencyResponse> SupportedCurrencies);
