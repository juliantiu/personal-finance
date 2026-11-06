using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using PersonalFinance.Client;
using PersonalFinance.Client.Accounts;
using PersonalFinance.Contracts.Profiles;
using Xunit;

namespace PersonalFinance.Client.Tests.Profiles;

public sealed class CreateProfilePageTests
{
    [Fact]
    public async Task Creation_route_requires_an_explicit_main_currency_and_valid_name()
    {
        var handler = new StubHandler();
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Equal("Create budget profile", page.Find("h1").TextContent));
        Assert.Null(page.FindComponent<MudSelect<string>>().Instance.GetState(value => value.Value));
        await Submit(page);
        Assert.Contains("Enter a profile name.", page.Markup);
        Assert.Contains("Select GBP or USD", page.Markup);
        Assert.Equal(0, handler.Posts);
        page.Find("#profile-name").Change(new string('x', 101));
        await SelectMain(page, "GBP");
        await Submit(page);
        Assert.Contains("at most 100 characters", page.Markup);
        Assert.Equal(0, handler.Posts);
    }

    [Fact]
    public async Task Valid_submission_sends_credentials_proof_and_contract_then_returns_to_list()
    {
        var handler = new StubHandler();
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        await Fill(page);
        await page.InvokeAsync(() => page.FindComponent<MudCheckBox<bool>>().Instance.ValueChanged.InvokeAsync(true));
        await Submit(page);
        page.WaitForAssertion(() => Assert.Equal("http://localhost/budget-profiles", context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.Equal(1, handler.Posts);
        Assert.Equal(" Home ", handler.Input!.Name);
        Assert.Equal("GBP", handler.Input.MainCurrency);
        Assert.Equal("USD", Assert.Single(handler.Input.AdditionalCurrencies!));
        Assert.True(handler.CredentialsIncluded);
        Assert.Equal("test-proof", handler.Proof);
    }

    [Fact]
    public async Task Server_field_feedback_preserves_inputs_and_can_be_corrected()
    {
        var handler = new StubHandler { Failure = "validation" };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        await Fill(page);
        await Submit(page);
        page.WaitForAssertion(() => Assert.Contains("Name rejected by server.", page.Markup));
        Assert.Equal(" Home ", page.Find("#profile-name").GetAttribute("value"));
        Assert.Equal("GBP", page.FindComponent<MudSelect<string>>().Instance.GetState(value => value.Value));
        handler.Failure = null;
        page.Find("#profile-name").Change("Fixed");
        await Submit(page);
        Assert.Equal(2, handler.Posts);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("server")]
    [InlineData("bad-success")]
    public async Task Uncertain_creation_preserves_inputs_and_requires_list_refresh_before_explicit_retry(string failure)
    {
        var handler = new StubHandler { Failure = failure };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        await Fill(page);
        await Submit(page);
        page.WaitForAssertion(() => Assert.Contains("couldn't confirm whether", page.Markup));
        Assert.Equal(" Home ", page.Find("#profile-name").GetAttribute("value"));
        Assert.Equal(1, handler.Posts);
        Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent == "Retry creation");
        await page.InvokeAsync(() => page.FindAll("button").Single(button => button.TextContent == "Refresh profile list").Click());
        page.WaitForAssertion(() => Assert.Contains("Already saved", page.Markup));
        Assert.Equal(1, handler.Lists);
        Assert.Equal(1, handler.Posts);
        handler.Failure = null;
        await page.InvokeAsync(() => page.FindAll("button").Single(button => button.TextContent == "Retry creation").Click());
        Assert.Equal(2, handler.Posts);
    }

    [Fact]
    public async Task In_flight_submission_disables_inputs_and_cannot_post_twice()
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHandler { Pending = completion.Task };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        await Fill(page);
        var click = Submit(page);
        page.WaitForAssertion(() => Assert.Equal(1, handler.Posts));
        Assert.True(page.Find("#profile-name").HasAttribute("disabled"));
        Assert.True(page.FindAll("button").Single(button => button.TextContent.Contains("Creating", StringComparison.Ordinal)).HasAttribute("disabled"));
        await page.InvokeAsync(() => page.FindAll("button").Single(button => button.TextContent.Contains("Creating", StringComparison.Ordinal)).Click());
        Assert.Equal(1, handler.Posts);
        completion.SetResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new CreatedProfileResponse(Guid.NewGuid())) });
        await click;
    }

    [Theory]
    [InlineData("antiforgery")]
    [InlineData("malformed-rejection")]
    public async Task Confirmed_rejection_preserves_inputs_and_allows_a_new_explicit_submission(string failure)
    {
        var handler = new StubHandler { Failure = failure };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        await Fill(page);
        await Submit(page);
        page.WaitForAssertion(() => Assert.Contains("Your profile wasn't created.", page.Markup));
        Assert.DoesNotContain("couldn't confirm whether", page.Markup);
        Assert.Equal(" Home ", page.Find("#profile-name").GetAttribute("value"));
        handler.Failure = null;
        await Submit(page);
        Assert.Equal(2, handler.Posts);
    }

    [Fact]
    public async Task A_failed_list_refresh_cannot_enable_uncertain_creation_retry()
    {
        var handler = new StubHandler { Failure = "network", FailList = true };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        await Fill(page);
        await Submit(page);
        await page.InvokeAsync(() => page.FindAll("button").Single(button => button.TextContent == "Refresh profile list").Click());
        page.WaitForAssertion(() => Assert.Contains("couldn't refresh your profiles", page.Markup));
        Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent == "Retry creation");
        Assert.Equal(" Home ", page.Find("#profile-name").GetAttribute("value"));
        Assert.Equal(1, handler.Posts);
    }

    [Theory]
    [InlineData("unauthorized")]
    [InlineData("anonymous")]
    public async Task Missing_session_returns_to_login_for_the_create_route(string state)
    {
        var handler = new StubHandler { Failure = state };
        await using var context = CreateContext(handler);
        var page = context.Render<App>();
        if (state == "unauthorized") { await Fill(page); await Submit(page); }
        page.WaitForAssertion(() => Assert.Equal("http://localhost/login?returnUrl=%2Fbudget-profiles%2Fcreate", context.Services.GetRequiredService<NavigationManager>().Uri));
    }

    private static async Task Fill(IRenderedComponent<App> page)
    {
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("#profile-name")));
        page.Find("#profile-name").Change(" Home ");
        await SelectMain(page, "GBP");
    }
    private static Task SelectMain(IRenderedComponent<App> page, string value) =>
        page.InvokeAsync(() => page.FindComponent<MudSelect<string>>().Instance.ValueChanged.InvokeAsync(value));
    private static Task Submit(IRenderedComponent<App> page) =>
        page.InvokeAsync(() => page.FindAll("button").Single(button => button.TextContent == "Create profile").Click());

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
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/budget-profiles/create");
        return context;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public string? Failure { get; set; }
        public bool FailList { get; init; }
        public Task<HttpResponseMessage>? Pending { get; init; }
        public int Posts { get; private set; }
        public int Lists { get; private set; }
        public CreateProfileRequest? Input { get; private set; }
        public bool CredentialsIncluded { get; private set; }
        public string? Proof { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = Failure != "anonymous", email = "person@example.test" }) };
            if (request.RequestUri.AbsolutePath == "/api/auth/antiforgery")
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "test-proof" }) };
            if (request.Method == HttpMethod.Get)
            {
                Lists++;
                if (FailList) return new(HttpStatusCode.ServiceUnavailable);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new ProfileListItem(Guid.NewGuid(), "Already saved", "GBP", "owner") }) };
            }
            Posts++;
            Input = await request.Content!.ReadFromJsonAsync<CreateProfileRequest>(cancellationToken);
            CredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
            Proof = request.Headers.TryGetValues("X-CSRF-TOKEN", out var values) ? values.Single() : null;
            if (Pending is not null) return await Pending;
            if (Failure == "network") throw new HttpRequestException();
            if (Failure == "server") return new(HttpStatusCode.InternalServerError);
            if (Failure == "unauthorized") return new(HttpStatusCode.Unauthorized);
            if (Failure == "antiforgery") return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { title = "The request could not be validated." }) };
            if (Failure == "malformed-rejection") return new(HttpStatusCode.BadRequest) { Content = new StringContent("{invalid") };
            if (Failure == "validation") return new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { errors = new { Name = new[] { "Name rejected by server." } } }) };
            return new(HttpStatusCode.Created) { Content = Failure == "bad-success" ? new StringContent("{invalid") : JsonContent.Create(new CreatedProfileResponse(Guid.NewGuid())) };
        }
    }
}
