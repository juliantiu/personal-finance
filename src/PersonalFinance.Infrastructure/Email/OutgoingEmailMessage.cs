namespace PersonalFinance.Infrastructure.Email;

public sealed record OutgoingEmailMessage(
    string Recipient,
    string SenderAddress,
    string SenderName,
    string Subject,
    string PlainTextBody,
    string HtmlBody)
{
    public override string ToString() => nameof(OutgoingEmailMessage);
}
