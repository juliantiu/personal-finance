using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PersonalFinance.Client.Pages;
using PersonalFinance.Contracts.Accounts;
using Xunit;

namespace PersonalFinance.Client.Tests.Accounts;

public sealed class RegisterPageTests
{
    [Fact]
    public void Invalid_registration_does_not_send_a_request()
    {
        using var context = CreateContext(new StubHandler(_ => throw new InvalidOperationException("No request expected.")));
        var page = context.Render<Register>();

        page.Find("input[type='email']").Change("not-an-email");
        page.Find("input[type='password']").Change("short");
        page.FindAll("input[type='password']")[1].Change("different");
        page.Find("button.mud-button-root").Click();

        Assert.Contains("Enter a valid email address.", page.Markup);
        Assert.Contains("Use at least 12 characters.", page.Markup);
        Assert.Empty(context.Services.GetRequiredService<StubHandler>().Requests);
    }

    [Fact]
    public void Valid_registration_posts_contract_and_shows_acknowledgement()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted));
        using var context = CreateContext(handler);
        var page = context.Render<Register>();
        FillValidForm(page);

        page.Find("button.mud-button-root").Click();

        page.WaitForAssertion(() => Assert.Contains("Request accepted.", page.Markup));
        var sent = Assert.Single(handler.Requests);
        Assert.Equal("api/accounts/register", sent.Path);
        Assert.Equal(new RegisterAccountRequest("person@example.test", "long-enough-pass", "long-enough-pass"), sent.Request);
    }

    [Fact]
    public void Server_validation_errors_are_shown_and_email_is_retained()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { title = "Invalid request", errors = new { Email = new[] { "Email is not accepted." } } })
        });
        using var context = CreateContext(handler);
        var page = context.Render<Register>();
        FillValidForm(page);

        page.Find("button.mud-button-root").Click();

        page.WaitForAssertion(() => Assert.Contains("Email is not accepted.", page.Markup));
        Assert.Equal("person@example.test", page.Find("input[type='email']").GetAttribute("value"));
        Assert.DoesNotContain("Request accepted.", page.Markup);
    }

    [Fact]
    public void Recoverable_server_failure_keeps_email_and_allows_retry()
    {
        var attempt = 0;
        var handler = new StubHandler(_ => ++attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.Accepted));
        using var context = CreateContext(handler);
        var page = context.Render<Register>();
        FillValidForm(page);

        page.Find("button.mud-button-root").Click();
        page.WaitForAssertion(() => Assert.Contains("temporarily unavailable", page.Markup));
        Assert.Equal("person@example.test", page.Find("input[type='email']").GetAttribute("value"));

        page.Find("button.mud-button-root").Click();
        page.WaitForAssertion(() => Assert.Contains("Request accepted.", page.Markup));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void Connection_failure_keeps_email_and_allows_retry()
    {
        var attempt = 0;
        var handler = new StubHandler(_ =>
        {
            if (++attempt == 1) throw new HttpRequestException();
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        using var context = CreateContext(handler);
        var page = context.Render<Register>();
        FillValidForm(page);

        page.Find("button.mud-button-root").Click();
        page.WaitForAssertion(() => Assert.Contains("couldn’t reach the service", page.Markup));
        Assert.Equal("person@example.test", page.Find("input[type='email']").GetAttribute("value"));

        page.Find("button.mud-button-root").Click();
        page.WaitForAssertion(() => Assert.Contains("Request accepted.", page.Markup));
        Assert.Equal(2, handler.Requests.Count);
    }

    private static Bunit.BunitContext CreateContext(StubHandler handler)
    {
        var context = new Bunit.BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(handler);
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });
        return context;
    }

    private static void FillValidForm(IRenderedComponent<Register> page)
    {
        page.Find("input[type='email']").Change("person@example.test");
        page.FindAll("input[type='password']")[0].Change("long-enough-pass");
        page.FindAll("input[type='password']")[1].Change("long-enough-pass");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<(string Path, RegisterAccountRequest Request)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<RegisterAccountRequest>(cancellationToken);
            Requests.Add((request.RequestUri!.PathAndQuery.TrimStart('/'), body!));
            return responseFactory(request);
        }
    }
}
