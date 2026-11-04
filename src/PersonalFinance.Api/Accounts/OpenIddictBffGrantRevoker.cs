using OpenIddict.Abstractions;
using OpenIddict.Server;
using PersonalFinance.Application.Accounts;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace PersonalFinance.Api.Accounts;

// Comment ID: CLASS-031. Resolves a protected session's authorization and revokes its entire OpenIddict token chain.
public sealed class OpenIddictBffGrantRevoker(
    IOpenIddictServerFactory factory,
    IOpenIddictServerDispatcher dispatcher,
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictTokenManager tokens) : IBffGrantRevoker
{
    // Comment ID: METHOD-048. Validates the stored access token for grant identification, allowing expiry solely for revocation.
    public async Task RevokeAsync(BffSession session, CancellationToken cancellationToken)
    {
        var transaction = await factory.CreateTransactionAsync();
        transaction.CancellationToken = cancellationToken;
        var context = new ValidateTokenContext(transaction)
        {
            Token = session.AccessToken,
            DisableLifetimeValidation = true,
            DisableAudienceValidation = true,
            DisablePresenterValidation = true,
            ValidTokenTypes = { TokenTypeIdentifiers.AccessToken }
        };
        await dispatcher.DispatchAsync(context);
        // A previously revoked token can still identify its signed grant; invalid signatures never produce a principal.
        var principal = context.Principal;
        var grantId = principal?.GetAuthorizationId();
        if (principal?.GetClaim(Claims.Subject) != session.AccountId.ToString() || string.IsNullOrWhiteSpace(grantId))
            throw new InvalidOperationException("The session grant could not be resolved.");

        var grant = await authorizations.FindByIdAsync(grantId, cancellationToken);
        if (grant is null) return;
        if (await authorizations.GetSubjectAsync(grant, cancellationToken) != session.AccountId.ToString())
            throw new InvalidOperationException("The session grant could not be resolved.");
        if (!await authorizations.TryRevokeAsync(grant, cancellationToken))
            throw new InvalidOperationException("The session grant could not be revoked.");
        await tokens.RevokeByAuthorizationIdAsync(grantId, cancellationToken);
    }
}
