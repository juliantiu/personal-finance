using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;
using PersonalFinance.Client.Accounts;
using PersonalFinance.Client.Layout;
using PersonalFinance.Client.Pages;
using Xunit;

namespace PersonalFinance.Client.Tests.Accounts;

public sealed class SignOutTests
{
    [Fact]
    public async Task Successful_sign_out_sends_antiforgery_and_updates_authentication_before_navigation()
    {
        var handler = new StubHandler();
        using var context = CreateContext(handler);
        var notified = false;
        context.Services.GetRequiredService<AuthenticationStateProvider>().AuthenticationStateChanged += task =>
            notified = !task.GetAwaiter().GetResult().User.Identity!.IsAuthenticated;
        var layout = context.Render<MainLayout>();
        layout.WaitForAssertion(() => Assert.Single(layout.FindAll("button")));
        await layout.InvokeAsync(() => layout.Find("button").Click());

        Assert.Equal("csrf-token", handler.PostToken);
        Assert.True(notified);
        Assert.EndsWith("/login?signedOut=true", context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Empty(layout.FindAll("button"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_sign_out_keeps_authenticated_state_and_allows_retry(bool networkFailure)
    {
        var handler = new StubHandler { Fail = true, NetworkFailure = networkFailure };
        using var context = CreateContext(handler);
        var layout = context.Render<MainLayout>();
        layout.WaitForAssertion(() => Assert.Single(layout.FindAll("button")));
        await layout.InvokeAsync(() => layout.Find("button").Click());

        layout.WaitForAssertion(() => Assert.Contains("couldn’t complete sign-out", layout.Markup));
        Assert.True((await context.Services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        Assert.Equal("http://localhost/", context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.False(layout.Find("button").HasAttribute("disabled"));
        handler.Fail = false;
        await layout.InvokeAsync(() => layout.Find("button").Click());
        Assert.EndsWith("/login?signedOut=true", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Signed_out_login_shows_confirmation_and_a_sign_in_action()
    {
        using var context = CreateContext(new StubHandler { Authenticated = false });
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/login?signedOut=true");
        var page = context.Render<Login>();
        Assert.Contains("You have been signed out.", page.Markup);
        Assert.Contains("Sign in", page.Find("button.mud-button-root").TextContent);
    }

    [Fact]
    public void A_query_parameter_cannot_report_sign_out_while_the_session_is_still_authenticated()
    {
        using var context = CreateContext(new StubHandler());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/login?signedOut=true");
        var page = context.Render<Login>();
        Assert.DoesNotContain("You have been signed out.", page.Markup);
    }

    [Fact]
    public void Unavailable_session_status_does_not_confirm_sign_out()
    {
        using var context = CreateContext(new StubHandler { Authenticated = false, FailSession = true });
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/login?signedOut=true");
        var page = context.Render<Login>();
        Assert.DoesNotContain("You have been signed out.", page.Markup);
    }

    private static BunitContext CreateContext(StubHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.RemoveAll<IAuthorizationService>();
        context.Services.AddAuthorizationCore();
        context.Services.AddCascadingAuthenticationState();
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });
        context.Services.AddSingleton<BffAuthenticationStateProvider>();
        context.Services.AddSingleton<AuthenticationStateProvider>(provider => provider.GetRequiredService<BffAuthenticationStateProvider>());
        return context;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public bool Fail { get; set; }
        public bool NetworkFailure { get; init; }
        public bool FailSession { get; init; }
        public bool Authenticated { get; set; } = true;
        public string? PostToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/session", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(FailSession ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = Authenticated, email = "person@example.test" }) });
            if (request.RequestUri.AbsolutePath.EndsWith("/antiforgery", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "csrf-token" }) });
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/api/auth/sign-out", request.RequestUri.AbsolutePath);
            PostToken = request.Headers.GetValues("X-CSRF-TOKEN").Single();
            if (Fail && NetworkFailure) throw new HttpRequestException("connection lost");
            if (!Fail) Authenticated = false;
            return Task.FromResult(new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent));
        }
    }
}
