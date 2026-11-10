namespace PersonalFinance.Domain.Profiles;

public sealed record ProfileCategoryMetadata(string Name, string? Comment)
{
    public static ProfileCategoryMetadata Create(string? name, string? comment)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrEmpty(normalizedName) || normalizedName.Length > ProfileCategory.MaximumNameLength)
            throw new ArgumentException("Enter a category name of up to 100 characters.", nameof(name));

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalizedComment?.Length > ProfileCategory.MaximumCommentLength)
            throw new ArgumentException("Use at most 2,000 characters for a comment.", nameof(comment));
        return new ProfileCategoryMetadata(normalizedName, normalizedComment);
    }
}

public sealed class ProfileCategory
{
    public const int MaximumNameLength = 100;
    public const int MaximumCommentLength = 2000;

    private ProfileCategory(Guid id, Guid profileId, string name, string? comment, string kind)
    {
        Id = id;
        ProfileId = profileId;
        Name = name;
        Comment = comment;
        Kind = kind;
    }

    public Guid Id { get; }
    public Guid ProfileId { get; }
    public string Name { get; }
    public string? Comment { get; }
    public string Kind { get; }

    public static ProfileCategory Restore(Guid id, Guid profileId, string name, string? comment, string kind)
    {
        if (id == Guid.Empty || profileId == Guid.Empty) throw new ArgumentException("A category and profile are required.");
        return new ProfileCategory(id, profileId, name, comment, kind);
    }

    public static ProfileCategory CreateOrdinary(Guid profileId, string? name, string? comment)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("A profile is required.", nameof(profileId));
        var metadata = ProfileCategoryMetadata.Create(name, comment);
        return new ProfileCategory(Guid.NewGuid(), profileId, metadata.Name, metadata.Comment, "ordinary");
    }

    public ProfileCategory ChangeMetadata(string? name, string? comment) 
    {
        var metadata = ProfileCategoryMetadata.Create(name, comment);
        return new ProfileCategory(Id, ProfileId, metadata.Name, metadata.Comment, Kind);
    }

}
