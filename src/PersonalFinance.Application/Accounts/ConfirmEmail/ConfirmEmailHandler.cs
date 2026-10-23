using Mediator;

namespace PersonalFinance.Application.Accounts.ConfirmEmail;

public sealed class ConfirmEmailHandler(IAccountIdentity identity) : ICommandHandler<ConfirmEmailCommand, ConfirmEmailResult>
{
    public async ValueTask<ConfirmEmailResult> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
        => await identity.ConfirmEmailAsync(command.AccountId, command.Token, cancellationToken)
            ? ConfirmEmailResult.Confirmed
            : ConfirmEmailResult.InvalidOrExpired;
}
