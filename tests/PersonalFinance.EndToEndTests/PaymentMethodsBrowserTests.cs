using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class PaymentMethodsBrowserTests
{
    [Fact]
    public async Task Editor_saves_masked_cards_and_refreshes_before_retrying_a_lost_save_response()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        Guid ownerId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "save-card@example.test", Email = "save-card@example.test", EmailConfirmed = true };
            var owner = new AccountIdentityUser { UserName = "card-owner@example.test", Email = "card-owner@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(account, "abcdefghijkl")).Succeeded);
            Assert.True((await manager.CreateAsync(owner, "abcdefghijkl")).Succeeded);
            accountId = account.Id; ownerId = owner.Id;
        }
        var profileId = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = ownerId, Name = "Synthetic card profile", MainCurrencyCode = "GBP" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profileId, AccountId = accountId, Role = "editor" });
            db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profileId, Kind = "cash" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var page = browser.Page;
        var route = $"/budget-profiles/{profileId}/payment-methods";
        await page.GotoAsync(browser.ClientUrl + route);
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("save-card@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + route, new() { Timeout = 15000 });
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Add card", Exact = true })).ToBeVisibleAsync(new() { Timeout = 15000 });
        var input = page.GetByLabel("Card number", new() { Exact = true });
        await Expect(input).ToHaveAttributeAsync("type", "password");
        await Expect(input).ToHaveAttributeAsync("autocomplete", "off");
        var nickname = page.GetByLabel("Nickname (optional)", new() { Exact = true });
        await nickname.FillAsync("  Travel card  ");
        await input.FillAsync("4111111111111112");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save card", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Enter 12–19 digits with a valid card checksum. Spaces and hyphens are allowed.", new() { Exact = true })).ToBeVisibleAsync();
        await input.FillAsync("4111-1111 1111 1111");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save card", Exact = true }).ClickAsync();
        try { await Expect(page.GetByText("Card saved.", new() { Exact = true })).ToBeVisibleAsync(); }
        catch (PlaywrightException)
        { throw new InvalidOperationException("Synthetic card save failed: " + await page.Locator("body").InnerTextAsync() + " | " + string.Join(" | ", browser.ApiResponses)); }
        await Expect(input).ToHaveValueAsync("");
        await Expect(nickname).ToHaveValueAsync("");
        var methods = page.GetByRole(AriaRole.List, new() { Name = "Profile payment methods" });
        await Expect(methods).ToContainTextAsync("•••• 1111");
        await Expect(methods).ToContainTextAsync("Travel card");
        await page.ReloadAsync();
        await Expect(methods).ToContainTextAsync("•••• 1111");
        await Expect(methods).ToContainTextAsync("Travel card");
        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profile", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.List, new() { Name = "Profile payment methods" })).ToContainTextAsync("Travel card");
        await page.GetByRole(AriaRole.Link, new() { Name = "Review payment methods", Exact = true }).ClickAsync();
        await input.FillAsync("4111111111111111");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save card", Exact = true }).ClickAsync();
        await Expect(page.GetByText("This card is already saved in this profile.", new() { Exact = true })).ToBeVisibleAsync();
        var posts = 0;
        await page.RouteAsync($"**/api/budget-profiles/{profileId}/payment-methods", async intercepted =>
        {
            if (intercepted.Request.Method != "POST") { await intercepted.ContinueAsync(); return; }
            posts++;
            await using var accepted = await intercepted.FetchAsync();
            Assert.Equal(201, accepted.Status);
            await intercepted.AbortAsync();
        });
        await input.FillAsync("5555555555554444");
        await nickname.FillAsync("Emergency card");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save card", Exact = true }).ClickAsync();
        await Expect(page.GetByText("We couldn't confirm whether your card was saved.", new() { Exact = false })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Retry save", Exact = true })).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Refresh payment methods", Exact = true }).ClickAsync();
        await Expect(methods).ToContainTextAsync("•••• 4444");
        await Expect(methods).ToContainTextAsync("Emergency card");
        await Expect(nickname).ToHaveValueAsync("Emergency card");
        await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Retry save", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(1, posts);
        await page.SetViewportSizeAsync(390, 844);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await browser.Api.WithDatabaseAsync(db => db.ProfileAccess.Where(value => value.ProfileId == profileId)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.Role, "view_only"), TestContext.Current.CancellationToken));
        await page.ReloadAsync();
        await Expect(methods).ToContainTextAsync("•••• 1111");
        await Expect(methods).ToContainTextAsync("•••• 4444");
        await Expect(input).ToHaveCountAsync(0);
        Assert.Empty(browser.PageErrors);
    }

    [Fact]
    public async Task Participant_can_deep_link_reload_and_navigate_to_methods_until_access_is_revoked()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        Guid ownerId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var participant = new AccountIdentityUser { UserName = "methods-browser@example.test", Email = "methods-browser@example.test", EmailConfirmed = true };
            var owner = new AccountIdentityUser { UserName = "methods-owner@example.test", Email = "methods-owner@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(participant, "abcdefghijkl")).Succeeded);
            Assert.True((await manager.CreateAsync(owner, "abcdefghijkl")).Succeeded);
            accountId = participant.Id;
            ownerId = owner.Id;
        }
        var profileId = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = ownerId, Name = "Payment methods home", MainCurrencyCode = "GBP" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profileId, AccountId = accountId, Role = "view_only" });
            db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profileId, Kind = "cash" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        var page = browser.Page;
        var route = $"/budget-profiles/{profileId}/payment-methods";
        await page.GotoAsync(browser.ClientUrl + route);
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/login?returnUrl=" + Uri.EscapeDataString(route));
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("methods-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + route);
        var methods = page.GetByRole(AriaRole.List, new() { Name = "Profile payment methods" });
        await Expect(methods.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
        await Expect(methods).ToContainTextAsync("Cash");
        await Expect(page.GetByText("Your access: View only", new() { Exact = true })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(methods).ToContainTextAsync("Cash");

        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profile", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + $"/budget-profiles/{profileId}");
        await page.GetByRole(AriaRole.Link, new() { Name = "Review payment methods", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + route);
        await Expect(methods).ToContainTextAsync("Cash");
        await page.SetViewportSizeAsync(390, 844);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));

        await browser.Api.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == profileId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Budget profile unavailable", Exact = true })).ToBeVisibleAsync();
        await Expect(methods).ToHaveCountAsync(0);
        await Expect(page.GetByText("Payment methods home", new() { Exact = true })).ToHaveCountAsync(0);
        Assert.Empty(browser.PageErrors);
    }
}
