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

public sealed class PaymentMethodsPageTests
{
    [Theory]
    [InlineData("owner", "Owner")]
    [InlineData("editor", "Editor")]
    [InlineData("view_only", "View only")]
    public async Task Deep_link_loads_existing_cash_with_credentials_and_no_mutation_controls(string role, string label)
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => Task.FromResult(Methods(id, "Shared home", role)));
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Cash", page.Markup));
        Assert.Contains("Payment methods", page.Markup);
        Assert.Contains("Shared home", page.Markup);
        Assert.Contains("Your access: " + label, page.Markup);
        Assert.Contains($"href=\"/budget-profiles/{id}\"", page.Markup);
        Assert.True(handler.CredentialsIncluded);
        Assert.Equal($"/api/budget-profiles/{id}/payment-methods", Assert.Single(handler.Paths));
        Assert.DoesNotContain("Add card", page.Markup);
        Assert.DoesNotContain("Reveal", page.Markup);
    }

    [Fact]
    public async Task Loading_is_distinct_from_a_genuine_empty_list()
    {
        var id = Guid.NewGuid();
        var completion = new TaskCompletionSource<HttpResponseMessage>();
        await using var context = CreateContext(new StubHandler(_ => completion.Task), Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Loading payment methods", page.Markup));
        Assert.DoesNotContain("No payment methods", page.Markup);
        completion.SetResult(Methods(id, "Empty home", empty: true));
        page.WaitForAssertion(() => Assert.Contains("No payment methods are available.", page.Markup));
        Assert.DoesNotContain("Cash", page.Markup);
        Assert.DoesNotContain("couldn't load", page.Markup);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("network")]
    [InlineData("json")]
    [InlineData("null")]
    [InlineData("missing-list")]
    [InlineData("other-profile")]
    public async Task Failed_load_is_not_empty_and_can_be_explicitly_retried(string failure)
    {
        var id = Guid.NewGuid();
        var calls = 0;
        var handler = new StubHandler(_ =>
        {
            if (++calls > 1) return Task.FromResult(Methods(id, "Recovered home"));
            return failure switch
            {
                "network" => Task.FromException<HttpResponseMessage>(new HttpRequestException()),
                "json" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid json") }),
                "null" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") }),
                "missing-list" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { profileId = id, profileName = "Incomplete", role = "owner" }) }),
                "other-profile" => Task.FromResult(Methods(Guid.NewGuid(), "Wrong profile")),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            };
        });
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("We couldn't load payment methods.", page.Markup));
        Assert.DoesNotContain("No payment methods", page.Markup);
        page.FindAll("button").Single(button => button.TextContent.Contains("Try again")).Click();
        page.WaitForAssertion(() => Assert.Contains("Recovered home", page.Markup));
        Assert.Contains("Cash", page.Markup);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Unavailable_profile_hides_previous_content_after_access_is_revoked()
    {
        var id = Guid.NewGuid();
        var calls = 0;
        var handler = new StubHandler(_ => Task.FromResult(++calls == 1 ? Methods(id, "Shared home") : new HttpResponseMessage(HttpStatusCode.NotFound)));
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Shared home", page.Markup));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(Route(Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Contains("Budget profile unavailable", page.Markup));
        Assert.DoesNotContain("Shared home", page.Markup);
        Assert.DoesNotContain("Cash", page.Markup);
        Assert.Contains("href=\"/budget-profiles\"", page.Markup);
    }

    [Fact]
    public async Task Session_failure_redirects_to_login_with_the_payment_methods_return_path()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))), Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Equal("http://localhost/login?returnUrl=" + Uri.EscapeDataString(Route(id)), context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.DoesNotContain("Cash", page.Markup);
        Assert.DoesNotContain("No payment methods", page.Markup);
    }

    [Fact]
    public async Task Observed_sign_out_removes_loaded_methods()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler(_ => Task.FromResult(Methods(id, "Shared home"))), Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Shared home", page.Markup));
        await page.InvokeAsync(() => context.Services.GetRequiredService<BffAuthenticationStateProvider>().NotifySignedOut());
        page.WaitForAssertion(() => Assert.DoesNotContain("Shared home", page.Markup));
        Assert.DoesNotContain("Cash", page.Markup);
    }

    [Fact]
    public async Task Route_change_clears_loaded_methods_and_ignores_a_late_response()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHandler(path => path.Contains(first.ToString()) ? Task.FromResult(Methods(first, "First home"))
            : path.Contains(second.ToString()) ? delayed.Task : Task.FromResult(Methods(third, "Third home", empty: true)));
        await using var context = CreateContext(handler, Route(first));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("First home", page.Markup));
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(Route(second));
        page.WaitForAssertion(() => Assert.Contains("Loading payment methods", page.Markup));
        Assert.DoesNotContain("First home", page.Markup);
        Assert.DoesNotContain("Cash", page.Markup);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(Route(third));
        page.WaitForAssertion(() => Assert.Contains("Third home", page.Markup));
        delayed.SetResult(Methods(second, "Second home"));
        await page.InvokeAsync(() => Task.CompletedTask);
        Assert.Contains("Third home", page.Markup);
        Assert.DoesNotContain("Second home", page.Markup);
        Assert.DoesNotContain("Cash", page.Markup);
        Assert.Equal(3, handler.Paths.Count);
    }

    private static string Route(Guid id) => $"/budget-profiles/{id}/payment-methods";

    private static HttpResponseMessage Methods(Guid id, string name, string role = "owner", bool empty = false) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { profileId = id, profileName = name, role, methods = empty ? Array.Empty<object>() : [new { id = Guid.NewGuid(), kind = "cash" }] })
    };

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

    private sealed class StubHandler(Func<string, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public bool CredentialsIncluded { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) });
            Paths.Add(request.RequestUri.AbsolutePath);
            CredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
            return respond(request.RequestUri.AbsolutePath);
        }
    }
}
