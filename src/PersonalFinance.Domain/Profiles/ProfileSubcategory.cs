namespace PersonalFinance.Domain.Profiles;

public sealed class ProfileSubcategory
{
    private ProfileSubcategory(Guid id, Guid categoryId, string name, string? comment, DateTimeOffset? trackingStoppedAt)
    {
        Id = id;
        CategoryId = categoryId;
        Name = name;
        Comment = comment;
        TrackingStoppedAt = trackingStoppedAt;
    }

    public Guid Id { get; }
    public Guid CategoryId { get; }
    public string Name { get; }
    public string? Comment { get; }
    public DateTimeOffset? TrackingStoppedAt { get; }

    public static ProfileSubcategory Restore(Guid id, Guid categoryId, string name, string? comment, DateTimeOffset? trackingStoppedAt = null)
    {
        if (id == Guid.Empty || categoryId == Guid.Empty)
            throw new ArgumentException("A subcategory and parent category are required.");
        var metadata = ProfileSubcategoryMetadata.Create(name, comment);
        return new ProfileSubcategory(id, categoryId, metadata.Name, metadata.Comment, trackingStoppedAt);
    }

    public static ProfileSubcategory Create(Guid categoryId, string? name, string? comment)
    {
        if (categoryId == Guid.Empty) throw new ArgumentException("A parent category is required.", nameof(categoryId));
        var metadata = ProfileSubcategoryMetadata.Create(name, comment);
        return new ProfileSubcategory(Guid.NewGuid(), categoryId, metadata.Name, metadata.Comment, null);
    }

    public ProfileSubcategory ChangeMetadata(string? name, string? comment)
    {
        if (TrackingStoppedAt is not null)
            throw new InvalidOperationException("A subcategory that has stopped tracking cannot be edited.");
        var metadata = ProfileSubcategoryMetadata.Create(name, comment);
        return new ProfileSubcategory(Id, CategoryId, metadata.Name, metadata.Comment, TrackingStoppedAt);
    }

    public ProfileSubcategory StopTracking(DateTimeOffset stoppedAt) => TrackingStoppedAt is not null
        ? this
        : new ProfileSubcategory(Id, CategoryId, Name, Comment, stoppedAt);
}
