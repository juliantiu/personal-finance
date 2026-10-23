using Mediator;

namespace PersonalFinance.Application.Accounts.ResendConfirmation;

public sealed record ResendConfirmationCommand(string Email) : ICommand<AccountRequestAccepted>
{
    public override string ToString() => nameof(ResendConfirmationCommand);
}
