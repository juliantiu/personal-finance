namespace PersonalFinance.Domain.Profiles;

public sealed record ProfileSubcategoryMetadata(string Name, string? Comment)
{
    public const int MaximumNameLength = 100;
    public const int MaximumCommentLength = 2000;

    public static ProfileSubcategoryMetadata Create(string? name, string? comment)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrEmpty(normalizedName) || normalizedName.Length > MaximumNameLength)
            throw new ArgumentException("Enter a subcategory name of up to 100 characters.", nameof(name));

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalizedComment?.Length > MaximumCommentLength)
            throw new ArgumentException("Use at most 2,000 characters for a comment.", nameof(comment));

        return new ProfileSubcategoryMetadata(normalizedName, normalizedComment);
    }
}
