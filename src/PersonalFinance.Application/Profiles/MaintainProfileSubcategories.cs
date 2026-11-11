using FluentValidation;
using Mediator;
using PersonalFinance.Domain.Profiles;
using DomainProfileSubcategory = PersonalFinance.Domain.Profiles.ProfileSubcategory;

namespace PersonalFinance.Application.Profiles;

public sealed record CreateProfileSubcategoryCommand(Guid ProfileId, Guid CategoryId, string? Name, string? Comment) : ICommand<Guid?>;

public sealed class CreateProfileSubcategoryValidator : AbstractValidator<CreateProfileSubcategoryCommand>
{
    public CreateProfileSubcategoryValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.CategoryId).NotEqual(Guid.Empty);
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Enter a subcategory name.")
            .Must(name => name is null || name.Trim().Length <= ProfileSubcategoryMetadata.MaximumNameLength).WithMessage("Use at most 100 characters after trimming.");
        RuleFor(command => command.Comment)
            .Must(comment => comment is null || comment.Length <= ProfileSubcategoryMetadata.MaximumCommentLength).WithMessage("Use at most 2,000 characters for a comment.");
    }
}

public sealed record UpdateProfileSubcategoryCommand(Guid ProfileId, Guid CategoryId, Guid SubcategoryId,
    string? Name, string? Comment, string? Version) : ICommand<UpdateProfileSubcategoryResult>;

public sealed class UpdateProfileSubcategoryValidator : AbstractValidator<UpdateProfileSubcategoryCommand>
{
    public UpdateProfileSubcategoryValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.CategoryId).NotEqual(Guid.Empty);
        RuleFor(command => command.SubcategoryId).NotEqual(Guid.Empty);
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Enter a subcategory name.")
            .Must(name => name is null || name.Trim().Length <= ProfileSubcategoryMetadata.MaximumNameLength).WithMessage("Use at most 100 characters after trimming.");
        RuleFor(command => command.Comment)
            .Must(comment => comment is null || comment.Length <= ProfileSubcategoryMetadata.MaximumCommentLength).WithMessage("Use at most 2,000 characters for a comment.");
        RuleFor(command => command.Version).Must(IsVersion).WithMessage("Reload the subcategory before saving your changes.");
    }

    private static bool IsVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;
        try { return Convert.FromBase64String(version).Length > 0; }
        catch (FormatException) { return false; }
    }
}

public enum UpdateProfileSubcategoryResult { Updated, NotFound, Conflict }

public interface IProfileSubcategoryWriter
{
    Task<Guid?> CreateAsync(Guid accountId, Guid profileId, Guid categoryId, DomainProfileSubcategory subcategory, CancellationToken cancellationToken);

    Task<UpdateProfileSubcategoryResult> UpdateAsync(Guid accountId, Guid profileId, Guid categoryId, Guid subcategoryId,
        ProfileSubcategoryMetadata metadata, byte[] expectedVersion, CancellationToken cancellationToken);
}

public sealed class CreateProfileSubcategoryHandler(ICurrentAccount currentAccount, IProfileSubcategoryWriter writer)
    : ICommandHandler<CreateProfileSubcategoryCommand, Guid?>
{
    public async ValueTask<Guid?> Handle(CreateProfileSubcategoryCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        var subcategory = DomainProfileSubcategory.Create(command.CategoryId, command.Name, command.Comment);
        return await writer.CreateAsync(accountId, command.ProfileId, command.CategoryId, subcategory, cancellationToken);
    }
}

public sealed class UpdateProfileSubcategoryHandler(ICurrentAccount currentAccount, IProfileSubcategoryWriter writer)
    : ICommandHandler<UpdateProfileSubcategoryCommand, UpdateProfileSubcategoryResult>
{
    public async ValueTask<UpdateProfileSubcategoryResult> Handle(UpdateProfileSubcategoryCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        var metadata = ProfileSubcategoryMetadata.Create(command.Name, command.Comment);
        var version = Convert.FromBase64String(command.Version!);
        return await writer.UpdateAsync(accountId, command.ProfileId, command.CategoryId, command.SubcategoryId,
            metadata, version, cancellationToken);
    }
}
