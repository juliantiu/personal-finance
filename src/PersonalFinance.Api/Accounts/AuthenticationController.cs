using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Server.AspNetCore;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Contracts.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersonalFinance.Api.Accounts;

// Comment ID: CLASS-007. Runs the browser-facing credential, authorization-code callback, and opaque-session endpoints.
[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    UserManager<AccountIdentityUser> userManager,
    SignInManager<AccountIdentityUser> signInManager,
    IBffSessionStore sessions,
    IAntiforgery antiforgery,
    IConfiguration configuration,
    IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    private const string SessionCookieName = "__Host-PF.Session";
    private const string ReturnPathKey = "pf.return_path";

    [HttpGet("antiforgery")]
    // Comment ID: METHOD-024. Creates the antiforgery cookie and request token required before credential sign-in.
    public IActionResult AntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { requestToken = tokens.RequestToken });
    }

    [HttpPost("sign-in")]
    [EnableRateLimiting("SignIn")]
    // Comment ID: METHOD-012. Authenticates verified credentials and starts a short-lived server-side Identity login session.
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The sign-in request could not be validated. Refresh the page and try again.",
                Status = StatusCodes.Status400BadRequest
            });
        }
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return SignInFailed();

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded || !user.EmailConfirmed)
            return SignInFailed();

        await signInManager.SignInAsync(user, isPersistent: false);
        return NoContent();
    }

    [HttpGet("start")]
    // Comment ID: METHOD-013. Starts the authorization-code flow while carrying only a validated local client return path.
    public async Task<IActionResult> Start([FromQuery] string? returnPath, CancellationToken cancellationToken)
    {
        await EnsureClientRegisteredAsync(cancellationToken);
        var safePath = ValidateReturnPath(returnPath) ?? "/budget-profiles";
        var properties = new AuthenticationProperties();
        properties.Items[ReturnPathKey] = safePath;
        properties.Items[OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = "PersonalFinance";
        return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("callback")]
    [IgnoreAntiforgeryToken]
    // Comment ID: METHOD-014. Validates the OpenIddict callback, persists server-held tokens, and issues an opaque BFF cookie.
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        if (result.Principal?.Identity?.IsAuthenticated != true)
            return RedirectToClient("/login");

        var accountValue = result.Principal.FindFirst(Claims.Subject)?.Value ??
            result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(accountValue, out var accountId))
            return RedirectToClient("/login");

        var tokens = result.Properties?.GetTokens().ToDictionary(token => token.Name, token => token.Value,
            StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        tokens.TryGetValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken, out var accessToken);
        tokens.TryGetValue(OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken, out var refreshToken);
        tokens.TryGetValue(OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessTokenExpirationDate, out var expiresValue);
        if (string.IsNullOrWhiteSpace(accessToken) || !DateTimeOffset.TryParse(expiresValue, out var accessExpires))
            return RedirectToClient("/login");

        var sessionId = await sessions.CreateAsync(new BffSession(accountId, accessToken, refreshToken,
            accessExpires, DateTimeOffset.UtcNow.AddDays(7)), cancellationToken);
        Response.Cookies.Append(SessionCookieName, sessionId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            IsEssential = true
        });
        await signInManager.SignOutAsync();
        var returnPath = result.Properties?.Items.TryGetValue(ReturnPathKey, out var requestedPath) == true
            ? requestedPath
            : "/budget-profiles";
        return RedirectToClient(ValidateReturnPath(returnPath) ?? "/budget-profiles");
    }

    [HttpGet("session")]
    // Comment ID: METHOD-015. Reports whether the opaque browser session resolves without exposing stored token data.
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var sessionId = Request.Cookies[SessionCookieName];
        var session = await sessions.FindAsync(sessionId ?? string.Empty, cancellationToken);
        if (session is null)
            return Ok(new SessionResponse(false, null));

        var account = await userManager.FindByIdAsync(session.AccountId.ToString());
        return account is { EmailConfirmed: true }
            ? Ok(new SessionResponse(true, account.Email))
            : Ok(new SessionResponse(false, null));
    }

    [HttpPost("sign-out")]
    // Comment ID: METHOD-049. Validates browser intent and clears the opaque cookie only after session termination succeeds.
    public async Task<IActionResult> SignOutCurrentSession([FromServices] SignOutBffSession signOut, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException)
        {
            return BadRequest(new ProblemDetails { Title = "The sign-out request could not be validated. Refresh the page and try again.", Status = 400 });
        }

        try { await signOut.ExecuteAsync(Request.Cookies[SessionCookieName] ?? string.Empty, cancellationToken); }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ProblemDetails { Title = "We couldn’t complete sign-out. Please try again.", Status = 503 });
        }

        Response.Cookies.Delete(SessionCookieName, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.None, Path = "/", IsEssential = true
        });
        return NoContent();
    }

    // Comment ID: METHOD-025. Builds the same safe unauthorized response for every credential failure.
    private IActionResult SignInFailed() => Unauthorized(new ProblemDetails
    {
        Title = "Sign-in failed. Check your details and try again.",
        Status = StatusCodes.Status401Unauthorized
    });

    // Comment ID: METHOD-022. Registers the exact public PKCE client lazily after its identity database is available.
    private async Task EnsureClientRegisteredAsync(CancellationToken cancellationToken)
    {
        var clientId = configuration["Authentication:OpenIddict:ClientId"] ?? "personal-finance-client";
        var issuer = new Uri(configuration["Authentication:OpenIddict:Issuer"]!, UriKind.Absolute);
        var application = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "Personal Finance Blazor client",
            ClientType = OpenIddictConstants.ClientTypes.Public,
            RedirectUris = { new Uri(issuer, "api/auth/callback") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                OpenIddictConstants.Permissions.Prefixes.Scope + "personal_finance_api"
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        };
        if (application is null)
            await applicationManager.CreateAsync(descriptor, cancellationToken);
        else
        {
            var redirects = await applicationManager.GetRedirectUrisAsync(application, cancellationToken);
            if (redirects.Length != 1 || redirects[0] != new Uri(issuer, "api/auth/callback").AbsoluteUri)
                await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        }
    }

    // Comment ID: METHOD-026. Allows only a same-origin absolute path before carrying it through the authorization redirect.
    private string? ValidateReturnPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] != '/' || value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains('\\') || value.Any(char.IsControl) ||
            value.Contains("%2f", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("%5c", StringComparison.OrdinalIgnoreCase))
            return null;

        var clientOrigin = GetClientOrigin();
        if (!Uri.TryCreate(clientOrigin, value, out var target) || target.GetLeftPart(UriPartial.Authority) != clientOrigin.GetLeftPart(UriPartial.Authority))
            return null;
        return target.PathAndQuery + target.Fragment;
    }

    // Comment ID: METHOD-027. Resolves the single configured client origin used by the BFF callback redirect.
    private Uri GetClientOrigin()
    {
        var origin = configuration["Authentication:OpenIddict:ClientOrigin"] ??
            configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault();
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException("An exact Blazor client origin must be configured.");
    }

    // Comment ID: METHOD-028. Redirects the browser to a configured client origin and validated local path.
    private IActionResult RedirectToClient(string path) => Redirect(new Uri(GetClientOrigin(), path).ToString());

    // Comment ID: CLASS-013. Returns only the browser session status and optional display email from the BFF boundary.
    private sealed record SessionResponse(bool Authenticated, string? Email);
}
