namespace PersonalFinance.Application.Accounts;

// Registration and resend deliberately return the same public result for existing accounts.
public sealed record AccountRequestAccepted
{
    public static AccountRequestAccepted Instance { get; } = new();

    private AccountRequestAccepted() { }
}
