using OpenIddict.Client;
using OpenIddict.Abstractions;
using static OpenIddict.Client.OpenIddictClientModels;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Api.Accounts;

// Comment ID: CLASS-024. Uses the registered public OpenIddict client to rotate BFF-held refresh tokens.
public sealed class OpenIddictBffTokenRefresher(
    OpenIddictClientService client,
    IConfiguration configuration) : IBffTokenRefresher
{
    // Comment ID: METHOD-040. Exchanges a refresh token with the configured issuer and returns only the server-held rotated token set.
    public async Task<BffTokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var issuer = configuration["Authentication:OpenIddict:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(refreshToken)) return null;

        try
        {
            var result = await client.AuthenticateWithRefreshTokenAsync(new RefreshTokenAuthenticationRequest
            {
                Issuer = new Uri(issuer, UriKind.Absolute),
                ProviderName = "PersonalFinance",
                RefreshToken = refreshToken,
                DisableUserInfo = true,
                CancellationToken = cancellationToken
            });

            return string.IsNullOrWhiteSpace(result.AccessToken) || result.AccessTokenExpirationDate is not { } expiresAt
                ? null
                : new BffTokenSet(result.AccessToken, expiresAt, result.RefreshToken);
        }
        catch (OpenIddictExceptions.ProtocolException)
        {
            return null;
        }
    }
}
