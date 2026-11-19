using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record GrantProfileAccessCommand(Guid ProfileId, string? Email, string? Role) : ICommand<GrantProfileAccessResult>;

public sealed class GrantProfileAccessValidator : AbstractValidator<GrantProfileAccessCommand>
{
    public GrantProfileAccessValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Enter the account email address.")
            .MaximumLength(256).WithMessage("Use an email address no longer than 256 characters.")
            .Must(email => email is null || new EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("Enter a valid email address.");
        RuleFor(command => command.Role)
            .Must(role => role is "editor" or "view_only")
            .WithMessage("Select editor or view-only access.");
    }
}

public interface IProfileAccessGrantStore
{
    Task<GrantProfileAccessResult> GrantAsync(Guid accountId, Guid profileId, string email, string role,
        CancellationToken cancellationToken);
}

public enum GrantProfileAccessResult
{
    Granted,
    RecipientNotEligible,
    OwnerCannotReceiveGrant,
    NotOwner
}

public sealed class GrantProfileAccessHandler(ICurrentAccount currentAccount, IProfileAccessGrantStore store)
    : ICommandHandler<GrantProfileAccessCommand, GrantProfileAccessResult>
{
    public async ValueTask<GrantProfileAccessResult> Handle(GrantProfileAccessCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await store.GrantAsync(accountId, command.ProfileId, command.Email!, command.Role!, cancellationToken);
    }
}
