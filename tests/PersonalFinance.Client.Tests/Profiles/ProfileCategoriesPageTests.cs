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

public sealed class ProfileCategoriesPageTests
{
    [Theory]
    [InlineData("owner", "Owner")]
    [InlineData("editor", "Editor")]
    [InlineData("view_only", "View only")]
    public async Task Participants_review_categories_and_only_writers_receive_mutation_controls(string role, string label)
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler((profileId, _) => Task.FromResult(Categories(profileId, role)));
        await using var context = CreateContext(handler, id);

        var page = context.Render<App>();

        page.WaitForAssertion(() => Assert.Contains("Wages", page.Markup));
        Assert.Contains($"Your access: {label}", page.Markup);
        var items = page.FindAll("ul[aria-label='Profile categories'] li");
        Assert.Equal(3, items.Count);
        Assert.Contains(items, item => item.TextContent.Contains("System category: Income"));
        Assert.Contains(items, item => item.TextContent.Contains("System category: Savings"));
        Assert.Contains(items, item => item.TextContent.Contains("System category: Debt"));
        Assert.Contains($"href=\"/budget-profiles/{id}\"", page.Markup);
        if (role == "view_only")
        {
            Assert.Contains(items, item => item.TextContent.Contains("Wages"));
            Assert.Contains(items, item => item.TextContent.Contains("Rainy day"));
            Assert.Contains(items, item => item.TextContent.Contains("Loans"));
            Assert.Empty(page.FindAll("input, textarea, form"));
            Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent.Contains("Save") || button.TextContent.Contains("Add category"));
        }
        else
        {
            Assert.Contains(page.FindAll("input"), input => input.GetAttribute("value") == "Wages");
            Assert.Contains(page.FindAll("input"), input => input.GetAttribute("value") == "Rainy day");
            Assert.Contains(page.FindAll("input"), input => input.GetAttribute("value") == "Loans");
            Assert.Contains(page.FindAll("button"), button => button.TextContent.Contains("Add category"));
            Assert.Equal(3, page.FindAll("button").Count(button => button.TextContent.Contains("Save category")));
        }
        Assert.True(handler.CredentialsIncluded);
        Assert.Equal(new[] { id }, handler.RequestedIds);
    }

    [Fact]
    public async Task Loading_clears_content_and_successful_empty_read_is_distinct_from_failure()
    {
        var id = Guid.NewGuid();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = CreateContext(new StubHandler((_, _) => completion.Task), id);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Loading categories", page.Markup));
        Assert.DoesNotContain("No categories", page.Markup);
        completion.SetResult(Categories(id, empty: true));
        page.WaitForAssertion(() => Assert.Contains("No categories are available for this profile.", page.Markup));
        Assert.DoesNotContain("role=\"alert\"", page.Markup);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("network")]
    [InlineData("json")]
    [InlineData("null")]
    public async Task Failed_load_has_truthful_feedback_and_can_be_retried(string failure)
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler((profileId, attempt) => attempt > 1
            ? Task.FromResult(Categories(profileId))
            : failure switch
            {
                "network" => Task.FromException<HttpResponseMessage>(new HttpRequestException("Synthetic network failure")),
                "json" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid json") }),
                "null" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<object?>(null) }),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            });
        await using var context = CreateContext(handler, id);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("We couldn't load categories.", page.Find("[role='alert']").TextContent));
        Assert.DoesNotContain("No categories", page.Markup);
        Assert.DoesNotContain("Wages", page.Markup);

        page.FindAll("button").Single(button => button.TextContent.Contains("Try again")).Click();

        page.WaitForAssertion(() => Assert.Contains("Wages", page.Markup));
        Assert.Empty(page.FindAll("[role='alert']"));
        Assert.Equal(2, handler.RequestedIds.Count);
    }

    [Fact]
    public async Task Unavailable_profile_shows_no_category_data_and_navigation_to_profiles()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))), id);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Categories unavailable", page.Markup));
        Assert.Empty(page.FindAll("ul[aria-label='Profile categories']"));
        Assert.Contains("href=\"/budget-profiles\"", page.Markup);
    }

    [Fact]
    public async Task Lost_session_clears_data_and_redirects_to_login_with_the_category_return_path()
    {
        var id = Guid.NewGuid();
        await using var context = CreateContext(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))), id);
        Task<AuthenticationState>? signedOut = null;
        context.Services.GetRequiredService<BffAuthenticationStateProvider>().AuthenticationStateChanged += state => signedOut = state;
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Equal($"http://localhost/login?returnUrl=%2Fbudget-profiles%2F{id}%2Fcategories",
            context.Services.GetRequiredService<NavigationManager>().Uri));
        Assert.DoesNotContain("Wages", page.Markup);
        Assert.NotNull(signedOut);
        var state = await signedOut;
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Route_change_immediately_clears_old_categories_while_loading_the_new_profile()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = CreateContext(new StubHandler((id, _) => id == first
            ? Task.FromResult(Categories(id)) : completion.Task), first);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Wages", page.Markup));

        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/budget-profiles/{second}/categories");

        page.WaitForAssertion(() => Assert.Contains("Loading categories", page.Markup));
        Assert.DoesNotContain("Wages", page.Markup);
        completion.SetResult(Categories(second, "editor", incomeName: "Second earnings"));
        page.WaitForAssertion(() => Assert.Contains("Second earnings", page.Markup));
        Assert.DoesNotContain("Wages", page.Markup);
        Assert.Contains($"href=\"/budget-profiles/{second}\"", page.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_previous_response_cannot_replace_current_categories_or_redirect_current_route(bool unauthorized)
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHandler((id, _) => id == first ? completion.Task : Task.FromResult(Categories(id, "editor", incomeName: "Second earnings")));
        await using var context = CreateContext(handler, first);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Loading categories", page.Markup));
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/budget-profiles/{second}/categories");
        page.WaitForAssertion(() => Assert.Contains("Second earnings", page.Markup));

        completion.SetResult(unauthorized ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Categories(first));
        // Drain the old HTTP operation and the renderer before asserting the retained route/content.
        await page.InvokeAsync(async () => await handler.FirstRequestFinished.Task);
        await page.InvokeAsync(() => { });

        Assert.EndsWith($"/budget-profiles/{second}/categories", navigation.Uri);
        Assert.Contains("Second earnings", page.Markup);
        Assert.DoesNotContain("Wages", page.Markup);
        Assert.Equal(new[] { first, second }, handler.RequestedIds);
    }

    [Fact]
    public async Task Writer_can_create_and_edit_and_conflict_reload_preserves_the_draft()
    {
        var id = Guid.NewGuid();
        var handler = new MutationHandler(id);
        await using var context = CreateContext(handler, id);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Wages", page.Markup));

        page.FindAll("input")[0].Change("Home");
        page.FindAll("input")[1].Change("Family budget");
        page.FindAll("button").Single(button => button.TextContent.Contains("Add category")).Click();
        page.WaitForAssertion(() => Assert.Single(handler.Writes));
        Assert.Contains("\"name\":\"Home\"", handler.Writes[0]);
        Assert.Contains("X-CSRF-TOKEN", handler.Headers);

        page.FindAll("input").Single(input => input.GetAttribute("value") == "Wages").Change("Draft earnings");
        page.FindAll("button").First(button => button.TextContent.Contains("Save category")).Click();
        page.WaitForAssertion(() => Assert.Contains("changed since you opened it", page.Markup));
        page.FindAll("button").Single(button => button.TextContent.Contains("Load latest category")).Click();
        page.WaitForAssertion(() => Assert.Contains("Draft earnings", page.Markup));
        Assert.Contains(page.FindAll("input"), input => input.GetAttribute("value") == "Draft earnings");
        page.FindAll("button").First(button => button.TextContent.Contains("Save category")).Click();
        page.WaitForAssertion(() => Assert.Contains("Draft earnings", page.Markup));
        Assert.Contains(handler.Writes, body => body.Contains("Draft earnings", StringComparison.Ordinal));
        Assert.Contains(handler.Writes, body => body.Contains("BAUG", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unknown_create_outcome_requires_review_before_the_user_can_retry()
    {
        var id = Guid.NewGuid();
        var handler = new MutationHandler(id, loseCreateResponse: true);
        await using var context = CreateContext(handler, id);
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Wages", page.Markup));

        page.FindAll("input")[0].Change("Possibly saved");
        page.FindAll("button").Single(button => button.TextContent.Contains("Add category")).Click();
        page.WaitForAssertion(() => Assert.Contains("Review the saved categories", page.Markup));
        var addButton = page.FindAll("button").Single(button => button.TextContent.Contains("Add category"));
        Assert.True(addButton.HasAttribute("disabled"));
        Assert.Single(handler.Writes);

        page.FindAll("button").Single(button => button.TextContent.Contains("Review categories")).Click();
        page.WaitForAssertion(() => Assert.DoesNotContain("Review the saved categories", page.Markup));
        Assert.Single(handler.Writes);
        Assert.False(page.FindAll("button").Single(button => button.TextContent.Contains("Add category")).HasAttribute("disabled"));
    }

    private static HttpResponseMessage Categories(Guid id, string role = "owner", bool empty = false, string incomeName = "Wages") => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            profileId = id, role,
            categories = empty ? [] : new[]
            {
                new { id = Guid.Parse("11111111-1111-1111-1111-111111111111"), name = incomeName, kind = "income", comment = (string?)null, version = "AQID" },
                new { id = Guid.Parse("22222222-2222-2222-2222-222222222222"), name = "Rainy day", kind = "savings", comment = (string?)null, version = "AQIE" },
                new { id = Guid.Parse("33333333-3333-3333-3333-333333333333"), name = "Loans", kind = "debt", comment = (string?)null, version = "AQIF" }
            }
        })
    };

    private static BunitContext CreateContext(HttpMessageHandler handler, Guid profileId)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.RemoveAll<IAuthorizationService>();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") });
        context.Services.AddSingleton<BffAuthenticationStateProvider>();
        context.Services.AddSingleton<AuthenticationStateProvider>(provider => provider.GetRequiredService<BffAuthenticationStateProvider>());
        context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/budget-profiles/{profileId}/categories");
        return context;
    }

    private sealed class StubHandler(Func<Guid, int, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Guid> RequestedIds { get; } = [];
        public bool CredentialsIncluded { get; private set; }
        public TaskCompletionSource FirstRequestFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) };
            if (request.RequestUri.AbsolutePath == "/api/auth/antiforgery")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "synthetic-csrf" }) };
            Assert.EndsWith("/categories", request.RequestUri.AbsolutePath);
            var id = Guid.Parse(request.RequestUri.Segments[^2].TrimEnd('/'));
            RequestedIds.Add(id);
            var attempt = RequestedIds.Count;
            CredentialsIncluded = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
            try { return await respond(id, attempt); }
            finally { if (attempt == 1) FirstRequestFinished.TrySetResult(); }
        }
    }

    private sealed class MutationHandler(Guid profileId, bool loseCreateResponse = false) : HttpMessageHandler
    {
        private int _reads;
        private int _edits;
        public List<string> Writes { get; } = [];
        public List<string> Headers { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) };
            if (request.RequestUri.AbsolutePath == "/api/auth/antiforgery")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "synthetic-csrf" }) };
            if (request.Method == HttpMethod.Get)
            {
                _reads++;
                return CategoriesForMutation(profileId, currentVersion: _reads > 2 ? "BAUG" : "AQID", categoryName: _edits > 1 ? "Draft earnings" : "Wages");
            }
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Writes.Add(body);
            Headers.AddRange(request.Headers.Select(header => header.Key));
            if (request.Method == HttpMethod.Post)
            {
                if (loseCreateResponse) throw new HttpRequestException("Synthetic lost create response.");
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = Guid.NewGuid() }) };
            }
            _edits++;
            return new HttpResponseMessage(_edits == 1 ? HttpStatusCode.Conflict : HttpStatusCode.OK);
        }
    }

    private static HttpResponseMessage CategoriesForMutation(Guid id, string currentVersion, string categoryName) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            profileId = id,
            role = "owner",
            categories = new[]
            {
                new { id = Guid.Parse("11111111-1111-1111-1111-111111111111"), name = categoryName, kind = "income", comment = (string?)null, version = currentVersion },
                new { id = Guid.Parse("22222222-2222-2222-2222-222222222222"), name = "Savings", kind = "savings", comment = (string?)null, version = "AQIE" },
                new { id = Guid.Parse("33333333-3333-3333-3333-333333333333"), name = "Debt", kind = "debt", comment = (string?)null, version = "AQIF" }
            }
        })
    };
}
