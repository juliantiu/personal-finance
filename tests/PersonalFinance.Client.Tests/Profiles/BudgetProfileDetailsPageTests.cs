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
using PersonalFinance.Contracts.Profiles;
using Xunit;

namespace PersonalFinance.Client.Tests.Profiles;

public sealed class BudgetProfileDetailsPageTests
{
    [Fact]
    public async Task Deep_link_loads_settings_role_and_initial_setup_records_with_credentials()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(id);
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Shared home", page.Markup));
        Assert.Contains("Your access: View only", page.Markup);
        Assert.Contains("Main currency: GBP", page.Markup);
        Assert.Contains("Additional currencies: USD", page.Markup);
        Assert.Contains("Income", page.Markup);
        Assert.Contains("Cash", page.Markup);
        Assert.Contains("IOU", page.Markup);
        Assert.True(handler.CredentialsIncluded);
        Assert.Equal($"/api/budget-profiles/{id}", handler.Path);
    }

    [Fact]
    public async Task Nonexistent_or_inaccessible_profile_shows_the_same_generic_unavailable_page()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler(id) { NotFound = true }, $"/budget-profiles/{id}");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Budget profile unavailable", page.Markup));
        Assert.Contains("This profile isn't available.", page.Markup);
        Assert.DoesNotContain("Secret profile", page.Markup);
        Assert.Contains("href=\"/budget-profiles\"", page.Markup);
    }

    private static BunitContext CreateContext(StubHandler handler, string route)
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

    private sealed class StubHandler(Guid id) : HttpMessageHandler
    {
        public bool NotFound { get; init; }
        public string? Path { get; private set; }
        public bool CredentialsIncluded { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) });
            Path = request.RequestUri.AbsolutePath;
            CredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
            if (NotFound) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var details = new ProfileDetailsResponse(id, "Shared home", "GBP", ["USD"], "view_only",
                [new ProfileCategoryDetailsResponse("Income", "income"), new ProfileCategoryDetailsResponse("Savings", "savings"), new ProfileCategoryDetailsResponse("Debt", "debt")],
                [new ProfilePaymentMethodDetailsResponse("cash")], [new ProfileTagDetailsResponse("IOU", "iou")]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(details) });
        }
    }
}
