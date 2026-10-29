using System.Net.Sockets;
using System.Security.Authentication;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Infrastructure.Email;

public sealed class Smtp4DevEmailTransport(
    IOptions<Smtp4DevOptions> options,
    ILogger<Smtp4DevEmailTransport> logger) : IEmailTransport
{
    public async Task SendAsync(OutgoingEmailMessage email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var client = new SmtpClient();
            var settings = options.Value;
            await client.ConnectAsync(settings.Host, settings.Port, settings.SecureSocketOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(email.SenderName, email.SenderAddress));
            message.To.Add(MailboxAddress.Parse(email.Recipient));
            message.Subject = email.Subject;
            message.Body = new BodyBuilder
            {
                TextBody = email.PlainTextBody,
                HtmlBody = email.HtmlBody
            }.ToMessageBody();

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsKnownSmtpFailure(exception))
        {
            logger.LogWarning("Local SMTP email submission failed ({FailureType}).", exception.GetType().Name);
            throw new ConfirmationEmailDeliveryException();
        }
    }

    private static bool IsKnownSmtpFailure(Exception exception) =>
        exception is SmtpCommandException or
            SmtpProtocolException or
            MailKit.Security.AuthenticationException or
            AuthenticationException or
            SocketException or
            IOException;
}
