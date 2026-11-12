namespace PersonalFinance.Contracts.Profiles;

public sealed record ProfilePaymentMethodsResponse(
    Guid ProfileId,
    string ProfileName,
    string Role,
    IReadOnlyList<ProfilePaymentMethodDetailsResponse> Methods);
