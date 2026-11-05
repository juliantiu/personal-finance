using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class BffAuthenticationTests
{
    [Fact]
    public async Task Successful_credentials_create_an_opaque_login_cookie_and_protected_ticket()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        await AddUser(host, "verified@example.test", confirmed: true);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await SignInWithAntiforgery(client, "verified@example.test", "abcdefghijkl");

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", TestContext.Current.CancellationToken);
        var ticket = await host.WithDatabaseAsync(db => db.AuthenticationTickets.AsNoTracking().SingleAsync());
        var payload = Encoding.UTF8.GetString(ticket.ProtectedPayload);

        Assert.NotNull(session);
        Assert.False(session.Authenticated);
        Assert.DoesNotContain("verified@example.test", payload, StringComparison.Ordinal);
        Assert.Equal(64, ticket.Id.Length);
        Assert.True(ticket.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Credential_sign_in_requires_the_antiforgery_token()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        using var client = host.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/sign-in",
            new { email = "person@example.test", password = "abcdefghijkl" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
            cookies.Any(value => value.StartsWith("__Host-PF.Login=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Unknown_unverified_and_incorrect_credentials_share_the_generic_failure()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        await AddUser(host, "verified@example.test", confirmed: true);
        await AddUser(host, "unverified@example.test", confirmed: false);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = await GetRequestToken(client);
        var wrong = await PostCredentials(client, token, "verified@example.test", "wrong-password");
        var unknown = await PostCredentials(client, token, "missing@example.test", "wrong-password");
        var unverified = await PostCredentials(client, token, "unverified@example.test", "abcdefghijkl");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unverified.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await unknown.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(await wrong.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            await unverified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Credentialed_CORS_is_limited_to_the_configured_client_origin()
    {
        await using var host = new AccountTestHost("https://client.example.test");
        using var client = host.CreateClient();
        using var allowedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/auth/sign-in");
        allowedRequest.Headers.Add("Origin", "https://client.example.test");
        allowedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        using var allowed = await client.SendAsync(allowedRequest, TestContext.Current.CancellationToken);
        using var rejectedRequest = new HttpRequestMessage(HttpMethod.Options, "/api/auth/sign-in");
        rejectedRequest.Headers.Add("Origin", "https://evil.example.test");
        rejectedRequest.Headers.Add("Access-Control-Request-Method", "POST");
        using var rejected = await client.SendAsync(rejectedRequest, TestContext.Current.CancellationToken);

        Assert.Equal("true", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Equal("https://client.example.test", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(rejected.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static async Task SignInWithAntiforgery(HttpClient client, string email, string password)
    {
        var token = await GetRequestToken(client);
        using var response = await PostCredentials(client, token, email, password);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-PF.Login=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> GetRequestToken(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var payload = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(TestContext.Current.CancellationToken);
        return Assert.IsType<string>(payload?.RequestToken);
    }

    private static async Task<HttpResponseMessage> PostCredentials(HttpClient client, string token, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-in")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AddUser(AccountTestHost host, string email, bool confirmed)
    {
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
        var user = new AccountIdentityUser { UserName = email, Email = email, EmailConfirmed = confirmed };
        Assert.True((await manager.CreateAsync(user, "abcdefghijkl")).Succeeded);
    }

    private sealed record AntiforgeryResponse(string? RequestToken);
    private sealed record SessionResponse(bool Authenticated, string? Email);
}
