namespace PersonalFinance.Contracts.Profiles;

// Comment ID: CLASS-139. Exposes only browser-safe category setup fields for an authorized profile landing page.
public sealed record ProfileCategoryDetailsResponse(string Name, string Kind);

// Comment ID: CLASS-143. Exposes a payment method's kind without server persistence details.
public sealed record ProfilePaymentMethodDetailsResponse(string Kind);

// Comment ID: CLASS-144. Exposes a tag name and its optional system role to the profile landing page.
public sealed record ProfileTagDetailsResponse(string Name, string? Role);

// Comment ID: CLASS-145. Defines the browser-safe profile view returned after the server checks current access.
public sealed record ProfileDetailsResponse(
    Guid Id,
    string Name,
    string MainCurrency,
    IReadOnlyList<string> AdditionalCurrencies,
    string Role,
    IReadOnlyList<ProfileCategoryDetailsResponse> Categories,
    IReadOnlyList<ProfilePaymentMethodDetailsResponse> PaymentMethods,
    IReadOnlyList<ProfileTagDetailsResponse> Tags);
