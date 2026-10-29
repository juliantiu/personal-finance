namespace PersonalFinance.Infrastructure.Email;

public interface IEmailTransport
{
    Task SendAsync(OutgoingEmailMessage message, CancellationToken cancellationToken);
}
