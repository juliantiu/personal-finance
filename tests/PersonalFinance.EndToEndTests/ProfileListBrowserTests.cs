using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class ProfileListBrowserTests
{
    [Fact]
    public async Task Profiles_can_be_opened_and_switched_through_deep_links_list_and_browser_history()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        Guid otherId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "switching-browser@example.test", Email = "switching-browser@example.test", EmailConfirmed = true };
            var other = new AccountIdentityUser { UserName = "switching-owner@example.test", Email = "switching-owner@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(account, "abcdefghijkl")).Succeeded);
            Assert.True((await manager.CreateAsync(other, "abcdefghijkl")).Succeeded);
            accountId = account.Id;
            otherId = other.Id;
        }

        var ownedId = Guid.NewGuid();
        var sharedId = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.AddRange(
                new BudgetProfileRecord { Id = ownedId, OwnerAccountId = accountId, Name = "Owned dashboard", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = sharedId, OwnerAccountId = otherId, Name = "Shared dashboard", MainCurrencyCode = "USD" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = sharedId, AccountId = accountId, Role = "view_only" });
            AddInitialSetup(db, ownedId);
            AddInitialSetup(db, sharedId);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        var page = browser.Page;
        await page.GotoAsync(browser.ClientUrl + "/budget-profiles");
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/login?returnUrl=%2Fbudget-profiles");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("switching-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");

        var ownedUrl = browser.ClientUrl + $"/budget-profiles/{ownedId}";
        var sharedUrl = browser.ClientUrl + $"/budget-profiles/{sharedId}";
        await page.GotoAsync(sharedUrl);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Shared dashboard", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Your access: View only", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Main currency: USD", new() { Exact = true })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Shared dashboard", Exact = true })).ToBeVisibleAsync();
        await browser.Api.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == sharedId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Budget profile unavailable", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Shared dashboard", new() { Exact = true })).ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profiles", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");
        var list = page.GetByRole(AriaRole.List, new() { Name = "Accessible budget profiles" });
        await Expect(list.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(1);
        await list.GetByRole(AriaRole.Link, new() { Name = "Owned dashboard", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(ownedUrl);
        await Expect(page.GetByText("Your access: Owner", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Main currency: GBP", new() { Exact = true })).ToBeVisibleAsync();

        await page.GoBackAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");
        await page.GoForwardAsync();
        await Expect(page).ToHaveURLAsync(ownedUrl);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Owned dashboard", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Your access: Owner", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Empty(browser.PageErrors);
    }

    [Fact]
    public async Task Protected_list_supports_real_sign_in_current_grants_and_recoverable_load_failure()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        Guid otherId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "profiles-browser@example.test", Email = "profiles-browser@example.test", EmailConfirmed = true };
            var other = new AccountIdentityUser { UserName = "other-profiles@example.test", Email = "other-profiles@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(account, "abcdefghijkl")).Succeeded);
            Assert.True((await manager.CreateAsync(other, "abcdefghijkl")).Succeeded);
            accountId = account.Id;
            otherId = other.Id;
        }

        var page = browser.Page;
        await page.GotoAsync(browser.ClientUrl + "/budget-profiles");
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/login?returnUrl=%2Fbudget-profiles");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("profiles-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Budget profiles", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("You don't have any budget profiles yet.", new() { Exact = true })).ToBeVisibleAsync();

        var sharedId = Guid.NewGuid();
        var viewedId = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.AddRange(
                new BudgetProfileRecord { Id = Guid.NewGuid(), OwnerAccountId = accountId, Name = "Home budget", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = sharedId, OwnerAccountId = otherId, Name = "Shared holiday", MainCurrencyCode = "USD" },
                new BudgetProfileRecord { Id = viewedId, OwnerAccountId = otherId, Name = "Family savings", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = Guid.NewGuid(), OwnerAccountId = otherId, Name = "Other account private", MainCurrencyCode = "USD" });
            db.ProfileAccess.AddRange(
                new ProfileAccessRecord { ProfileId = sharedId, AccountId = accountId, Role = "editor" },
                new ProfileAccessRecord { ProfileId = viewedId, AccountId = accountId, Role = "view_only" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        await page.SetViewportSizeAsync(390, 844);
        await page.ReloadAsync();
        var list = page.GetByRole(AriaRole.List, new() { Name = "Accessible budget profiles" });
        await Expect(list.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(3);
        await Expect(list).ToContainTextAsync("Home budget");
        await Expect(list).ToContainTextAsync("USD");
        await Expect(list).ToContainTextAsync("Your access: Owner");
        await Expect(list).ToContainTextAsync("Your access: Editor");
        await Expect(list).ToContainTextAsync("Your access: View only");
        await Expect(page.GetByText("Other account private", new() { Exact = true })).ToHaveCountAsync(0);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));

        await browser.Api.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == sharedId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        await page.ReloadAsync();
        await Expect(list.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
        await Expect(page.GetByText("Shared holiday", new() { Exact = true })).ToHaveCountAsync(0);

        await page.RouteAsync("**/api/budget-profiles", route => route.AbortAsync());
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("We couldn't load your budget profiles.");
        await Expect(page.GetByText("You don't have any budget profiles yet.", new() { Exact = true })).ToHaveCountAsync(0);
        await page.UnrouteAsync("**/api/budget-profiles");
        await page.GetByRole(AriaRole.Button, new() { Name = "Try again", Exact = true }).ClickAsync();
        await Expect(list.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
        Assert.Empty(browser.PageErrors);
    }

    private static void AddInitialSetup(AccountIdentityDbContext db, Guid profileId)
    {
        db.Categories.AddRange(
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Income", Kind = "income" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Savings", Kind = "savings" },
            new CategoryRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "Debt", Kind = "debt" });
        db.PaymentMethods.Add(new PaymentMethodRecord { Id = Guid.NewGuid(), ProfileId = profileId, Kind = "cash" });
        db.Tags.AddRange(
            new TagRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "IOU", SystemRole = "iou" },
            new TagRecord { Id = Guid.NewGuid(), ProfileId = profileId, Name = "IOU REPAYMENT", SystemRole = "iou_repayment" });
    }
}
