using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using PersonalFinance.Tests.Support;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class AccountBrowserTests
{
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
}

internal sealed class BrowserSession : IAsyncDisposable
{
    private WebApplication? client;
    private IPlaywright? playwright;
    private IBrowser? browser;
    public AccountTestHost Api { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;
    public string ClientUrl { get; private set; } = "";
    public List<string> PageErrors { get; } = [];

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
            builder.WebHost.UseUrls("http://127.0.0.1:0");
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
            session.Api = new AccountTestHost(session.ClientUrl);
            session.Api.UseKestrel(0);
            using var http = session.Api.CreateClient();
            apiUrl = http.BaseAddress!.ToString();
            await session.Api.MigrateAsync();
            session.playwright = await Playwright.CreateAsync();
            session.browser = await session.playwright.Chromium.LaunchAsync(new() { Headless = true });
            session.Page = await session.browser.NewPageAsync();
            session.Page.SetDefaultTimeout(15000);
            session.Page.PageError += (_, error) => session.PageErrors.Add(error);
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
