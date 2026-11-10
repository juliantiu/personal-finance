namespace PersonalFinance.Contracts.Profiles;

public sealed record ProfileCategoriesResponse(Guid ProfileId, string Role, IReadOnlyList<ProfileCategoryResponse> Categories);

public sealed record ProfileCategoryResponse(Guid Id, string Name, string Kind, string? Comment, string Version);

public sealed record CreateProfileCategoryRequest(string? Name, string? Comment);

public sealed record UpdateProfileCategoryRequest(string? Name, string? Comment, string? Version);

public sealed record CreatedProfileCategoryResponse(Guid Id);
