using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
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
        Assert.Contains($"href=\"/budget-profiles/{id}/currencies\"", page.Markup);
        Assert.Contains($"href=\"/budget-profiles/{id}/categories\"", page.Markup);
        Assert.Contains("Income", page.Markup);
        Assert.Contains("Cash", page.Markup);
        Assert.Contains("IOU", page.Markup);
        Assert.True(handler.CredentialsIncluded);
        Assert.Equal($"/api/budget-profiles/{id}", handler.Path);
    }

    [Fact]
    public async Task Owner_sees_loading_then_populated_participant_list()
    {
        var id = Guid.NewGuid();
        var participantResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ParticipantsStubHandler(id, "owner", participantResponse.Task);
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();
        await handler.ParticipantRequested.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        page.WaitForAssertion(() => Assert.Contains("Loading participants…", page.Markup));

        participantResponse.SetResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[]
            {
                new ProfileParticipantResponse("editor@example.test", "editor"),
                new ProfileParticipantResponse("viewer@example.test", "view_only")
            })
        });
        page.WaitForAssertion(() =>
        {
            Assert.Contains("Sharing", page.Markup);
            Assert.Contains("editor@example.test — Editor", page.Markup);
            Assert.Contains("viewer@example.test — View only", page.Markup);
        });
        Assert.Equal($"/api/budget-profiles/{id}/participants", handler.ParticipantPath);
        Assert.True(handler.ParticipantCredentialsIncluded);
    }

    [Fact]
    public async Task Owner_sees_empty_participant_state()
    {
        var id = Guid.NewGuid();
        var handler = new ParticipantsStubHandler(id, "owner");
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("No participants have access yet.", page.Markup));
        Assert.Contains("Sharing", page.Markup);
    }

    [Fact]
    public async Task Owner_submits_role_grant_with_antiforgery_proof_and_sees_saved_feedback()
    {
        var id = Guid.NewGuid();
        var handler = new ParticipantsStubHandler(id, "owner");
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("No participants have access yet.", page.Markup));
        page.Find("input[type='email']").Change("recipient@example.test");
        var role = page.FindComponent<MudSelect<string>>();
        await role.InvokeAsync(() => role.Instance.ValueChanged.InvokeAsync("view_only"));
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save access", StringComparison.Ordinal)).ClickAsync();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Access saved.", page.Markup);
            Assert.Contains("recipient@example.test — View only", page.Markup);
        });
        Assert.True(handler.GrantRequested);
        Assert.Equal("recipient@example.test", handler.GrantEmail);
        Assert.Equal("view_only", handler.GrantRole);
        Assert.Equal("test-proof", handler.GrantAntiforgeryToken);
        Assert.True(handler.GrantCredentialsIncluded);
    }

    [Fact]
    public async Task Owner_sees_generic_missing_account_feedback_when_grant_is_rejected()
    {
        var id = Guid.NewGuid();
        var handler = new ParticipantsStubHandler(id, "owner") { GrantStatus = HttpStatusCode.NotFound };
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("No participants have access yet.", page.Markup));
        page.Find("input[type='email']").Change("missing@example.test");
        await page.FindAll("button").Single(button => button.TextContent.Contains("Save access", StringComparison.Ordinal)).ClickAsync();

        page.WaitForAssertion(() => Assert.Contains("No eligible account found.", page.Markup));
        Assert.DoesNotContain("Access saved.", page.Markup);
    }

    [Fact]
    public async Task Owner_removes_participant_with_antiforgery_proof_and_refreshes_the_list()
    {
        var id = Guid.NewGuid();
        var handler = new ParticipantsStubHandler(id, "owner");
        handler.SeedParticipant(new ProfileParticipantResponse("recipient@example.test", "editor"));
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("recipient@example.test — Editor", page.Markup));
        await page.Find("button[aria-label='Remove access for recipient@example.test']").ClickAsync();

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Access removed.", page.Markup);
            Assert.Contains("No participants have access yet.", page.Markup);
            Assert.DoesNotContain("recipient@example.test — Editor", page.Markup);
        });
        Assert.True(handler.RemovalRequested);
        Assert.Equal("recipient@example.test", handler.RemovalEmail);
        Assert.Equal("test-proof", handler.RemovalAntiforgeryToken);
        Assert.True(handler.RemovalCredentialsIncluded);
    }

    [Theory]
    [InlineData("editor")]
    [InlineData("view_only")]
    public async Task Non_owner_does_not_see_sharing_view_or_request_participants(string role)
    {
        var id = Guid.NewGuid();
        var handler = new ParticipantsStubHandler(id, role);
        await using var context = CreateContext(handler, $"/budget-profiles/{id}");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Shared home", page.Markup));
        Assert.DoesNotContain("Sharing", page.Markup);
        Assert.Null(handler.ParticipantPath);
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

    [Fact]
    public async Task Expired_session_clears_profile_details_and_returns_to_login()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler(id) { Unauthorized = true }, $"/budget-profiles/{id}");

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Equal(
            $"http://localhost/login?returnUrl=%2Fbudget-profiles%2F{id}",
            context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.DoesNotContain("Shared home", page.Markup);
        Assert.DoesNotContain("Main currency:", page.Markup);
    }

    [Fact]
    public async Task Changing_profile_route_loads_the_new_profile_and_clears_the_previous_content()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var handler = new RouteSwitchingHandler(firstId, secondId);
        await using var context = CreateContext(handler, $"/budget-profiles/{firstId}");

        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("First profile", page.Markup));

        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/budget-profiles/{secondId}");

        page.WaitForAssertion(() =>
        {
            Assert.Contains("Second profile", page.Markup);
            Assert.DoesNotContain("First profile", page.Markup);
            Assert.Contains("Main currency: USD", page.Markup);
            Assert.Contains("Your access: Editor", page.Markup);
        });
        Assert.Equal(new[] { firstId, secondId }, handler.RequestedProfileIds);
    }

    [Fact]
    public async Task Late_response_for_previous_route_cannot_replace_the_selected_profile()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResponse = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RouteSwitchingHandler(firstId, secondId)
        {
            FirstResponse = firstResponse.Task,
            SecondResponse = secondResponse.Task
        };
        await using var context = CreateContext(handler, $"/budget-profiles/{firstId}");

        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Loading budget profile", page.Markup));

        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/budget-profiles/{secondId}");
        page.WaitForAssertion(() => Assert.Contains("Loading budget profile", page.Markup));
        Assert.DoesNotContain("First profile", page.Markup);

        secondResponse.SetResult(RouteSwitchingHandler.ProfileResponse(secondId, "Second profile", "USD", "editor"));
        page.WaitForAssertion(() => Assert.Contains("Second profile", page.Markup));

        firstResponse.SetResult(RouteSwitchingHandler.ProfileResponse(firstId, "First profile", "GBP", "view_only"));
        await Task.Yield();

        Assert.Contains("Second profile", page.Markup);
        Assert.DoesNotContain("First profile", page.Markup);
        Assert.Contains("Main currency: USD", page.Markup);
        Assert.Contains("Your access: Editor", page.Markup);
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

    private sealed class StubHandler(Guid id) : HttpMessageHandler
    {
        public bool NotFound { get; init; }
        public bool Unauthorized { get; init; }
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
            if (Unauthorized) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (NotFound) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var details = new ProfileDetailsResponse(id, "Shared home", "GBP", ["USD"], "view_only",
                [new ProfileCategoryDetailsResponse(Guid.NewGuid(), "Income", "income"), new ProfileCategoryDetailsResponse(Guid.NewGuid(), "Savings", "savings"), new ProfileCategoryDetailsResponse(Guid.NewGuid(), "Debt", "debt")],
                [new ProfilePaymentMethodDetailsResponse("cash")], [new ProfileTagDetailsResponse("IOU", "iou")]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(details) });
        }
    }

    private sealed class ParticipantsStubHandler(Guid id, string role, Task<HttpResponseMessage>? participantResponse = null) : HttpMessageHandler
    {
        public TaskCompletionSource ParticipantRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? ParticipantPath { get; private set; }
        public bool ParticipantCredentialsIncluded { get; private set; }
        public HttpStatusCode GrantStatus { get; init; } = HttpStatusCode.OK;
        public bool GrantRequested { get; private set; }
        public string? GrantEmail { get; private set; }
        public string? GrantRole { get; private set; }
        public string? GrantAntiforgeryToken { get; private set; }
        public bool GrantCredentialsIncluded { get; private set; }
        public bool RemovalRequested { get; private set; }
        public string? RemovalEmail { get; private set; }
        public string? RemovalAntiforgeryToken { get; private set; }
        public bool RemovalCredentialsIncluded { get; private set; }
        private readonly List<ProfileParticipantResponse> savedParticipants = [];

        public void SeedParticipant(ProfileParticipantResponse participant) => savedParticipants.Add(participant);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) };

            if (request.RequestUri.AbsolutePath == "/api/auth/antiforgery")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "test-proof" }) };

            if (request.RequestUri.AbsolutePath.EndsWith("/participants", StringComparison.Ordinal))
            {
                if (request.Method == HttpMethod.Post)
                {
                    GrantRequested = true;
                    GrantAntiforgeryToken = request.Headers.GetValues("X-CSRF-TOKEN").SingleOrDefault();
                    GrantCredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var grantOptions)
                        && grantOptions.TryGetValue("credentials", out var grantCredentials) && Equals(grantCredentials, "include");
                    var grant = await request.Content!.ReadFromJsonAsync<ProfileAccessGrantRequest>(cancellationToken: cancellationToken);
                    GrantEmail = grant?.Email;
                    GrantRole = grant?.Role;
                    if (grant is not null)
                    {
                        var currentIndex = savedParticipants.FindIndex(value => value.Email == grant.Email);
                        if (currentIndex >= 0) savedParticipants[currentIndex] = new ProfileParticipantResponse(grant.Email!, grant.Role!);
                        else savedParticipants.Add(new ProfileParticipantResponse(grant.Email!, grant.Role!));
                    }
                    return new HttpResponseMessage(GrantStatus)
                    {
                        Content = GrantStatus == HttpStatusCode.NotFound
                            ? JsonContent.Create(new { title = "No eligible account found." })
                            : null
                    };
                }
                if (request.Method == HttpMethod.Delete)
                {
                    RemovalRequested = true;
                    RemovalAntiforgeryToken = request.Headers.GetValues("X-CSRF-TOKEN").SingleOrDefault();
                    RemovalCredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var removeOptions)
                        && removeOptions.TryGetValue("credentials", out var removeCredentials) && Equals(removeCredentials, "include");
                    var removal = await request.Content!.ReadFromJsonAsync<ProfileAccessRemovalRequest>(cancellationToken: cancellationToken);
                    RemovalEmail = removal?.Email;
                    savedParticipants.RemoveAll(value => value.Email == removal?.Email);
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                ParticipantPath = request.RequestUri.AbsolutePath;
                ParticipantCredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                    && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
                ParticipantRequested.TrySetResult();
                return participantResponse is not null
                    ? await participantResponse
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(savedParticipants.ToArray()) };
            }

            var details = new ProfileDetailsResponse(id, "Shared home", "GBP", [], role,
                [new ProfileCategoryDetailsResponse(Guid.NewGuid(), "Income", "income")],
                [new ProfilePaymentMethodDetailsResponse("cash")], [new ProfileTagDetailsResponse("IOU", "iou")]);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(details) };
        }
    }

    private sealed class RouteSwitchingHandler(Guid firstId, Guid secondId) : HttpMessageHandler
    {
        private readonly List<Guid> _requestedProfileIds = [];
        public Task<HttpResponseMessage>? FirstResponse { get; init; }
        public Task<HttpResponseMessage>? SecondResponse { get; init; }
        public IReadOnlyList<Guid> RequestedProfileIds => _requestedProfileIds;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) });

            var id = Guid.Parse(request.RequestUri.Segments[^1]);
            _requestedProfileIds.Add(id);
            if (id == firstId && FirstResponse is not null) return FirstResponse;
            if (id == secondId && SecondResponse is not null) return SecondResponse;
            if (id == firstId) return Task.FromResult(ProfileResponse(id, "First profile", "GBP", "view_only"));
            if (id == secondId) return Task.FromResult(ProfileResponse(id, "Second profile", "USD", "editor"));
            throw new InvalidOperationException($"Unexpected profile request: {id}.");
        }

        public static HttpResponseMessage ProfileResponse(Guid id, string name, string currency, string role)
        {
            var details = new ProfileDetailsResponse(id, name, currency, [], role,
                [new ProfileCategoryDetailsResponse(Guid.NewGuid(), "Income", "income")],
                [new ProfilePaymentMethodDetailsResponse("cash")],
                [new ProfileTagDetailsResponse("IOU", "iou")]);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(details) };
        }
    }
}
