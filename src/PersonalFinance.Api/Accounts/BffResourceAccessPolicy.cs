using OpenIddict.Abstractions;
using OpenIddict.Validation;
using PersonalFinance.Application.Accounts;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Validation.OpenIddictValidationEvents;

namespace PersonalFinance.Api.Accounts;

// Comment ID: CLASS-034. Enforces the configured BFF client, API scope, and current account eligibility for resource access.
public sealed class BffResourceAccessPolicy(IAccountSessionEligibility eligibility, IConfiguration configuration)
    : IOpenIddictValidationHandler<ProcessAuthenticationContext>
{
    // Comment ID: METHOD-052. Rejects validated access tokens whose client, scope, or account no longer permits API access.
    public async ValueTask HandleAsync(ProcessAuthenticationContext context)
    {
        var principal = context.AccessTokenPrincipal;
        if (principal is null) return;
        var clientId = configuration["Authentication:OpenIddict:ClientId"] ?? "personal-finance-client";
        if (!principal.HasPresenter(clientId) || !principal.HasScope("personal_finance_api") ||
            !Guid.TryParse(principal.GetClaim(Claims.Subject), out var accountId) ||
            !await eligibility.IsActiveAsync(accountId, context.CancellationToken))
            context.Reject(error: Errors.InvalidToken, description: "The access token is not valid for this resource.");
    }
}
