namespace PersonalFinance.Application.Accounts;

public sealed class EmailConfirmation(Guid accountId, string email, string token)
{
    public Guid AccountId { get; } = accountId;
    public string Email { get; } = email;
    public string Token { get; } = token;

    public override string ToString() => nameof(EmailConfirmation);
}
