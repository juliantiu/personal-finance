using Mediator;

namespace PersonalFinance.Application.Accounts.ResendConfirmation;

public sealed class ResendConfirmationHandler(
    IAccountIdentity identity,
    IConfirmationEmailSender emailSender) : ICommandHandler<ResendConfirmationCommand, AccountRequestAccepted>
{
    public async ValueTask<AccountRequestAccepted> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        var confirmation = await identity.CreateEmailConfirmationAsync(command.Email, cancellationToken);
        if (confirmation is not null)
        {
            try
            {
                await emailSender.SendAsync(confirmation, cancellationToken);
            }
            catch (ConfirmationEmailDeliveryException)
            {
                // Match registration's non-disclosing acknowledgement. Delivery failures are
                // recorded safely by the sender; they do not delete the existing account.
            }
        }

        return AccountRequestAccepted.Instance;
    }
}
