using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Tests.Support;
using PersonalFinance.Infrastructure.Accounts;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class AccountBrowserTests
{
    [Fact]
    public async Task Https_session_survives_restart_with_persisted_credentials_and_rejects_forged_requests()
    {
        await using var session = await BrowserSession.StartAsync(services =>
            services.AddSingleton<IStartupFilter, SyntheticResourceStartupFilter>());
        await AddConfirmedUser(session.Api, "restart-browser@example.test");
        await Login(session.Page, session.ClientUrl, "restart-browser@example.test");
        var cookie = Assert.Single(await session.Page.Context.CookiesAsync(), value => value.Name == "__Host-PF.Session");
        Assert.True(cookie.HttpOnly && cookie.Secure);
        Assert.Equal(-1, cookie.Expires);
        Assert.Equal(SameSiteAttribute.None, cookie.SameSite);
        Assert.StartsWith("https://", session.ApiUrl);
        var before = await ReadSession(session.Api, cookie.Value);
        Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(session.Api, before.AccessToken));

        var forged = await session.Page.EvaluateAsync<int>("async api => (await fetch(api + 'api/auth/sign-out', { method: 'POST', credentials: 'include' })).status", session.ApiUrl);
        Assert.Equal(400, forged);
        Assert.True(await BrowserAuthenticated(session.Page, session.ApiUrl));
        var deniedPage = await session.Page.Context.NewPageAsync();
        await deniedPage.GotoAsync(session.ClientUrl.Replace("127.0.0.1", "localhost", StringComparison.Ordinal) + "/index.html");
        var denied = await deniedPage.EvaluateAsync<bool>("async api => { try { await fetch(api + 'api/auth/session', { credentials: 'include' }); return false; } catch { return true; } }", session.ApiUrl);
        Assert.True(denied);
        await deniedPage.CloseAsync();

        await session.RestartApiAsync();
        Assert.True(await BrowserAuthenticated(session.Page, session.ApiUrl));
        Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(session.Api, before.AccessToken));
        await ExpireAccessToken(session.Api, cookie.Value);
        using (var scope = session.Api.Services.CreateScope())
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>()
                .GetAccessTokenAsync(cookie.Value, TestContext.Current.CancellationToken));
        var after = await ReadSession(session.Api, cookie.Value);
        Assert.NotEqual(before.RefreshToken, after.RefreshToken);
        Assert.Equal(before.SessionExpiresAt, after.SessionExpiresAt);
        await session.Page.ReloadAsync();
        await Expect(session.Page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true })).ToBeVisibleAsync();
        await session.Page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
        await Expect(session.Page.Locator(".mud-alert")).ToContainTextAsync("You have been signed out.");
        Assert.Equal(HttpStatusCode.Unauthorized, await ResourceStatus(session.Api, after.AccessToken));
        Assert.Empty(session.PageErrors);
    }

    [Fact]
    public async Task Signing_out_revokes_rotated_tokens_and_preserves_a_second_browser_session()
    {
        await using var browser = await BrowserSession.StartAsync(services =>
            services.AddSingleton<IStartupFilter, SyntheticResourceStartupFilter>());
        await AddConfirmedUser(browser.Api, "signout-browser@example.test");
        var page = browser.Page;
        await Login(page, browser.ClientUrl, "signout-browser@example.test");
        var firstCookie = Assert.Single(await page.Context.CookiesAsync(), cookie => cookie.Name == "__Host-PF.Session");
        await using var secondContext = await page.Context.Browser!.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var secondPage = await secondContext.NewPageAsync();
        await Login(secondPage, browser.ClientUrl, "signout-browser@example.test");
        var secondCookie = Assert.Single(await secondContext.CookiesAsync(), cookie => cookie.Name == "__Host-PF.Session");
        Assert.NotEqual(firstCookie.Value, secondCookie.Value);

        var initial = await ReadSession(browser.Api, firstCookie.Value);
        await ExpireAccessToken(browser.Api, firstCookie.Value);
        using (var scope = browser.Api.Services.CreateScope())
        {
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>()
                .GetAccessTokenAsync(firstCookie.Value, TestContext.Current.CancellationToken));
        }
        var rotated = await ReadSession(browser.Api, firstCookie.Value);
        var other = await ReadSession(browser.Api, secondCookie.Value);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(browser.Api, initial.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(browser.Api, rotated.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(browser.Api, other.AccessToken));

        await page.RouteAsync("**/api/auth/sign-out", route => route.AbortAsync());
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("couldn’t complete sign-out");
        Assert.True(await BrowserAuthenticated(page, browser.ApiUrl));
        await page.UnrouteAsync("**/api/auth/sign-out");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/login?signedOut=true");
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("You have been signed out.");
        Assert.DoesNotContain(await page.Context.CookiesAsync(), cookie => cookie.Name == "__Host-PF.Session");
        Assert.False(await BrowserAuthenticated(page, browser.ApiUrl));
        Assert.True(await BrowserAuthenticated(secondPage, browser.ApiUrl));
        Assert.Equal(1, await browser.Api.WithDatabaseAsync(db => db.BffSessions.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(HttpStatusCode.Unauthorized, await ResourceStatus(browser.Api, initial.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, await ResourceStatus(browser.Api, rotated.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(browser.Api, other.AccessToken));

        using (var scope = browser.Api.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
            Assert.Null(await store.FindAsync(firstCookie.Value, TestContext.Current.CancellationToken));
            var provider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
            Assert.Null(await provider.GetAccessTokenAsync(firstCookie.Value, TestContext.Current.CancellationToken));
            var refresher = scope.ServiceProvider.GetRequiredService<IBffTokenRefresher>();
            Assert.Null(await refresher.RefreshAsync(initial.RefreshToken!, TestContext.Current.CancellationToken));
            Assert.Null(await refresher.RefreshAsync(rotated.RefreshToken!, TestContext.Current.CancellationToken));
        }
        await ExpireAccessToken(browser.Api, secondCookie.Value);
        using (var scope = browser.Api.Services.CreateScope())
        {
            var token = await scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>()
                .GetAccessTokenAsync(secondCookie.Value, TestContext.Current.CancellationToken);
            Assert.NotNull(token);
            Assert.Equal(HttpStatusCode.NoContent, await ResourceStatus(browser.Api, token));
        }
        await secondPage.ReloadAsync();
        await Expect(secondPage.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true })).ToBeVisibleAsync();
        Assert.Empty(browser.PageErrors);
    }

    [Fact]
    public async Task Verified_login_completes_PKCE_without_exposing_tokens_and_returns_to_local_page()
    {
        await using var session = await BrowserSession.StartAsync();
        await AddConfirmedUser(session.Api, "login-browser@example.test");
        var page = session.Page;
        await page.GotoAsync(session.ClientUrl + "/login?returnUrl=%2Fbudget-profiles");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("login-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();

        var expectedUrl = session.ClientUrl + "/budget-profiles";
        try { await Expect(page).ToHaveURLAsync(expectedUrl); }
        catch (Exception exception)
        {
            var currentUrl = new Uri(page.Url).GetLeftPart(UriPartial.Path);
            var alerts = "unavailable after navigation";
            try { alerts = string.Join(" | ", await page.Locator(".mud-alert").AllInnerTextsAsync()); }
            catch (Microsoft.Playwright.PlaywrightException) { }
            throw new InvalidOperationException($"Login did not complete. Current URL: {currentUrl}. Alerts: {alerts}. Responses: {string.Join(" | ", session.ApiResponses)}. Console: {string.Join(" | ", session.ConsoleErrors)}", exception);
        }
        var sessionStatus = await page.EvaluateAsync<bool>(
            "async apiBase => (await (await fetch(apiBase + 'api/auth/session', { credentials: 'include' })).json()).authenticated",
            session.ApiUrl);
        Assert.True(sessionStatus);
        var cookies = await page.Context.CookiesAsync();
        var sessionCookie = Assert.Single(cookies, cookie => cookie.Name == "__Host-PF.Session");
        Assert.True(sessionCookie.HttpOnly);
        Assert.True(sessionCookie.Secure);

        BffSession initialSession;
        using (var scope = session.Api.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PersonalFinance.Infrastructure.Persistence.AccountIdentityDbContext>();
            var row = await database.BffSessions.SingleAsync(TestContext.Current.CancellationToken);
            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("PersonalFinance.BffSession.v1");
            initialSession = System.Text.Json.JsonSerializer.Deserialize<BffSession>(protector.Unprotect(row.ProtectedPayload))!;
            var expired = initialSession with { AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
            row.ProtectedPayload = protector.Protect(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(expired));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var scope = session.Api.Services.CreateScope())
        {
            var tokenProvider = scope.ServiceProvider.GetRequiredService<IBffSessionTokenProvider>();
            var refreshedAccessToken = await tokenProvider.GetAccessTokenAsync(sessionCookie.Value,
                TestContext.Current.CancellationToken);
            Assert.False(string.IsNullOrWhiteSpace(refreshedAccessToken));
            var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
            var rotatedSession = await store.FindAsync(sessionCookie.Value, TestContext.Current.CancellationToken);
            Assert.NotNull(rotatedSession);
            Assert.NotEqual(initialSession.RefreshToken, rotatedSession.RefreshToken);
            Assert.Equal(initialSession.SessionExpiresAt, rotatedSession.SessionExpiresAt);
            var replay = await scope.ServiceProvider.GetRequiredService<IBffTokenRefresher>()
                .RefreshAsync(initialSession.RefreshToken!, TestContext.Current.CancellationToken);
            Assert.True(replay is null, "A redeemed refresh token must be rejected.");

            var handler = new CapturingAuthorizationHandler();
            using var resourceHttpClient = new HttpClient(handler) { BaseAddress = new Uri("https://synthetic-resource.example.test/") };
            var resourceClient = new BffProtectedResourceClient(resourceHttpClient, tokenProvider);
            using var resourceResponse = await resourceClient.SendAsync(sessionCookie.Value,
                new HttpRequestMessage(HttpMethod.Get, "protected"), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, resourceResponse?.StatusCode);
            Assert.Equal("Bearer", handler.Authorization?.Scheme);
            Assert.Equal(refreshedAccessToken, handler.Authorization?.Parameter);
        }

        var browserCookiesAfterRefresh = await page.Context.CookiesAsync();
        Assert.Single(browserCookiesAfterRefresh, cookie => cookie.Name == "__Host-PF.Session");
        Assert.DoesNotContain("access_token", await page.Locator("body").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", await page.Locator("body").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.PageErrors);
    }

    [Fact]
    public async Task Registration_confirmation_and_replay_persist_through_real_browser_and_api()
    {
        await using var session = await BrowserSession.StartAsync();
        var page = session.Page;
        await page.GotoAsync(session.ClientUrl + "/register");
        await FillRegistration(page, "browser@example.test");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account", Exact = true }).ClickAsync();
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("Request accepted.");
        Assert.Equal(1, await session.Api.WithDatabaseAsync(db => db.Users.CountAsync(u => !u.EmailConfirmed)));
        var id = await session.Api.WithDatabaseAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        var link = session.Api.Email.LinkFor("browser@example.test").ToString();
        await page.GotoAsync(link);
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("Email confirmed.");
        await Expect(page).ToHaveURLAsync(session.ClientUrl + "/confirm-email");
        Assert.True(await session.Api.WithDatabaseAsync(db => db.Users.AnyAsync(u => u.Id == id && u.EmailConfirmed)));
        Assert.Empty(await page.Context.CookiesAsync());
        var replay = await page.RunAndWaitForResponseAsync(() => page.GotoAsync(link), response =>
            response.Url.EndsWith("/api/accounts/confirm-email", StringComparison.Ordinal) && response.Request.Method == "POST");
        Assert.Equal(200, replay.Status);
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("No one has been signed in.");
        Assert.Empty(await page.Context.CookiesAsync());
        Assert.Empty(session.PageErrors);
    }

    [Fact]
    public async Task Invalid_link_can_resend_and_confirm_an_existing_account()
    {
        await using var session = await BrowserSession.StartAsync();
        var page = session.Page;
        await page.GotoAsync(session.ClientUrl + "/register");
        await FillRegistration(page, "resend@example.test");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create account", Exact = true }).ClickAsync();
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("Request accepted.");
        var request = session.Api.Email.ConfirmationFor("resend@example.test");
        await page.GotoAsync(session.ClientUrl + $"/confirm-email#accountId={request.AccountId}&token=invalid");
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("invalid or expired");
        await Expect(page).ToHaveURLAsync(session.ClientUrl + "/confirm-email");
        Assert.False(await session.Api.WithDatabaseAsync(db => db.Users.AnyAsync(u => u.EmailConfirmed)));
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("resend@example.test");
        await page.GetByRole(AriaRole.Button, new() { Name = "Send confirmation email again" }).ClickAsync();
        await Expect(page.Locator(".mud-alert").Last).ToContainTextAsync("Request accepted.");
        Assert.Equal(2, session.Api.Email.Messages.Count);
        await page.GotoAsync(session.Api.Email.LinkFor("resend@example.test").ToString());
        await Expect(page.Locator(".mud-alert").First).ToContainTextAsync("Email confirmed.");
        Assert.True(await session.Api.WithDatabaseAsync(db => db.Users.AllAsync(u => u.EmailConfirmed)));
        Assert.Empty(session.PageErrors);
    }

    [Fact]
    public async Task Narrow_screen_validation_and_connection_retry_retain_email_and_allow_keyboard_submission()
    {
        await using var session = await BrowserSession.StartAsync();
        var page = session.Page;
        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync(session.ClientUrl + "/register");
        var submit = page.GetByRole(AriaRole.Button, new() { Name = "Create account", Exact = true });
        await submit.ClickAsync();
        await Expect(page.GetByText("Enter your email address.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal(0, await session.Api.WithDatabaseAsync(db => db.Users.CountAsync()));
        await FillRegistration(page, "retry-browser@example.test");
        await page.RouteAsync("**/api/accounts/register", route => route.AbortAsync());
        await submit.ClickAsync();
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("couldn’t reach the service");
        await Expect(page.GetByLabel("Email address", new() { Exact = true })).ToHaveValueAsync("retry-browser@example.test");
        Assert.Equal(0, await session.Api.WithDatabaseAsync(db => db.Users.CountAsync()));
        await page.UnrouteAsync("**/api/accounts/register");
        await submit.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Expect(page.Locator(".mud-alert")).ToContainTextAsync("Request accepted.");
        Assert.Equal(1, await session.Api.WithDatabaseAsync(db => db.Users.CountAsync()));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        Assert.Empty(session.PageErrors);
    }

    private static async Task FillRegistration(IPage page, string email)
    {
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByLabel("Confirm password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByLabel("Confirm password", new() { Exact = true }).BlurAsync();
    }

    private static async Task Login(IPage page, string clientUrl, string email)
    {
        await page.GotoAsync(clientUrl + "/login");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync(email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(clientUrl + "/budget-profiles");
    }

    private static Task<bool> BrowserAuthenticated(IPage page, string apiUrl) => page.EvaluateAsync<bool>(
        "async api => (await (await fetch(api + 'api/auth/session', { credentials: 'include' })).json()).authenticated", apiUrl);

    private static async Task<BffSession> ReadSession(AccountTestHost host, string sessionId)
    {
        using var scope = host.Services.CreateScope();
        return Assert.IsType<BffSession>(await scope.ServiceProvider.GetRequiredService<IBffSessionStore>()
            .FindAsync(sessionId, TestContext.Current.CancellationToken));
    }

    private static async Task ExpireAccessToken(AccountTestHost host, string sessionId)
    {
        var current = await ReadSession(host, sessionId);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PersonalFinance.Infrastructure.Persistence.AccountIdentityDbContext>();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sessionId)));
        var row = await db.BffSessions.SingleAsync(value => value.Id == hash, TestContext.Current.CancellationToken);
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("PersonalFinance.BffSession.v1");
        row.ProtectedPayload = protector.Protect(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(current with
        {
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        }));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpStatusCode> ResourceStatus(AccountTestHost host, string accessToken)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                errors == System.Net.Security.SslPolicyErrors.None || request.RequestUri?.IsLoopback == true &&
                certificate?.Thumbprint == host.HttpsCertificateThumbprint
        };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(host.Issuer) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/synthetic/protected");
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static async Task AddConfirmedUser(AccountTestHost host, string email)
    {
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
        var user = new AccountIdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, "abcdefghijkl")).Succeeded);
    }

    private sealed class CapturingAuthorizationHandler : HttpMessageHandler
    {
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

internal sealed class BrowserSession : IAsyncDisposable
{
    private WebApplication? client;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private TestKeyMaterial? keys;
    private Action<IServiceCollection>? configureServices;
    private int apiPort;
    public AccountTestHost Api { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;
    public string ClientUrl { get; private set; } = "";
    public string ApiUrl { get; private set; } = "";
    public List<string> PageErrors { get; } = [];
    public List<string> ApiResponses { get; } = [];
    public List<string> ConsoleErrors { get; } = [];

    public static async Task<BrowserSession> StartAsync(Action<IServiceCollection>? configure = null)
    {
        var session = new BrowserSession { keys = new TestKeyMaterial(), configureServices = configure };
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "PersonalFinance.slnx"))) root = root.Parent;
            var webRoot = Environment.GetEnvironmentVariable("PERSONAL_FINANCE_TEST_CLIENT") ??
                Path.Combine(root?.FullName ?? throw new InvalidOperationException("Repository root not found."),
                    "src", "PersonalFinance.Client", "bin", "Release", "net10.0", "publish", "wwwroot");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new InvalidOperationException("Publish the client before browser tests: dotnet publish src/PersonalFinance.Client -c Release");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot, EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0,
                listener => listener.UseHttps(session.keys.HttpsCertificate)));
            session.client = builder.Build();
            string apiUrl = "";
            // Serve test configuration without changing the published client artifact.
            session.client.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/appsettings.json"))
                    await context.Response.WriteAsJsonAsync(new { ApiBaseUrl = apiUrl });
                else await next(context);
            });
            var contentTypes = new FileExtensionContentTypeProvider();
            contentTypes.Mappings[".dat"] = "application/octet-stream"; // Blazor ICU globalization data
            session.client.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
            session.client.MapFallbackToFile("index.html");
            await session.client.StartAsync();
            session.ClientUrl = session.client.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
            session.apiPort = ReservePort();
            session.Api = session.CreateApi();
            using var http = session.Api.CreateClient();
            apiUrl = http.BaseAddress!.ToString();
            session.ApiUrl = apiUrl;
            await session.Api.MigrateAsync();
            session.playwright = await Playwright.CreateAsync();
            session.browser = await LaunchBrowserAsync(session.playwright);
            var context = await session.browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
            session.Page = await context.NewPageAsync();
            session.Page.SetDefaultTimeout(15000);
            session.Page.PageError += (_, error) => session.PageErrors.Add(error);
            session.Page.Response += (_, response) =>
            {
                if (response.Url.Contains("/api/", StringComparison.Ordinal))
                    session.ApiResponses.Add($"{(int)response.Status} {new Uri(response.Url).GetLeftPart(UriPartial.Path)}");
            };
            session.Page.Console += (_, message) => { if (message.Type == "error") session.ConsoleErrors.Add(message.Text); };
            // Fail with startup diagnostics before entering any account data.
            var startupErrors = new List<string>();
            session.Page.Console += (_, message) => { if (message.Type == "error") startupErrors.Add(message.Text); };
            await session.Page.GotoAsync(session.ClientUrl + "/register");
            try { await Expect(session.Page.GetByRole(AriaRole.Heading, new() { Name = "Create your account" })).ToBeVisibleAsync(); }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Client startup failed: " + string.Join(" | ", startupErrors.Concat(session.PageErrors)), exception);
            }
            return session;
        }
        catch { await session.DisposeAsync(); throw; }
    }

    private AccountTestHost CreateApi(string? databaseName = null)
    {
        var host = new AccountTestHost(ClientUrl)
        {
            DatabaseName = databaseName ?? "PersonalFinance_UC001_Test_" + Guid.NewGuid().ToString("N"),
            Issuer = $"https://127.0.0.1:{apiPort}/",
            UsePersistentKeys = true,
            UseSecureAntiforgery = true,
            HttpsCertificateThumbprint = keys!.HttpsCertificate.Thumbprint,
            Settings = keys!.Settings,
            ConfigureAdditionalServices = services =>
            {
                services.AddOpenIddict().AddClient(options => options.UseSystemNetHttp().ConfigureHttpClientHandler(handler =>
                    handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
                        errors == System.Net.Security.SslPolicyErrors.None ||
                        request.RequestUri?.IsLoopback == true && certificate?.Thumbprint == keys.HttpsCertificate.Thumbprint));
                configureServices?.Invoke(services);
            }
        };
        host.UseKestrel(options => options.Listen(IPAddress.Loopback, apiPort, listener => listener.UseHttps(keys.HttpsCertificate)));
        return host;
    }

    public async Task RestartApiAsync()
    {
        var databaseName = Api.DatabaseName;
        Api.DeleteDatabaseOnDispose = false;
        await Api.DisposeAsync();
        Api = CreateApi(databaseName);
        using var http = Api.CreateClient();
        Assert.Equal(ApiUrl, http.BaseAddress!.ToString());
        await Api.MigrateAsync();
    }

    private static Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
    {
        var browser = Environment.GetEnvironmentVariable("PERSONAL_FINANCE_TEST_BROWSER") ?? "chrome";
        var options = new BrowserTypeLaunchOptions { Headless = true };
        switch (browser.ToLowerInvariant())
        {
            case "chrome":
            case "edge":
                options.Channel = browser == "edge" ? "msedge" : "chrome";
                return playwright.Chromium.LaunchAsync(options);
            case "firefox": return playwright.Firefox.LaunchAsync(options);
            case "webkit": return playwright.Webkit.LaunchAsync(options);
            case "brave":
                options.ExecutablePath = Environment.GetEnvironmentVariable("PERSONAL_FINANCE_BRAVE_PATH");
                if (string.IsNullOrWhiteSpace(options.ExecutablePath) || !File.Exists(options.ExecutablePath))
                    throw new InvalidOperationException("Set PERSONAL_FINANCE_BRAVE_PATH to the actual Brave executable; Chromium is not a Brave substitute.");
                return playwright.Chromium.LaunchAsync(options);
            default: throw new InvalidOperationException("PERSONAL_FINANCE_TEST_BROWSER must be chrome, edge, firefox, webkit, or brave.");
        }
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async ValueTask DisposeAsync()
    {
        try { if (browser is not null) await browser.DisposeAsync(); }
        finally
        {
            playwright?.Dispose();
            try { if (Api is not null) await Api.DisposeAsync(); }
            finally
            {
                try { if (client is not null) await client.DisposeAsync(); }
                finally { keys?.Dispose(); }
            }
        }
    }
}

internal sealed class SyntheticResourceStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use(async (context, continuation) =>
        {
            if (context.Request.Path != "/synthetic/protected") { await continuation(context); return; }
            var result = await context.AuthenticateAsync(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            var principal = result.Principal;
            var eligible = context.RequestServices.GetRequiredService<IAccountSessionEligibility>();
            context.Response.StatusCode = result.Succeeded && principal is not null &&
                principal.HasScope("personal_finance_api") && principal.HasPresenter("personal-finance-client") &&
                Guid.TryParse(principal.GetClaim(OpenIddictConstants.Claims.Subject), out var accountId) &&
                await eligible.IsActiveAsync(accountId, context.RequestAborted) ? 204 : 401;
        });
    };
}
