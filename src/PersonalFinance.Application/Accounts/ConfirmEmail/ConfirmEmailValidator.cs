using FluentValidation;

namespace PersonalFinance.Application.Accounts.ConfirmEmail;

public sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty().WithMessage("A confirmation account is required.");
        RuleFor(command => command.Token).NotEmpty().WithMessage("A confirmation token is required.");
    }
}
