using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using Xunit;
using DomainProfileCategory = PersonalFinance.Domain.Profiles.ProfileCategory;

namespace PersonalFinance.Application.Tests.Profiles;

public sealed class MaintainProfileCategoriesTests
{
    [Fact]
    public async Task Create_resolves_current_account_and_sends_normalized_ordinary_category_to_writer()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var writer = new Writer();
        var handler = new CreateProfileCategoryHandler(new CurrentAccount(accountId), writer);

        var id = await handler.Handle(new CreateProfileCategoryCommand(profileId, "  Home  ", "  Shared budget  "), TestContext.Current.CancellationToken);

        Assert.Equal(accountId, writer.AccountId);
        Assert.Equal(profileId, writer.ProfileId);
        Assert.Equal(id, writer.Category!.Id);
        Assert.Equal("ordinary", writer.Category.Kind);
        Assert.Equal("Home", writer.Category.Name);
        Assert.Equal("Shared budget", writer.Category.Comment);
    }

    [Fact]
    public async Task Update_forwards_profile_item_identity_normalized_metadata_and_expected_version()
    {
        var accountId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var expectedVersion = new byte[] { 2, 4, 8 };
        var writer = new Writer { UpdateResult = UpdateProfileCategoryResult.Conflict };
        var handler = new UpdateProfileCategoryHandler(new CurrentAccount(accountId), writer);

        var result = await handler.Handle(new UpdateProfileCategoryCommand(profileId, categoryId, " Pay ", "   ", Convert.ToBase64String(expectedVersion)),
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateProfileCategoryResult.Conflict, result);
        Assert.Equal(accountId, writer.AccountId);
        Assert.Equal(profileId, writer.ProfileId);
        Assert.Equal(categoryId, writer.CategoryId);
        Assert.Equal("Pay", writer.Metadata!.Name);
        Assert.Null(writer.Metadata.Comment);
        Assert.Equal(expectedVersion, writer.ExpectedVersion);
    }

    [Fact]
    public async Task Missing_account_is_rejected_before_category_writer_is_called()
    {
        var writer = new Writer();
        var handler = new CreateProfileCategoryHandler(new CurrentAccount(null), writer);

        await Assert.ThrowsAsync<UnauthenticatedAccountException>(() => handler.Handle(
            new CreateProfileCategoryCommand(Guid.NewGuid(), "Home", null), TestContext.Current.CancellationToken).AsTask());

        Assert.Null(writer.AccountId);
    }

    private sealed class CurrentAccount(Guid? id) : ICurrentAccount
    {
        public Task<Guid?> GetAccountIdAsync(CancellationToken cancellationToken) => Task.FromResult(id);
    }

    private sealed class Writer : IProfileCategoryWriter
    {
        public Guid? AccountId { get; private set; }
        public Guid ProfileId { get; private set; }
        public Guid CategoryId { get; private set; }
        public DomainProfileCategory? Category { get; private set; }
        public ProfileCategoryMetadata? Metadata { get; private set; }
        public byte[]? ExpectedVersion { get; private set; }
        public UpdateProfileCategoryResult UpdateResult { get; init; } = UpdateProfileCategoryResult.Updated;

        public Task<Guid?> CreateAsync(Guid accountId, Guid profileId, DomainProfileCategory category, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            Category = category;
            return Task.FromResult<Guid?>(category.Id);
        }

        public Task<UpdateProfileCategoryResult> UpdateAsync(Guid accountId, Guid profileId, Guid categoryId,
            ProfileCategoryMetadata metadata, byte[] expectedVersion, CancellationToken cancellationToken)
        {
            AccountId = accountId;
            ProfileId = profileId;
            CategoryId = categoryId;
            Metadata = metadata;
            ExpectedVersion = expectedVersion;
            return Task.FromResult(UpdateResult);
        }
    }
}
