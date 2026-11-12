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
