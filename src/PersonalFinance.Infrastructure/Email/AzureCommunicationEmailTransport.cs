using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Infrastructure.Email;

public sealed class AzureCommunicationEmailTransport(
    EmailClient emailClient,
    IOptions<ConfirmationEmailOptions> senderOptions,
    ILogger<AzureCommunicationEmailTransport> logger) : IEmailTransport
{
    public async Task SendAsync(OutgoingEmailMessage email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = new EmailContent(email.Subject)
        {
            PlainText = email.PlainTextBody,
            Html = email.HtmlBody
        };
        var message = new EmailMessage(senderOptions.Value.SenderAddress, email.Recipient, content);

        try
        {
            var operation = await emailClient.SendAsync(WaitUntil.Completed, message, cancellationToken);
            if (operation.HasValue && operation.Value.Status == EmailSendStatus.Failed)
            {
                logger.LogWarning("Azure Communication Services rejected an email submission.");
                throw new ConfirmationEmailDeliveryException();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RequestFailedException)
        {
            logger.LogWarning("Azure Communication Services email submission failed.");
            throw new ConfirmationEmailDeliveryException();
        }
    }
}
