namespace PersonalFinance.Api.Accounts;

public sealed record RegisterAccountRequest(string Email, string Password, string ConfirmPassword);

public sealed record ConfirmEmailRequest(Guid AccountId, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record AccountRequestAcknowledgement(string Message);

public sealed record ConfirmEmailResponse(bool Confirmed);
