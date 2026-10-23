using Mediator;

namespace PersonalFinance.Application.Accounts.ConfirmEmail;

public sealed record ConfirmEmailCommand(Guid AccountId, string Token) : ICommand<ConfirmEmailResult>
{
    public override string ToString() => nameof(ConfirmEmailCommand);
}
