namespace PersonalFinance.Contracts.Profiles;

public sealed record CreateProfileRequest(string? Name, string? MainCurrency, string[]? AdditionalCurrencies);

public sealed record CreatedProfileResponse(Guid Id);
