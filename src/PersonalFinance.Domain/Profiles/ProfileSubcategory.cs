namespace PersonalFinance.Domain.Profiles;

public sealed class ProfileSubcategory
{
    private ProfileSubcategory(Guid id, Guid categoryId, string name, string? comment)
    {
        Id = id;
        CategoryId = categoryId;
        Name = name;
        Comment = comment;
    }

    public Guid Id { get; }
    public Guid CategoryId { get; }
    public string Name { get; }
    public string? Comment { get; }

    public static ProfileSubcategory Restore(Guid id, Guid categoryId, string name, string? comment)
    {
        if (id == Guid.Empty || categoryId == Guid.Empty)
            throw new ArgumentException("A subcategory and parent category are required.");
        var metadata = ProfileSubcategoryMetadata.Create(name, comment);
        return new ProfileSubcategory(id, categoryId, metadata.Name, metadata.Comment);
    }

    public static ProfileSubcategory Create(Guid categoryId, string? name, string? comment)
    {
        if (categoryId == Guid.Empty) throw new ArgumentException("A parent category is required.", nameof(categoryId));
        var metadata = ProfileSubcategoryMetadata.Create(name, comment);
        return new ProfileSubcategory(Guid.NewGuid(), categoryId, metadata.Name, metadata.Comment);
    }

    public ProfileSubcategory ChangeMetadata(string? name, string? comment)
    {
        var metadata = ProfileSubcategoryMetadata.Create(name, comment);
        return new ProfileSubcategory(Id, CategoryId, metadata.Name, metadata.Comment);
    }
}
