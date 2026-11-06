// Comment ID: CLASS-058. Carries only caller-selected setup values; the acting account is resolved separately on the server.
using FluentValidation;
using Mediator;
using PersonalFinance.Domain.Profiles;

namespace PersonalFinance.Application.Profiles;

public sealed record CreateProfileCommand(string? Name, string? MainCurrency, IReadOnlyList<string>? AdditionalCurrencies) : ICommand<Guid>;

public interface IProfileCreationStore
{
    Task SaveAsync(BudgetProfile profile, CancellationToken cancellationToken);
}

public sealed class CreateProfileValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Enter a profile name.")
            .Must(name => name is null || name.Trim().Length <= BudgetProfile.MaximumNameLength).WithMessage("Use at most 100 characters after trimming.");
        RuleFor(command => command.MainCurrency)
            .Must(BudgetProfile.IsSupportedCurrency).WithMessage("Select GBP or USD as the main currency.");
        RuleFor(command => command.AdditionalCurrencies)
            .Must((command, currencies) => currencies is null ||
                (currencies.All(currency => BudgetProfile.IsSupportedCurrency(currency) && currency != command.MainCurrency) && currencies.Distinct().Count() == currencies.Count))
            .WithMessage("Additional currencies must be supported, distinct, and different from the main currency.");
    }
}

public sealed class CreateProfileHandler(ICurrentAccount currentAccount, IProfileCreationStore store) : ICommandHandler<CreateProfileCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateProfileCommand command, CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken) ?? throw new UnauthenticatedAccountException();
        var profile = BudgetProfile.Create(accountId, command.Name, command.MainCurrency, command.AdditionalCurrencies);
        await store.SaveAsync(profile, cancellationToken);
        return profile.Id;
    }
}
