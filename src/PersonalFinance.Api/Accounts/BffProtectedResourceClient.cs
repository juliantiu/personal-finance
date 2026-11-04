using System.Net.Http.Headers;
using PersonalFinance.Application.Accounts;

namespace PersonalFinance.Api.Accounts;

// Comment ID: CLASS-025. Defines the API-side HTTP boundary for server-to-resource BFF requests.
public interface IBffProtectedResourceClient
{
    // Comment ID: METHOD-041. Sends a relative resource request with the current BFF session's server-held access token.
    Task<HttpResponseMessage?> SendAsync(string sessionId, HttpRequestMessage request, CancellationToken cancellationToken);
}

// Comment ID: CLASS-026. Adds BFF-held bearer tokens to relative server-to-resource HTTP requests.
public sealed class BffProtectedResourceClient(
    HttpClient httpClient,
    IBffSessionTokenProvider tokenProvider) : IBffProtectedResourceClient
{
    // Comment ID: METHOD-042. Resolves a current access token and sends it only to the configured resource origin.
    public async Task<HttpResponseMessage?> SendAsync(
        string sessionId,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var baseAddress = httpClient.BaseAddress;
        if (request.RequestUri is null || request.RequestUri.IsAbsoluteUri || baseAddress is null ||
            !Uri.TryCreate(baseAddress, request.RequestUri, out var destination) ||
            !string.Equals(destination.GetLeftPart(UriPartial.Authority), baseAddress.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Resource requests must use a relative URI.", nameof(request));

        var accessToken = await tokenProvider.GetAccessTokenAsync(sessionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
