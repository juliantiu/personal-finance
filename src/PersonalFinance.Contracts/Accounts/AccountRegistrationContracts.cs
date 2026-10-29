namespace PersonalFinance.Contracts.Accounts;

public sealed record RegisterAccountRequest(string Email, string Password, string ConfirmPassword);

public sealed record AccountRequestAcknowledgement(string Message);
