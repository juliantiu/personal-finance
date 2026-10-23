using Mediator;

namespace PersonalFinance.Application.Accounts.RegisterAccount;

public sealed record RegisterAccountCommand(
    string Email,
    string Password,
    string ConfirmPassword) : ICommand<AccountRequestAccepted>
{
    public override string ToString() => nameof(RegisterAccountCommand);
}
