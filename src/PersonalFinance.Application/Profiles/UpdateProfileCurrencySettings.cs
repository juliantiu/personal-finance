using FluentValidation;
using Mediator;
using PersonalFinance.Domain.Profiles;

namespace PersonalFinance.Application.Profiles;

public sealed record UpdateProfileCurrencySettingsCommand(
    Guid ProfileId,
    IReadOnlyList<string>? AdditionalCurrencies,
    long ConfigurationVersion) : ICommand<ProfileCurrencySettingsUpdateResult>;

public enum ProfileCurrencySettingsUpdateResult
{
    Updated,
    NotFound,
    Forbidden,
    Invalid,
    Conflict
}

public interface IProfileCurrencySettingsUpdater
{
    Task<ProfileCurrencySettingsUpdateResult> UpdateAsync(
        Guid accountId,
        Guid profileId,
        IReadOnlyList<string>? additionalCurrencies,
        long expectedVersion,
        CancellationToken cancellationToken);
}

public sealed class UpdateProfileCurrencySettingsValidator : AbstractValidator<UpdateProfileCurrencySettingsCommand>
{
    public UpdateProfileCurrencySettingsValidator()
    {
        RuleFor(command => command.ProfileId).NotEmpty();
        RuleFor(command => command.ConfigurationVersion).GreaterThanOrEqualTo(0);
        RuleFor(command => command.AdditionalCurrencies)
            .Must(currencies => currencies is null || currencies.Count <= 1 && currencies.All(BudgetProfile.IsSupportedCurrency))
            .WithMessage("Choose no additional currency or one supported currency.");
    }
}

public sealed class UpdateProfileCurrencySettingsHandler(
    ICurrentAccount currentAccount,
    IProfileCurrencySettingsUpdater updater) : ICommandHandler<UpdateProfileCurrencySettingsCommand, ProfileCurrencySettingsUpdateResult>
{
    public async ValueTask<ProfileCurrencySettingsUpdateResult> Handle(
        UpdateProfileCurrencySettingsCommand command,
        CancellationToken cancellationToken)
    {
        var accountId = await currentAccount.GetAccountIdAsync(cancellationToken)
            ?? throw new UnauthenticatedAccountException();
        return await updater.UpdateAsync(accountId, command.ProfileId, command.AdditionalCurrencies, command.ConfigurationVersion, cancellationToken);
    }
}
