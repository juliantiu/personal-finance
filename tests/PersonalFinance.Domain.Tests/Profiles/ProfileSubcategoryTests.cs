using PersonalFinance.Domain.Profiles;
using Xunit;

namespace PersonalFinance.Domain.Tests.Profiles;

public sealed class ProfileSubcategoryTests
{
    [Fact]
    public void Creation_and_metadata_updates_preserve_subcategory_and_parent_identity()
    {
        var categoryId = Guid.NewGuid();
        var subcategory = ProfileSubcategory.Create(categoryId, "  Groceries  ", "  Weekly\nshop  ");

        var updated = subcategory.ChangeMetadata(" Home ", "   ");

        Assert.NotEqual(Guid.Empty, subcategory.Id);
        Assert.Equal(categoryId, subcategory.CategoryId);
        Assert.Equal(subcategory.Id, updated.Id);
        Assert.Equal(categoryId, updated.CategoryId);
        Assert.Equal("Home", updated.Name);
        Assert.Null(updated.Comment);
    }

    [Fact]
    public void Subcategory_metadata_uses_its_own_type_and_preserves_interior_comment_lines()
    {
        var metadata = ProfileSubcategoryMetadata.Create("  Groceries  ", "  Weekly\nshop  ");

        Assert.IsType<ProfileSubcategoryMetadata>(metadata);
        Assert.Equal("Groceries", metadata.Name);
        Assert.Equal("Weekly\nshop", metadata.Comment);
        Assert.Equal(100, ProfileSubcategoryMetadata.MaximumNameLength);
        Assert.Equal(2000, ProfileSubcategoryMetadata.MaximumCommentLength);
    }

    [Fact]
    public void Subcategory_metadata_enforces_its_own_name_and_comment_limits()
    {
        Assert.Throws<ArgumentException>(() => ProfileSubcategoryMetadata.Create(" \t ", null));
        Assert.Throws<ArgumentException>(() => ProfileSubcategoryMetadata.Create(new string('x', 101), null));
        Assert.Throws<ArgumentException>(() => ProfileSubcategoryMetadata.Create("Food", new string('x', 2001)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Subcategory_name_must_be_nonblank(string? name)
    {
        Assert.Throws<ArgumentException>(() => ProfileSubcategory.Create(Guid.NewGuid(), name, null));
    }

    [Fact]
    public void Subcategory_name_and_comment_limits_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => ProfileSubcategory.Create(Guid.NewGuid(), new string('x', 101), null));
        Assert.Throws<ArgumentException>(() => ProfileSubcategory.Create(Guid.NewGuid(), "Food", new string('x', 2001)));
    }

    [Fact]
    public void A_subcategory_requires_a_parent_category()
    {
        Assert.Throws<ArgumentException>(() => ProfileSubcategory.Create(Guid.Empty, "Food", null));
    }

    [Fact]
    public void Stopping_tracking_records_once_and_metadata_cannot_change_afterward()
    {
        var stoppedAt = new DateTimeOffset(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);
        var subcategory = ProfileSubcategory.Create(Guid.NewGuid(), "Food", "weekly");

        var stopped = subcategory.StopTracking(stoppedAt);
        var stoppedAgain = stopped.StopTracking(stoppedAt.AddHours(1));

        Assert.Equal(stoppedAt, stopped.TrackingStoppedAt);
        Assert.Equal(stoppedAt, stoppedAgain.TrackingStoppedAt);
        Assert.Throws<InvalidOperationException>(() => { stopped.ChangeMetadata("Groceries", null); });
    }
}
