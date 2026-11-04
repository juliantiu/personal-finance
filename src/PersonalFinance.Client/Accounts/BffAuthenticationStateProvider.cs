using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace PersonalFinance.Client.Accounts;

// Comment ID: CLASS-010. Resolves Blazor authentication state from the BFF without receiving OAuth tokens.
public sealed class BffAuthenticationStateProvider(HttpClient httpClient) : AuthenticationStateProvider
{
    // Comment ID: METHOD-051. Notifies protected routes and account controls after the BFF confirms session termination.
    public void NotifySignedOut() => NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));

    // Comment ID: METHOD-017. Queries the cookie-backed BFF session endpoint and returns an anonymous principal on safe failures.
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/session");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return Anonymous();
            var session = await response.Content.ReadFromJsonAsync<SessionState>();
            if (session?.Authenticated != true) return Anonymous();

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, session.Email ?? "Signed in")], "BffSession");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (HttpRequestException)
        {
            return Anonymous();
        }
        catch (TaskCanceledException)
        {
            return Anonymous();
        }
    }

    // Comment ID: METHOD-029. Creates the unauthenticated client state used when the BFF session is absent or unavailable.
    private static AuthenticationState Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

    // Comment ID: CLASS-014. Carries the non-sensitive session status returned by the BFF endpoint.
    private sealed record SessionState(bool Authenticated, string? Email);
}
