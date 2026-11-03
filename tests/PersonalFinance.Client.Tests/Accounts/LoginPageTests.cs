using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PersonalFinance.Client.Pages;
using PersonalFinance.Client.Accounts;
using Xunit;

namespace PersonalFinance.Client.Tests.Accounts;

public sealed class LoginPageTests
{
    [Fact]
    public void Protected_page_redirect_keeps_only_a_safe_local_return_path()
    {
        using var context = CreateContext(new StubHandler(HttpStatusCode.Unauthorized));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/transactions?period=current");

        context.Render<RedirectToLogin>();

        Assert.Equal("http://localhost/login?returnUrl=%2Ftransactions%3Fperiod%3Dcurrent", navigation.Uri);
    }

    [Fact]
    public async Task Successful_login_starts_the_BFF_flow_with_a_safe_return_path()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent);
        using var context = CreateContext(handler);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/login?returnUrl=%2Ftransactions%3Fperiod%3Dcurrent");
        var page = context.Render<Login>();
        page.Find("input[type='email']").Change("person@example.test");
        page.Find("input[type='password']").Change("correct horse battery staple");

        await page.InvokeAsync(() => page.Find("button.mud-button-root").Click());

        Assert.Equal("https://api.example.test/api/auth/start?returnPath=%2Ftransactions%3Fperiod%3Dcurrent", navigation.Uri);
        Assert.Equal(new[] { "api/auth/antiforgery", "api/auth/sign-in" }, handler.Paths);
        Assert.Equal("request-token", handler.SignInToken);
    }

    [Fact]
    public async Task Unsafe_return_path_is_replaced_with_the_budget_profiles_default()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent);
        using var context = CreateContext(handler);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/login?returnUrl=%2F%2Fevil.example.test");
        var page = context.Render<Login>();
        page.Find("input[type='email']").Change("person@example.test");
        page.Find("input[type='password']").Change("correct horse battery staple");

        await page.InvokeAsync(() => page.Find("button.mud-button-root").Click());

        Assert.Equal("https://api.example.test/api/auth/start?returnPath=%2Fbudget-profiles", navigation.Uri);
    }

    [Fact]
    public async Task Direct_login_without_a_return_path_uses_the_budget_profiles_default()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent);
        using var context = CreateContext(handler);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/login");
        var page = context.Render<Login>();
        page.Find("input[type='email']").Change("person@example.test");
        page.Find("input[type='password']").Change("correct horse battery staple");

        await page.InvokeAsync(() => page.Find("button.mud-button-root").Click());

        Assert.Equal("https://api.example.test/api/auth/start?returnPath=%2Fbudget-profiles", navigation.Uri);
    }

    [Fact]
    public async Task Credential_failures_show_the_same_safe_message()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized);
        using var context = CreateContext(handler);
        var page = context.Render<Login>();
        page.Find("input[type='email']").Change("person@example.test");
        page.Find("input[type='password']").Change("wrong-password");

        await page.InvokeAsync(() => page.Find("button.mud-button-root").Click());

        page.WaitForAssertion(() => Assert.Contains("Sign-in failed. Check your details and try again.", page.Markup));
    }

    private static BunitContext CreateContext(StubHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });
        return context;
    }

    private sealed class StubHandler(HttpStatusCode signInStatus) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public string? SignInToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery.TrimStart('/'));
            if (request.RequestUri.AbsolutePath.EndsWith("antiforgery", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "request-token" }) };
            request.Headers.TryGetValues("X-CSRF-TOKEN", out var values);
            SignInToken = values?.SingleOrDefault();
            return new HttpResponseMessage(signInStatus);
        }
    }
}
