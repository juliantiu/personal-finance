namespace PersonalFinance.Contracts.Accounts;

public sealed record RegisterAccountRequest(string Email, string Password, string ConfirmPassword);

public sealed record AccountRequestAcknowledgement(string Message);

public sealed record ConfirmEmailRequest(Guid AccountId, string Token);

public sealed record ConfirmEmailResponse(bool Confirmed);

public sealed record ResendConfirmationRequest(string Email);
