using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task Nickname_length_feedback_uses_trimmed_length(int length, bool submits)
    {
        var id = Guid.NewGuid();
        var handler = new CardHandler("success");
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Add card", page.Markup));
        page.Find("#card-nickname").Input("  " + new string('x', length) + "  ");
        page.Find("#card-number").Input("4111111111111111");
        page.FindAll("button").Single(button => button.TextContent == "Save card").Click();
        if (submits) page.WaitForAssertion(() => Assert.Contains("Card saved.", page.Markup));
        else page.WaitForAssertion(() => Assert.Contains("Use at most 100 characters after trimming.", page.Markup));
        Assert.Equal(submits ? 1 : 0, handler.Posts);
    }

    [Fact]
    public async Task Viewers_see_nicknames_masked_numbers_and_fallback_labels_without_edit_controls()
    {
        var id = Guid.NewGuid();
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { profileId = id, profileName = "Home", role = "view_only", methods = new object[]
            {
                new { id = Guid.NewGuid(), kind = "cash", nickname = "Wallet" },
                new { id = Guid.NewGuid(), kind = "credit_card", nickname = "Travel <b>card</b>", maskedIdentifier = "•••• 1111" },
                new { id = Guid.NewGuid(), kind = "credit_card", nickname = (string?)null, maskedIdentifier = "•••• 4444" }
            } })
        }));
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Wallet", page.Find("ul").TextContent));
        Assert.Contains("Travel <b>card</b>", page.Find("ul").TextContent);
        Assert.Empty(page.FindAll("ul b"));
        Assert.Contains("•••• 1111", page.Find("ul").TextContent);
        Assert.Contains("Credit card", page.Find("ul").TextContent);
        Assert.Contains("•••• 4444", page.Find("ul").TextContent);
        Assert.Empty(page.FindAll("#card-nickname"));
    }

    [Fact]
    public async Task Nickname_is_entered_sent_displayed_and_cleared_after_success()
    {
        var id = Guid.NewGuid();
        var handler = new CardHandler("success");
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Add card", page.Markup));
        Assert.Single(page.FindAll("#card-nickname"));
        page.Find("#card-nickname").Input("  Travel card  ");
        page.Find("#card-number").Input("4111111111111111");
        page.FindAll("button").Single(button => button.TextContent == "Save card").Click();
        page.WaitForAssertion(() => Assert.Contains("Card saved.", page.Markup));
        Assert.Equal("  Travel card  ", handler.SubmittedNickname);
        Assert.Contains("Travel card", page.Find("ul").TextContent);
        Assert.Contains("•••• 1111", page.Find("ul").TextContent);
        Assert.Equal("", page.Find("#card-nickname").GetAttribute("value") ?? "");
    }

    [Theory]
    [InlineData("owner", "Owner")]
    [InlineData("editor", "Editor")]
    [InlineData("view_only", "View only")]
    public async Task Deep_link_loads_cash_and_shows_only_current_writer_controls(string role, string label)
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
        if (role is "owner" or "editor") Assert.Contains("Add card", page.Markup);
        else Assert.DoesNotContain("Add card", page.Markup);
        Assert.DoesNotContain("Reveal", page.Markup);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("invalid")]
    [InlineData("duplicate")]
    [InlineData("network")]
    [InlineData("dependency")]
    public async Task Save_reports_masked_success_field_errors_or_uncertainty_with_explicit_refresh(string outcome)
    {
        var id = Guid.NewGuid();
        var handler = new CardHandler(outcome);
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Add card", page.Markup));
        page.Find("#card-number").Input("4111111111111111");
        page.Find("#card-nickname").Input("Travel card");
        page.FindAll("button").Single(button => button.TextContent == "Save card").Click();
        page.WaitForAssertion(() => Assert.Equal(1, handler.Posts));
        Assert.Equal("4111111111111111", handler.SubmittedNumber);
        Assert.Equal("Travel card", handler.SubmittedNickname);
        if (outcome == "success")
        {
            page.WaitForAssertion(() => Assert.Contains("Card saved.", page.Markup));
            Assert.Contains("•••• 1111", page.Find("ul").TextContent);
            Assert.DoesNotContain("4111111111111111", page.Markup);
            Assert.Equal("", page.Find("#card-number").GetAttribute("value") ?? "");
            Assert.Equal("", page.Find("#card-nickname").GetAttribute("value") ?? "");
        }
        else if (outcome is "invalid" or "duplicate")
        {
            page.WaitForAssertion(() => Assert.Contains("Synthetic safe field error", page.Markup));
            Assert.Contains("4111111111111111", page.Find("#card-number").GetAttribute("value"));
            Assert.Equal("Travel card", page.Find("#card-nickname").GetAttribute("value"));
        }
        else
        {
            page.WaitForAssertion(() => Assert.Contains("couldn't confirm", page.Markup));
            Assert.DoesNotContain(page.FindAll("button"), button => button.TextContent == "Retry save");
            page.FindAll("button").Single(button => button.TextContent == "Refresh payment methods").Click();
            page.WaitForAssertion(() => Assert.Contains(page.FindAll("button"), button => button.TextContent == "Retry save"));
            Assert.Equal(1, handler.Posts);
            Assert.Equal(2, handler.Lists);
            Assert.Equal("Travel card", page.Find("#card-nickname").GetAttribute("value"));
        }
        Assert.True(handler.HasProof);
    }

    [Fact]
    public async Task Pending_save_disables_submission_and_route_change_discards_draft_and_late_result()
    {
        var id = Guid.NewGuid();
        var handler = new CardHandler("pending");
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Add card", page.Markup));
        page.Find("#card-number").Input("4111111111111111");
        page.Find("#card-nickname").Input("Pending nickname");
        var click = page.FindAll("button").Single(button => button.TextContent == "Save card").ClickAsync(new());
        page.WaitForAssertion(() => Assert.True(page.Find("#card-number").HasAttribute("disabled")));
        Assert.Equal(1, handler.Posts);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(Route(Guid.NewGuid()));
        handler.Pending.SetResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = Guid.NewGuid(), kind = "credit_card", maskedIdentifier = "•••• 1111" }) });
        await click;
        page.WaitForAssertion(() => Assert.DoesNotContain("4111111111111111", page.Markup));
        Assert.DoesNotContain("Card saved.", page.Markup);
        Assert.DoesNotContain("Pending nickname", page.Markup);
    }

    private sealed class CardHandler(string outcome) : HttpMessageHandler
    {
        public int Posts; public int Lists; public bool HasProof; public string? SubmittedNumber; public string? SubmittedNickname;
        public TaskCompletionSource<HttpResponseMessage> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/auth/session") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = true, email = "person@example.test" }) });
            if (path == "/api/auth/antiforgery") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "proof" }) });
            if (request.Method == HttpMethod.Post)
            {
                Posts++;
                using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
                SubmittedNumber = body.RootElement.GetProperty("number").GetString();
                SubmittedNickname = body.RootElement.TryGetProperty("nickname", out var nickname) ? nickname.GetString() : null;
                HasProof = request.Headers.GetValues("X-CSRF-TOKEN").Single() == "proof";
                if (outcome == "pending") return Pending.Task;
                if (outcome == "network") return Task.FromException<HttpResponseMessage>(new HttpRequestException());
                if (outcome == "dependency") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                if (outcome is "session" or "forbidden" or "unavailable") return Task.FromResult(new HttpResponseMessage(outcome switch
                { "session" => HttpStatusCode.Unauthorized, "forbidden" => HttpStatusCode.Forbidden, _ => HttpStatusCode.NotFound }));
                if (outcome is "invalid" or "duplicate") return Task.FromResult(new HttpResponseMessage(outcome == "invalid" ? HttpStatusCode.BadRequest : HttpStatusCode.Conflict)
                { Content = JsonContent.Create(new { errors = new { Number = new[] { "Synthetic safe field error" } } }) });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { id = Guid.NewGuid(), kind = "credit_card", maskedIdentifier = "•••• 1111" }) });
            }
            Lists++;
            var current = Guid.Parse(path.Split('/')[3]);
            return Task.FromResult(Posts > 0 && outcome == "success"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { profileId = current, profileName = "Home", role = "owner", methods = new[] { new { id = Guid.NewGuid(), kind = "credit_card", maskedIdentifier = "•••• 1111", nickname = SubmittedNickname?.Trim() } } }) }
                : Methods(current, "Home"));
        }
    }

    [Theory]
    [InlineData("session")]
    [InlineData("forbidden")]
    [InlineData("unavailable")]
    public async Task Observed_session_or_access_loss_clears_card_entry_and_removes_write_controls(string outcome)
    {
        var id = Guid.NewGuid();
        var handler = new CardHandler(outcome);
        await using var context = CreateContext(handler, Route(id));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("Add card", page.Markup));
        page.Find("#card-number").Input("4111111111111111");
        page.FindAll("button").Single(button => button.TextContent == "Save card").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("#card-number")));
        Assert.DoesNotContain("4111111111111111", page.Markup);
        Assert.DoesNotContain("Add card", page.Markup);
        Assert.DoesNotContain("Retry save", page.Markup);
        Assert.Equal(1, handler.Posts);
        if (outcome == "session") Assert.Contains("/login?returnUrl=", context.Services.GetRequiredService<NavigationManager>().Uri);
        if (outcome == "forbidden") Assert.Contains("Your access: View only", page.Markup);
        if (outcome == "unavailable") Assert.Contains("Budget profile unavailable", page.Markup);
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
