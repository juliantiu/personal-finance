using Mediator;

namespace PersonalFinance.Application.Accounts.RegisterAccount;

public sealed class RegisterAccountHandler(
    IAccountIdentity identity,
    IConfirmationEmailSender emailSender) : ICommandHandler<RegisterAccountCommand, AccountRequestAccepted>
{
    public async ValueTask<AccountRequestAccepted> Handle(RegisterAccountCommand command, CancellationToken cancellationToken)
    {
        var confirmation = await identity.CreateAccountAsync(command.Email, command.Password, cancellationToken);
        if (confirmation is not null)
        {
            try
            {
                await emailSender.SendAsync(confirmation, cancellationToken);
            }
            catch (ConfirmationEmailDeliveryException)
            {
                // The unverified account remains recoverable through resend. The public response
                // acknowledges the request, never promises account creation or email delivery.
            }
        }

        return AccountRequestAccepted.Instance;
    }
}
