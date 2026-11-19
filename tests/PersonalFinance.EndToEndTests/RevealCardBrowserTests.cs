using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class RevealCardBrowserTests
{
    [Fact]
    public async Task Editor_explicitly_reveals_hides_and_closes_without_retaining_numbers_after_context_or_access_loss()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId, ownerId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var editor = new AccountIdentityUser { UserName = "reveal-editor@example.test", Email = "reveal-editor@example.test", EmailConfirmed = true };
            var owner = new AccountIdentityUser { UserName = "reveal-owner@example.test", Email = "reveal-owner@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(editor, "abcdefghijkl")).Succeeded);
            Assert.True((await manager.CreateAsync(owner, "abcdefghijkl")).Succeeded);
            accountId = editor.Id; ownerId = owner.Id;
        }
        var profile = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profile, OwnerAccountId = ownerId, Name = "Synthetic reveal home", MainCurrencyCode = "GBP" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = profile, AccountId = accountId, Role = "editor" });
            db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profile, Kind = "cash" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var page = browser.Page;
        var route = $"/budget-profiles/{profile}/payment-methods";
        await page.GotoAsync(browser.ClientUrl + route);
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("reveal-editor@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + route, new() { Timeout = 15000 });
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Add card", Exact = true })).ToBeVisibleAsync();
        await page.GetByLabel("Card number", new() { Exact = true }).FillAsync("4111111111111111");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save card", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Card saved.", new() { Exact = true })).ToBeVisibleAsync();
        var requests = 0;
        page.Request += (_, request) => { if (new Uri(request.Url).AbsolutePath.EndsWith("/reveal")) requests++; };
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.List, new() { Name = "Profile payment methods" })).ToContainTextAsync("•••• 1111");
        Assert.Equal(0, requests);
        Assert.False(await ContainsPlaintext(page));
        var reveal = page.GetByRole(AriaRole.Button, new() { Name = "Reveal card", Exact = true });
        await Expect(reveal).ToHaveCountAsync(1);
        await reveal.ClickAsync();
        var number = page.GetByLabel("Revealed card number", new() { Exact = true });
        await Expect(number).ToBeVisibleAsync();
        Assert.True(await number.TextContentAsync() == "4111111111111111");
        Assert.Equal(1, requests);
        await page.GetByRole(AriaRole.Button, new() { Name = "Hide number", Exact = true }).ClickAsync();
        await Expect(number).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Button, new() { Name = "Reveal number", Exact = true }).ClickAsync();
        await Expect(number).ToBeVisibleAsync();
        Assert.Equal(2, requests);
        await page.Keyboard.PressAsync("Escape");
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        Assert.False(await ContainsPlaintext(page));
        await reveal.ClickAsync();
        await Expect(number).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await reveal.ClickAsync();
        await Expect(number).ToBeVisibleAsync();
        await page.SetViewportSizeAsync(390, 844);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await page.GotoAsync(browser.ClientUrl + $"/budget-profiles/{profile}");
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Review payment methods", Exact = true })).ToBeVisibleAsync();
        Assert.False(await ContainsPlaintext(page));
        await page.GetByRole(AriaRole.Link, new() { Name = "Review payment methods", Exact = true }).ClickAsync();
        await reveal.ClickAsync();
        await Expect(number).ToBeVisibleAsync();
        await browser.Api.WithDatabaseAsync(db => db.ProfileAccess.Where(x => x.ProfileId == profile).ExecuteUpdateAsync(x => x.SetProperty(a => a.Role, "view_only"), TestContext.Current.CancellationToken));
        await page.GetByRole(AriaRole.Button, new() { Name = "Hide number", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Reveal number", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Your access: View only", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Expect(reveal).ToHaveCountAsync(0);
        Assert.False(await ContainsPlaintext(page));
        Assert.True(await page.EvaluateAsync<bool>("!JSON.stringify([localStorage, sessionStorage]).includes('4111111111111111')"));
        Assert.Empty(browser.PageErrors);
    }

    private static async Task<bool> ContainsPlaintext(IPage page) => (await page.ContentAsync()).Contains("4111111111111111", StringComparison.Ordinal);
}
