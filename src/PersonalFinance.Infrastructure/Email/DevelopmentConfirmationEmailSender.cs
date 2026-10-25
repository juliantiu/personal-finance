using Microsoft.Extensions.Options;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Infrastructure.Email;

public sealed class DevelopmentConfirmationEmailSender(
    IOptions<DevelopmentEmailOptions> options,
    IDevelopmentEmailInbox inbox) : IConfirmationEmailSender
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

        inbox.Add(new DevelopmentEmailMessage(confirmation.Email, link));
        return Task.CompletedTask;
    }
}
