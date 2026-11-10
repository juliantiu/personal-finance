using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;
using PersonalFinance.Client;
using PersonalFinance.Client.Accounts;
using Xunit;

namespace PersonalFinance.Client.Tests.Profiles;

public sealed class ProfileCurrencySettingsPageTests
{
    [Theory]
    [InlineData("owner", "Owner")]
    [InlineData("editor", "Editor")]
    [InlineData("view_only", "View only")]
    public async Task Participant_can_review_saved_settings_supported_choices_and_role(string role, string roleLabel)
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(id, role);
        await using var context = CreateContext(handler, $"/budget-profiles/{id}/currencies");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Currency settings", page.Markup));
        Assert.Contains("Main currency: GBP", page.Markup);
        Assert.Contains("Additional currencies: USD", page.Markup);
        Assert.Contains("Pound sterling", page.Markup);
        Assert.Contains("US dollar", page.Markup);
        Assert.Contains($"Your access: {roleLabel}", page.Markup);
        if (role == "view_only")
        {
            Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("cannot change them", page.Markup);
        }
        else
        {
            Assert.Contains("Save currency settings", page.Markup);
            Assert.Contains("Main currency stays fixed at GBP", page.Markup);
        }
        Assert.True(handler.CredentialsIncluded);
        Assert.Equal($"/api/budget-profiles/{id}/currency-settings", handler.Path);
    }

    [Fact]
    public async Task Owner_can_save_settings_with_antiforgery_and_page_refreshes_authoritative_values()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(id, "owner");
        await using var context = CreateContext(handler, $"/budget-profiles/{id}/currencies");
        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Save currency settings", page.Markup));
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save currency settings", StringComparison.Ordinal)).ClickAsync();

        page.WaitForAssertion(() => Assert.Contains("Currency settings saved.", page.Markup));
        Assert.Equal(HttpMethod.Put, handler.UpdateMethod);
        Assert.Contains("/api/auth/antiforgery", handler.Paths);
        Assert.Contains($"/api/budget-profiles/{id}/currency-settings", handler.Paths);
        Assert.Contains("Additional currencies: USD", page.Markup);
    }

    [Fact]
    public async Task Failed_save_does_not_claim_success_or_discard_the_draft()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(id, "owner") { UpdateFailure = true };
        await using var context = CreateContext(handler, $"/budget-profiles/{id}/currencies");
        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Save currency settings", page.Markup));
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save currency settings", StringComparison.Ordinal)).ClickAsync();

        page.WaitForAssertion(() => Assert.Contains("couldn't confirm whether the settings were saved", page.Markup));
        Assert.DoesNotContain("Currency settings saved.", page.Markup);
        Assert.Contains("Additional currencies: USD", page.Markup);
    }

    [Fact]
    public async Task Missing_or_inaccessible_profile_shows_generic_unavailable_state()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler(id, "view_only") { NotFound = true }, $"/budget-profiles/{id}/currencies");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Currency settings unavailable", page.Markup));
        Assert.DoesNotContain("Secret profile", page.Markup);
    }

    [Fact]
    public async Task Failed_load_is_not_displayed_as_empty_or_default_currency_settings()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler(id, "owner") { Failure = true }, $"/budget-profiles/{id}/currencies");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("We couldn't load currency settings.", page.Markup));
        Assert.Contains("role=\"alert\"", page.Markup);
        Assert.DoesNotContain("Main currency:", page.Markup);
        Assert.DoesNotContain("No additional currencies", page.Markup);
    }

    [Fact]
    public async Task Loading_is_distinct_from_saved_settings_and_then_displays_the_response()
    {
        var id = Guid.NewGuid();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHandler(id, "owner") { Pending = completion.Task };
        await using var context = CreateContext(handler, $"/budget-profiles/{id}/currencies");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Loading currency settings", page.Markup));
        Assert.DoesNotContain("Main currency:", page.Markup);
        completion.SetResult(StubHandler.SettingsResponse(id, "owner"));
        page.WaitForAssertion(() => Assert.Contains("Main currency: GBP", page.Markup));
    }

    private static BunitContext CreateContext(HttpMessageHandler handler, string route)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.RemoveAll<IAuthorizationService>();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });
        context.Services.AddSingleton<BffAuthenticationStateProvider>();
        context.Services.AddSingleton<AuthenticationStateProvider>(provider => provider.GetRequiredService<BffAuthenticationStateProvider>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        return context;
    }

    private sealed class StubHandler(Guid id, string role) : HttpMessageHandler
    {
        public bool NotFound { get; init; }
        public bool Failure { get; init; }
        public bool UpdateFailure { get; init; }
        public Task<HttpResponseMessage>? Pending { get; init; }
        public string? Path { get; private set; }
        public bool CredentialsIncluded { get; private set; }
        public HttpMethod? UpdateMethod { get; private set; }
        public List<string> Paths { get; } = [];
        private bool _updated;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) });
            Path = request.RequestUri.AbsolutePath;
            Paths.Add(request.RequestUri.AbsolutePath);
            CredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
            if (request.RequestUri.AbsolutePath == "/api/auth/antiforgery")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "synthetic-proof" }) });
            if (request.Method == HttpMethod.Put)
            {
                UpdateMethod = request.Method;
                if (UpdateFailure) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                _updated = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
            if (Pending is not null) return Pending;
            if (NotFound) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if (Failure) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            Assert.Equal($"/api/budget-profiles/{id}/currency-settings", Path);
            return Task.FromResult(SettingsResponse(id, role, _updated));
        }

        public static HttpResponseMessage SettingsResponse(Guid id, string role, bool updated = false) => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    profileId = id,
                    mainCurrency = "GBP",
                    additionalCurrencies = new[] { "USD" },
                    role,
                    configurationVersion = updated ? 1L : 0L,
                    supportedCurrencies = new[]
                    {
                        new { code = "GBP", name = "Pound sterling", symbol = "£" },
                        new { code = "USD", name = "US dollar", symbol = "$" }
                    }
                })
            };
    }
}
