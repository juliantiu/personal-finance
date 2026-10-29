using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Infrastructure.Email;

public sealed class ConfirmationEmailSender(
    IOptions<ConfirmationEmailOptions> options,
    IEmailTransport transport) : IConfirmationEmailSender
{
    public Task SendAsync(EmailConfirmation confirmation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var clientBase = new Uri(options.Value.ClientBaseUrl, UriKind.Absolute);
        var link = new UriBuilder(new Uri(clientBase, "confirm-email"))
        {
            Fragment = $"accountId={Uri.EscapeDataString(confirmation.AccountId.ToString())}" +
                       $"&token={Uri.EscapeDataString(confirmation.Token)}"
        }.Uri;

        var textLink = link.ToString();
        var htmlLink = HtmlEncoder.Default.Encode(textLink);
        var message = new OutgoingEmailMessage(
            confirmation.Email,
            options.Value.SenderAddress,
            options.Value.SenderName,
            "Confirm your email address",
            $"Confirm your email address by opening this link:\r\n\r\n{textLink}\r\n\r\n" +
            "This link expires after 24 hours.",
            "<!doctype html><html><body><p>Confirm your email address by opening this link:</p>" +
            $"<p><a href=\"{htmlLink}\">Confirm email address</a></p>" +
            "<p>This link expires after 24 hours.</p></body></html>");

        return transport.SendAsync(message, cancellationToken);
    }
}
