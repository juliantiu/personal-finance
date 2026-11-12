using FluentValidation;
using Mediator;
using PersonalFinance.Domain.Profiles;
using DomainProfileCategory = PersonalFinance.Domain.Profiles.ProfileCategory;

namespace PersonalFinance.Application.Profiles;

public sealed record CreateProfileCategoryCommand(Guid ProfileId, string? Name, string? Comment) : ICommand<Guid?>;

public sealed class CreateProfileCategoryValidator : AbstractValidator<CreateProfileCategoryCommand>
{
    public CreateProfileCategoryValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Enter a category name.")
            .Must(name => name is null || name.Trim().Length <= DomainProfileCategory.MaximumNameLength).WithMessage("Use at most 100 characters after trimming.");
        RuleFor(command => command.Comment)
            .Must(comment => comment is null || comment.Length <= DomainProfileCategory.MaximumCommentLength).WithMessage("Use at most 2,000 characters for a comment.");
    }
}

public sealed record UpdateProfileCategoryCommand(Guid ProfileId, Guid CategoryId, string? Name, string? Comment, string? Version) : ICommand<UpdateProfileCategoryResult>;

public sealed class UpdateProfileCategoryValidator : AbstractValidator<UpdateProfileCategoryCommand>
{
    public UpdateProfileCategoryValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.CategoryId).NotEqual(Guid.Empty);
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Enter a category name.")
            .Must(name => name is null || name.Trim().Length <= DomainProfileCategory.MaximumNameLength).WithMessage("Use at most 100 characters after trimming.");
        RuleFor(command => command.Comment)
            .Must(comment => comment is null || comment.Length <= DomainProfileCategory.MaximumCommentLength).WithMessage("Use at most 2,000 characters for a comment.");
        RuleFor(command => command.Version).Must(IsVersion).WithMessage("Reload the category before saving your changes.");
    }

    private static bool IsVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try { return Convert.FromBase64String(version).Length > 0; }
        catch (FormatException) { return false; }
    }
}

public enum UpdateProfileCategoryResult { Updated, NotFound, Conflict }

public sealed record StopTrackingProfileCategoryCommand(Guid ProfileId, Guid CategoryId, string? Version)
    : ICommand<StopTrackingProfileCategoryResult>;

public sealed class StopTrackingProfileCategoryValidator : AbstractValidator<StopTrackingProfileCategoryCommand>
{
    public StopTrackingProfileCategoryValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.CategoryId).NotEqual(Guid.Empty);
        RuleFor(command => command.Version).Must(IsVersion).WithMessage("Reload the category before deleting it.");
    }

    private static bool IsVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try { return Convert.FromBase64String(version).Length > 0; }
        catch (FormatException) { return false; }
    }
}

public enum StopTrackingProfileCategoryResult { Stopped, Protected, NotFound, Conflict }

public interface IProfileCategoryWriter
{
    Task<Guid?> CreateAsync(Guid accountId, Guid profileId, DomainProfileCategory category, CancellationToken cancellationToken);

    Task<UpdateProfileCategoryResult> UpdateAsync(Guid accountId, Guid profileId, Guid categoryId,
        ProfileCategoryMetadata metadata, byte[] expectedVersion, CancellationToken cancellationToken);

    Task<StopTrackingProfileCategoryResult> StopTrackingAsync(Guid accountId, Guid profileId, Guid categoryId,
        byte[] expectedVersion, CancellationToken cancellationToken);
}

public sealed class CreateProfileCategoryHandler(ICurrentAccount currentAccount, IProfileCategoryWriter writer)
    : ICommandHandler<CreateProfileCategoryCommand, Guid?>
{
    public async ValueTask<Guid?> Handle(CreateProfileCategoryCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        var category = DomainProfileCategory.CreateOrdinary(command.ProfileId, command.Name, command.Comment);
        return await writer.CreateAsync(accountId, command.ProfileId, category, cancellationToken);
    }
}

public sealed class UpdateProfileCategoryHandler(ICurrentAccount currentAccount, IProfileCategoryWriter writer)
    : ICommandHandler<UpdateProfileCategoryCommand, UpdateProfileCategoryResult>
{
    public async ValueTask<UpdateProfileCategoryResult> Handle(UpdateProfileCategoryCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        var metadata = ProfileCategoryMetadata.Create(command.Name, command.Comment);
        var version = Convert.FromBase64String(command.Version!);
        return await writer.UpdateAsync(accountId, command.ProfileId, command.CategoryId, metadata, version, cancellationToken);
    }
}

public sealed class StopTrackingProfileCategoryHandler(ICurrentAccount currentAccount, IProfileCategoryWriter writer)
    : ICommandHandler<StopTrackingProfileCategoryCommand, StopTrackingProfileCategoryResult>
{
    public async ValueTask<StopTrackingProfileCategoryResult> Handle(StopTrackingProfileCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await writer.StopTrackingAsync(accountId, command.ProfileId, command.CategoryId,
            Convert.FromBase64String(command.Version!), cancellationToken);
    }
}
