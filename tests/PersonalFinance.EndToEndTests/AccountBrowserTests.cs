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
            var currentUrl = page.Url;
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
    public AccountTestHost Api { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;
    public string ClientUrl { get; private set; } = "";
    public string ApiUrl { get; private set; } = "";
    public List<string> PageErrors { get; } = [];
    public List<string> ApiResponses { get; } = [];
    public List<string> ConsoleErrors { get; } = [];

    public static async Task<BrowserSession> StartAsync()
    {
        var session = new BrowserSession();
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "PersonalFinance.slnx"))) root = root.Parent;
            var webRoot = Environment.GetEnvironmentVariable("PERSONAL_FINANCE_TEST_CLIENT") ??
                Path.Combine(root?.FullName ?? throw new InvalidOperationException("Repository root not found."),
                    "src", "PersonalFinance.Client", "bin", "Release", "net10.0", "publish", "wwwroot");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new InvalidOperationException("Publish the client before browser tests: dotnet publish src/PersonalFinance.Client -c Release");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://127.0.0.1:{ReservePort()}");
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
            var apiPort = ReservePort();
            session.Api = new AccountTestHost(session.ClientUrl) { Issuer = $"http://127.0.0.1:{apiPort}/" };
            session.Api.UseKestrel(apiPort);
            using var http = session.Api.CreateClient();
            apiUrl = http.BaseAddress!.ToString();
            session.ApiUrl = apiUrl;
            await session.Api.MigrateAsync();
            session.playwright = await Playwright.CreateAsync();
            session.browser = await session.playwright.Chromium.LaunchAsync(new()
            {
                Headless = true,
                ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            });
            session.Page = await session.browser.NewPageAsync();
            session.Page.SetDefaultTimeout(15000);
            session.Page.PageError += (_, error) => session.PageErrors.Add(error);
            session.Page.Response += (_, response) =>
            {
                if (response.Url.Contains("/api/", StringComparison.Ordinal))
                    session.ApiResponses.Add($"{(int)response.Status} {response.Url}");
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
            finally { if (client is not null) await client.DisposeAsync(); }
        }
    }
}
