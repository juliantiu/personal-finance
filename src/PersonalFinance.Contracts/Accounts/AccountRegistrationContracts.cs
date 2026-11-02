namespace PersonalFinance.Contracts.Accounts;

// Comment ID: CLASS-005. Carries the email and password submitted to the sign-in endpoint.
public sealed record SignInRequest(string Email, string Password);

public sealed record RegisterAccountRequest(string Email, string Password, string ConfirmPassword);

public sealed record AccountRequestAcknowledgement(string Message);

public sealed record ConfirmEmailRequest(Guid AccountId, string Token);

public sealed record ConfirmEmailResponse(bool Confirmed);

public sealed record ResendConfirmationRequest(string Email);
