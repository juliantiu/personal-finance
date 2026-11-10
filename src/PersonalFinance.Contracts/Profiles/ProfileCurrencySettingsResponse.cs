namespace PersonalFinance.Contracts.Profiles;

public sealed record SupportedCurrencyResponse(string Code, string Name, string Symbol);

public sealed record ProfileCurrencySettingsResponse(
    Guid ProfileId,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    long ConfigurationVersion,
    IReadOnlyList<SupportedCurrencyResponse> SupportedCurrencies);

public sealed record UpdateProfileCurrencySettingsRequest(string[]? AdditionalCurrencies, long ConfigurationVersion);
