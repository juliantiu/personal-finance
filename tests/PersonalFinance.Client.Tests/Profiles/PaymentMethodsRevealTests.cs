using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Client;
using PersonalFinance.Client.Accounts;
using Xunit;

namespace PersonalFinance.Client.Tests.Profiles;

public sealed partial class PaymentMethodsPageTests
{
    [Theory]
    [InlineData("owner")]
    [InlineData("editor")]
    [InlineData("view_only")]
    public async Task Explicit_reveal_is_available_only_for_writer_cards_and_hide_and_close_clear_it(string role)
    {
        var handler = new RevealHandler { Role = role };
        await using var context = CreateContext(handler, Route(Guid.NewGuid()));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("•••• 1111", page.Markup));
        Assert.Equal(0, handler.Reveals);
        Assert.DoesNotContain("4111111111111111", page.Markup);
        if (role == "view_only") { Assert.DoesNotContain(page.FindAll("button"), x => x.TextContent == "Reveal card"); return; }
        Assert.Single(page.FindAll("button"), x => x.TextContent == "Reveal card");
        page.FindAll("button").Single(x => x.TextContent == "Reveal card").Click();
        page.WaitForAssertion(() => Assert.Contains("4111111111111111", page.Markup));
        Assert.True(handler.Proof && handler.Credentials);
        page.FindAll("button").Single(x => x.TextContent == "Hide number").Click();
        page.WaitForAssertion(() => Assert.DoesNotContain("4111111111111111", page.Markup));
        page.FindAll("button").Single(x => x.TextContent == "Reveal number").Click();
        page.WaitForAssertion(() => Assert.Equal(2, handler.Reveals));
        page.WaitForAssertion(() => Assert.Contains("4111111111111111", page.Markup));
        page.FindAll("button").Single(x => x.TextContent == "Close").Click();
        page.WaitForAssertion(() => Assert.DoesNotContain("4111111111111111", page.Markup));
        Assert.Empty(page.FindAll("[role=dialog]"));
    }

    [Theory]
    [InlineData("close")]
    [InlineData("route")]
    [InlineData("signout")]
    public async Task Leaving_reveal_context_discards_pending_and_loaded_plaintext(string action)
    {
        var handler = new RevealHandler { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = CreateContext(handler, Route(Guid.NewGuid()));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("•••• 1111", page.Markup));
        var click = page.FindAll("button").Single(x => x.TextContent == "Reveal card").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Equal(1, handler.Reveals));
        if (action == "close") page.FindAll("button").Single(x => x.TextContent == "Close").Click();
        if (action == "route") context.Services.GetRequiredService<NavigationManager>().NavigateTo(Route(Guid.NewGuid()));
        if (action == "signout") await page.InvokeAsync(() => context.Services.GetRequiredService<BffAuthenticationStateProvider>().NotifySignedOut());
        handler.Pending.SetResult(RevealHandler.Number());
        await click;
        Assert.DoesNotContain("4111111111111111", page.Markup);
        Assert.Empty(page.FindAll("[role=dialog]"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Reveal_failure_does_not_display_plaintext_or_retry_automatically(HttpStatusCode status)
    {
        var handler = new RevealHandler { Status = status };
        await using var context = CreateContext(handler, Route(Guid.NewGuid()));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("•••• 1111", page.Markup));
        page.FindAll("button").Single(x => x.TextContent == "Reveal card").Click();
        page.WaitForAssertion(() => Assert.Equal(1, handler.Reveals));
        page.WaitForAssertion(() => Assert.True(status == HttpStatusCode.Unauthorized
            ? context.Services.GetRequiredService<NavigationManager>().Uri.Contains("/login")
            : status == HttpStatusCode.Forbidden ? page.Markup.Contains("Your access: View only")
            : page.Markup.Contains("couldn't reveal")));
        Assert.DoesNotContain("4111111111111111", page.Markup);
        Assert.Equal(1, handler.Reveals);
    }

    [Theory]
    [InlineData("route")]
    [InlineData("signout")]
    public async Task Loaded_plaintext_is_removed_when_the_page_context_ends(string action)
    {
        var handler = new RevealHandler();
        await using var context = CreateContext(handler, Route(Guid.NewGuid()));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("•••• 1111", page.Markup));
        page.FindAll("button").Single(x => x.TextContent == "Reveal card").Click();
        page.WaitForAssertion(() => Assert.Contains("4111111111111111", page.Markup));
        if (action == "route") context.Services.GetRequiredService<NavigationManager>().NavigateTo(Route(Guid.NewGuid()));
        else await page.InvokeAsync(() => context.Services.GetRequiredService<BffAuthenticationStateProvider>().NotifySignedOut());
        page.WaitForAssertion(() => Assert.DoesNotContain("4111111111111111", page.Markup));
        Assert.Empty(page.FindAll("[role=dialog]"));
    }

    [Fact]
    public async Task Pending_reveal_prevents_double_submission_and_hide_rejects_its_late_response()
    {
        var handler = new RevealHandler { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using var context = CreateContext(handler, Route(Guid.NewGuid()));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("•••• 1111", page.Markup));
        var first = page.FindAll("button").Single(x => x.TextContent == "Reveal card").ClickAsync(new());
        page.WaitForAssertion(() => Assert.Equal(1, handler.Reveals));
        Assert.DoesNotContain(page.FindAll("button"), x => x.TextContent == "Reveal number");
        page.FindAll("button").Single(x => x.TextContent == "Hide number").Click();
        handler.Pending.SetResult(RevealHandler.Number());
        await first;
        Assert.DoesNotContain("4111111111111111", page.Markup);
        Assert.Equal(1, handler.Reveals);
        Assert.Single(page.FindAll("[role=dialog]"));
    }

    [Theory]
    [InlineData("network")]
    [InlineData("json")]
    [InlineData("proof")]
    public async Task Reveal_transport_or_invalid_response_has_safe_feedback_and_no_automatic_retry(string failure)
    {
        var handler = new RevealHandler { Failure = failure };
        await using var context = CreateContext(handler, Route(Guid.NewGuid()));
        var page = context.Render<App>();
        page.WaitForAssertion(() => Assert.Contains("•••• 1111", page.Markup));
        page.FindAll("button").Single(x => x.TextContent == "Reveal card").Click();
        page.WaitForAssertion(() => Assert.Contains("couldn't reveal", page.Markup));
        Assert.DoesNotContain("4111111111111111", page.Markup);
        Assert.Equal(failure == "proof" ? 0 : 1, handler.Reveals);
    }

    private sealed class RevealHandler : HttpMessageHandler
    {
        public string Role = "owner";
        public int Reveals;
        public bool Proof, Credentials;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public TaskCompletionSource<HttpResponseMessage>? Pending;
        public string? Failure;
        public static HttpResponseMessage Number() => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { number = "4111111111111111" }) };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/auth/session") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { authenticated = true }) });
            if (path == "/api/auth/antiforgery") return Task.FromResult(Failure == "proof" ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { requestToken = "proof" }) });
            if (path.EndsWith("/reveal"))
            {
                Reveals++;
                if (Failure == "network") return Task.FromException<HttpResponseMessage>(new HttpRequestException());
                if (Failure == "json") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("invalid") });
                Proof = request.Method == HttpMethod.Post && request.Headers.Contains("X-CSRF-TOKEN");
                Credentials = request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options)
                    && options.TryGetValue("credentials", out var credentials) && Equals(credentials, "include");
                return Pending?.Task ?? Task.FromResult(Status == HttpStatusCode.OK ? Number() : new HttpResponseMessage(Status));
            }
            var profile = Guid.Parse(path.Split('/')[3]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { profileId = profile, profileName = "Reveal home", role = Role,
                methods = new object[] { new { id = Guid.NewGuid(), kind = "cash" }, new { id = Guid.NewGuid(), kind = "credit_card", maskedIdentifier = "•••• 1111" } } }) });
        }
    }
}
