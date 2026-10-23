namespace PersonalFinance.Application.Accounts;

/// <summary>Identity operations implemented by Infrastructure, without exposing Identity types.</summary>
public interface IAccountIdentity
{
    /// <returns>Confirmation details for a new account, or null for an existing normalized email.</returns>
    Task<EmailConfirmation?> CreateAccountAsync(string email, string password, CancellationToken cancellationToken);

    /// <returns>Confirmation details for an unverified account, or null for an unknown/verified email.</returns>
    Task<EmailConfirmation?> CreateEmailConfirmationAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Validates the protected token for its account before confirming it. Replaying a still-valid
    /// token may succeed; an invalid token must fail even if the account is already confirmed.
    /// </summary>
    Task<bool> ConfirmEmailAsync(Guid accountId, string token, CancellationToken cancellationToken);
}
