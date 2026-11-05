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

public sealed class ProfileListPageTests
{
    [Fact]
    public async Task Authenticated_empty_list_is_a_real_protected_route()
    {
        await using var context = CreateContext(new StubHandler());
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("You don't have any budget profiles yet.", page.Markup));
        Assert.Equal("Budget profiles", page.Find("h1").TextContent);
        Assert.DoesNotContain("not found", page.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Populated_list_displays_names_currencies_and_current_roles_without_unimplemented_links()
    {
        var handler = new StubHandler { Populated = true };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Equal(3, page.FindAll("ul[aria-label='Accessible budget profiles'] > li").Count));
        foreach (var text in new[] { "Home", "Shared holiday", "Family savings", "GBP", "USD", "Owner", "Editor", "View only" })
            Assert.Contains(text, page.Markup);
        Assert.DoesNotContain("You don't have any budget profiles yet.", page.Markup);
        Assert.Empty(page.FindAll("a[href^='/budget-profiles/']"));
        Assert.True(handler.CredentialsIncluded);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("network")]
    [InlineData("json")]
    [InlineData("null")]
    public async Task Failed_load_is_not_an_empty_list_and_can_be_retried(string failure)
    {
        var handler = new StubHandler { Failure = failure };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("We couldn't load your budget profiles.", page.Markup));
        Assert.Contains("We couldn't load your budget profiles.", page.Find("[role='alert']").TextContent);
        Assert.DoesNotContain("You don't have any budget profiles yet.", page.Markup);
        handler.Failure = null;
        await page.InvokeAsync(() => page.FindAll("button").Single(button => button.TextContent.Contains("Try again", StringComparison.Ordinal)).Click());
        page.WaitForAssertion(() => Assert.Contains("You don't have any budget profiles yet.", page.Markup));
        Assert.Equal(2, handler.ProfileRequests);
    }

    [Fact]
    public async Task Loading_is_distinct_from_empty_and_error_states()
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = CreateContext(new StubHandler { Pending = completion.Task });
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Loading budget profiles", page.Markup));
        Assert.DoesNotContain("You don't have any budget profiles yet.", page.Markup);
        completion.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        page.WaitForAssertion(() => Assert.Contains("You don't have any budget profiles yet.", page.Markup));
    }

    [Fact]
    public async Task Anonymous_route_redirects_to_login_without_loading_profiles()
    {
        var handler = new StubHandler { Authenticated = false };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Equal("http://localhost/login?returnUrl=%2Fbudget-profiles", context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(0, handler.ProfileRequests);
    }

    [Fact]
    public async Task A_session_lost_between_route_authorization_and_list_load_returns_to_login()
    {
        await using var context = CreateContext(new StubHandler { Failure = "unauthorized" });
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Equal("http://localhost/login?returnUrl=%2Fbudget-profiles",
            context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.DoesNotContain("Home", page.Markup);
    }

    private static BunitContext CreateContext(StubHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.RemoveAll<IAuthorizationService>();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });
        context.Services.AddSingleton<BffAuthenticationStateProvider>();
        context.Services.AddSingleton<AuthenticationStateProvider>(provider => provider.GetRequiredService<BffAuthenticationStateProvider>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/budget-profiles");
        return context;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public bool Authenticated { get; init; } = true;
        public bool Populated { get; init; }
        public string? Failure { get; set; }
        public Task<HttpResponseMessage>? Pending { get; init; }
        public int ProfileRequests { get; private set; }
        public bool CredentialsIncluded { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { authenticated = Authenticated, email = "person@example.test" }) });
            if (request.RequestUri.AbsolutePath == "/api/auth/antiforgery")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { requestToken = "test-csrf-token" }) });
            Assert.Equal("/api/budget-profiles", request.RequestUri.AbsolutePath);
            ProfileRequests++;
            CredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
            if (Pending is not null) return Pending;
            if (Failure == "network") throw new HttpRequestException();
            if (Failure == "server") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            if (Failure == "unauthorized") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (Failure is "json" or "null") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(Failure == "json" ? "{invalid" : "null", System.Text.Encoding.UTF8, "application/json") });
            object[] profiles = Populated
                ? [new { id = Guid.NewGuid(), name = "Home", mainCurrency = "GBP", role = "owner" },
                   new { id = Guid.NewGuid(), name = "Shared holiday", mainCurrency = "USD", role = "editor" },
                   new { id = Guid.NewGuid(), name = "Family savings", mainCurrency = "GBP", role = "view_only" }]
                : [];
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(profiles) });
        }
    }
}
