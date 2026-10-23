namespace PersonalFinance.Application.Accounts;

public interface IConfirmationEmailSender
{
    /// <exception cref="ConfirmationEmailDeliveryException">Known delivery failure, recorded safely by the adapter.</exception>
    Task SendAsync(EmailConfirmation confirmation, CancellationToken cancellationToken);
}
