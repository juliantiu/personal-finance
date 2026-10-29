using PersonalFinance.Contracts.Accounts;

namespace PersonalFinance.Api.Accounts;

public sealed record ConfirmEmailRequest(Guid AccountId, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record ConfirmEmailResponse(bool Confirmed);
