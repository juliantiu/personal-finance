using FluentValidation;

namespace PersonalFinance.Application.Accounts.RegisterAccount;

public sealed class RegisterAccountValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("Enter a valid email address.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Enter a password.")
            .MinimumLength(12).WithMessage("Use at least 12 characters.");

        RuleFor(command => command.ConfirmPassword)
            .Equal(command => command.Password)
            .WithMessage("Password confirmation must match.");
    }
}
