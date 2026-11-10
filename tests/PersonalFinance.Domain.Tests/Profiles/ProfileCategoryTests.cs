using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Domain.Tests.Profiles;

public sealed class ProfileCategoryTests
{
    [Fact]
    public void Ordinary_category_metadata_is_normalized_and_updates_keep_identity_and_kind()
    {
        var profileId = Guid.NewGuid();
        var category = ProfileCategory.CreateOrdinary(profileId, "  Food  ", "  Weekly groceries  ");

        var updated = category.ChangeMetadata(" Home ", "   ");

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal(profileId, category.ProfileId);
        Assert.Equal("ordinary", category.Kind);
        Assert.Equal(category.Id, updated.Id);
        Assert.Equal(category.ProfileId, updated.ProfileId);
        Assert.Equal(category.Kind, updated.Kind);
        Assert.Equal("Home", updated.Name);
        Assert.Null(updated.Comment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Category_name_must_be_nonblank(string? name)
    {
        Assert.Throws<ArgumentException>(() => ProfileCategory.CreateOrdinary(Guid.NewGuid(), name, null));
    }

    [Fact]
    public void Category_name_and_comment_limits_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => ProfileCategory.CreateOrdinary(Guid.NewGuid(), new string('x', 101), null));
        Assert.Throws<ArgumentException>(() => ProfileCategory.CreateOrdinary(Guid.NewGuid(), "Food", new string('x', 2001)));
    }
}
