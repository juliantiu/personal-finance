using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using PersonalFinance.Client.Pages;
using Xunit;

namespace PersonalFinance.Client.Tests.Accounts;

public sealed class ConfirmationPageTests
{
    [Fact]
    public void Valid_link_posts_confirmation_and_shows_success_without_signing_in()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        using var context = CreateContext(handler);
        context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("http://localhost/confirm-email#accountId=5d0f8819-f74b-4dc5-91ad-229d169102fa&token=opaque%2Btoken%26reserved%3Dvalue");

        var page = context.Render<ConfirmEmail>();

        page.WaitForAssertion(() => Assert.Contains("Email confirmed", page.Markup));
        var request = Assert.Single(handler.Requests);
        Assert.Equal("api/accounts/confirm-email", request.Path);
        using var body = System.Text.Json.JsonDocument.Parse(request.Body);
        Assert.Equal("5d0f8819-f74b-4dc5-91ad-229d169102fa", body.RootElement.GetProperty("accountId").GetString());
        Assert.Equal("opaque+token&reserved=value", body.RootElement.GetProperty("token").GetString());
        Assert.DoesNotContain("token", context.Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Expired_link_shows_failure_and_allows_resend()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest);
        using var context = CreateContext(handler);
        context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo("http://localhost/confirm-email#accountId=5d0f8819-f74b-4dc5-91ad-229d169102fa&token=invalid");

        var page = context.Render<ConfirmEmail>();

        page.WaitForAssertion(() => Assert.Contains("invalid or expired", page.Markup, StringComparison.OrdinalIgnoreCase));
        page.Find("input[type='email']").Change("person@example.test");
        page.Find("button.mud-button-root").Click();

        page.WaitForAssertion(() => Assert.Contains("Request accepted.", page.Markup));
        Assert.Equal(new[] { "api/accounts/confirm-email", "api/accounts/resend-confirmation" }, handler.Requests.Select(request => request.Path));
    }

    private static BunitContext CreateContext(StubHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });
        return context;
    }

    private sealed class StubHandler(HttpStatusCode confirmationStatus) : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri!.PathAndQuery.TrimStart('/'), body));
            return new HttpResponseMessage(request.RequestUri.AbsolutePath.EndsWith("confirm-email", StringComparison.Ordinal)
                ? confirmationStatus
                : HttpStatusCode.Accepted);
        }
    }
}
