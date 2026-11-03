using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PersonalFinance.Infrastructure.Accounts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersonalFinance.Api.Accounts;

// Comment ID: CLASS-009. Converts an authenticated Identity account into the authorization-code principal issued by OpenIddict.
[ApiController]
public sealed class AuthorizationController(SignInManager<AccountIdentityUser> signInManager) : ControllerBase
{
    [HttpGet("/connect/authorize")]
    // Comment ID: METHOD-016. Issues an authorization code only for a verified account authenticated by the login session.
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (authentication.Principal?.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var accountId = authentication.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(accountId))
            return Unauthorized();

        var user = await signInManager.UserManager.FindByIdAsync(accountId);
        if (user is null || !user.EmailConfirmed)
        {
            await signInManager.SignOutAsync();
            return Unauthorized();
        }

        var identity = new ClaimsIdentity(authenticationType: "OpenIddictServer");
        identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(Claims.Email, user.Email ?? string.Empty));
        identity.AddClaim(new Claim(Claims.Name, user.Email ?? string.Empty));
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(HttpContext.GetOpenIddictServerRequest()?.GetScopes() ?? []);
        principal.SetResources("personal_finance_api");
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email or Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => []
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
