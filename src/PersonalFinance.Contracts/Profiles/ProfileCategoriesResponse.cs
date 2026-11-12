namespace PersonalFinance.Contracts.Profiles;

public sealed record ProfileCategoriesResponse(Guid ProfileId, string Role, IReadOnlyList<ProfileCategoryResponse> Categories);

public sealed record ProfileCategoryResponse(Guid Id, string Name, string Kind, string? Comment, string Version,
    DateTimeOffset? TrackingStoppedAt, IReadOnlyList<ProfileSubcategoryResponse> Subcategories);

public sealed record ProfileSubcategoryResponse(Guid Id, Guid CategoryId, string Name, string? Comment,
    DateTimeOffset? TrackingStoppedAt, bool IsAvailableForTracking, string Version);

public sealed record CreateProfileCategoryRequest(string? Name, string? Comment);

public sealed record UpdateProfileCategoryRequest(string? Name, string? Comment, string? Version);

public sealed record CreatedProfileCategoryResponse(Guid Id);

public sealed record CreateProfileSubcategoryRequest(string? Name, string? Comment);

public sealed record UpdateProfileSubcategoryRequest(string? Name, string? Comment, string? Version);

public sealed record StopTrackingSubcategoryRequest(string? Version);

public sealed record StopTrackingCategoryRequest(string? Version);

public sealed record CreatedProfileSubcategoryResponse(Guid Id);
