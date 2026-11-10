namespace PersonalFinance.Contracts.Profiles;

public sealed record ProfileCategoriesResponse(Guid ProfileId, string Role, IReadOnlyList<ProfileCategoryResponse> Categories);

public sealed record ProfileCategoryResponse(Guid Id, string Name, string Kind);
