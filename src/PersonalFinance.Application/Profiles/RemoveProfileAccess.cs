using FluentValidation;
using Mediator;

namespace PersonalFinance.Application.Profiles;

public sealed record RemoveProfileAccessCommand(Guid ProfileId, string? Email) : ICommand<RemoveProfileAccessResult>;

public sealed class RemoveProfileAccessValidator : AbstractValidator<RemoveProfileAccessCommand>
{
    public RemoveProfileAccessValidator()
    {
        RuleFor(command => command.ProfileId).NotEqual(Guid.Empty);
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Enter the account email address.")
            .MaximumLength(256).WithMessage("Use an email address no longer than 256 characters.");
    }
}

public interface IProfileAccessRemovalStore
{
    Task<RemoveProfileAccessResult> RemoveAsync(Guid accountId, Guid profileId, string email,
        CancellationToken cancellationToken);
}

public enum RemoveProfileAccessResult
{
    Removed,
    NoActiveAccess,
    NotOwner
}

public sealed class RemoveProfileAccessHandler(ICurrentAccount currentAccount, IProfileAccessRemovalStore store)
    : ICommandHandler<RemoveProfileAccessCommand, RemoveProfileAccessResult>
{
    public async ValueTask<RemoveProfileAccessResult> Handle(RemoveProfileAccessCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await store.RemoveAsync(accountId, command.ProfileId, command.Email!, cancellationToken);
    }
}
