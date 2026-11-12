namespace PersonalFinance.Contracts.Profiles;

public sealed record ProfileCategoryDetailsResponse(string Name, string Kind);

public sealed record ProfilePaymentMethodDetailsResponse(Guid Id, string Kind);

public sealed record ProfileTagDetailsResponse(string Name, string? Role);

public sealed record ProfileDetailsResponse(
    Guid Id,
    string Name,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    IReadOnlyList<ProfileCategoryDetailsResponse> Categories,
    IReadOnlyList<ProfilePaymentMethodDetailsResponse> PaymentMethods,
    IReadOnlyList<ProfileTagDetailsResponse> Tags);
