namespace PersonalFinance.Infrastructure.Email;

public sealed record DevelopmentEmailMessage(string Recipient, Uri ConfirmationLink);
