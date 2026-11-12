using System.Data;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Domain.Profiles;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using DomainProfileCategory = PersonalFinance.Domain.Profiles.ProfileCategory;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileCategoryWriter(AccountIdentityDbContext database) : IProfileCategoryWriter
{
    public async Task<Guid?> CreateAsync(Guid accountId, Guid profileId, DomainProfileCategory category, CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(accountId, profileId, cancellationToken)) return null;
        database.Categories.Add(new CategoryRecord
        {
            Id = category.Id,
            ProfileId = category.ProfileId,
            Name = category.Name,
            Comment = category.Comment,
            Kind = category.Kind
        });
        await database.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task<UpdateProfileCategoryResult> UpdateAsync(Guid accountId, Guid profileId, Guid categoryId,
        ProfileCategoryMetadata metadata, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EfProfileCategoryWriteLock.AcquireAsync(database, categoryId, cancellationToken);
        if (!await CanEditAsync(accountId, profileId, cancellationToken)) return UpdateProfileCategoryResult.NotFound;
        var record = await database.Categories.SingleOrDefaultAsync(category =>
            category.ProfileId == profileId && category.Id == categoryId, cancellationToken);
        if (record is null) return UpdateProfileCategoryResult.NotFound;
        if (record.TrackingStoppedAt is not null) return UpdateProfileCategoryResult.NotFound;
        if (!record.Version.SequenceEqual(expectedVersion)) return UpdateProfileCategoryResult.Conflict;

        // Why is the next line necessary, I mean, why instantiate create DomainProfileCategory if all we are going to do is change the record name and comment?
        var category = DomainProfileCategory.Restore(record.Id, record.ProfileId, record.Name, record.Comment, record.Kind)
            .ChangeMetadata(metadata.Name, metadata.Comment);
        record.Name = category.Name;
        record.Comment = category.Comment;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return UpdateProfileCategoryResult.Updated;
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateProfileCategoryResult.Conflict;
        }
    }

    public async Task<StopTrackingProfileCategoryResult> StopTrackingAsync(Guid accountId, Guid profileId,
        Guid categoryId, byte[] expectedVersion, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await EfProfileCategoryWriteLock.AcquireAsync(database, categoryId, cancellationToken);
        if (!await CanEditAsync(accountId, profileId, cancellationToken)) return StopTrackingProfileCategoryResult.NotFound;
        var record = await database.Categories.SingleOrDefaultAsync(category =>
            category.ProfileId == profileId && category.Id == categoryId, cancellationToken);
        if (record is null) return StopTrackingProfileCategoryResult.NotFound;
        if (record.Kind != "ordinary") return StopTrackingProfileCategoryResult.Protected;
        if (!record.Version.SequenceEqual(expectedVersion)) return StopTrackingProfileCategoryResult.Conflict;
        if (record.TrackingStoppedAt is not null) return StopTrackingProfileCategoryResult.Stopped;

        var category = DomainProfileCategory.Restore(record.Id, record.ProfileId, record.Name, record.Comment,
            record.Kind, record.TrackingStoppedAt).StopTracking(DateTimeOffset.UtcNow);
        record.TrackingStoppedAt = category.TrackingStoppedAt;
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return StopTrackingProfileCategoryResult.Stopped;
        }
        catch (DbUpdateConcurrencyException)
        {
            return StopTrackingProfileCategoryResult.Conflict;
        }
    }

    private async Task<bool> CanEditAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken) =>
        await database.BudgetProfiles.AnyAsync(profile => profile.Id == profileId && profile.OwnerAccountId == accountId, cancellationToken)
        || await database.ProfileAccess.AnyAsync(access => access.ProfileId == profileId && access.AccountId == accountId && access.Role == "editor", cancellationToken);
}
