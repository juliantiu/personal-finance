namespace PersonalFinance.Application.Accounts;

// Do not attach provider exceptions: they can include recipients or confirmation URLs.
public sealed class ConfirmationEmailDeliveryException()
    : Exception("Confirmation email could not be delivered.");
