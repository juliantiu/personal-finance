using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class ProfileCategoriesBrowserTests
{
    [Fact]
    public async Task Categories_support_sign_in_deep_links_profile_navigation_reload_retry_and_revoked_access()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        Guid otherId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "categories-browser@example.test", Email = "categories-browser@example.test", EmailConfirmed = true };
            var other = new AccountIdentityUser { UserName = "category-owner@example.test", Email = "category-owner@example.test", EmailConfirmed = true };
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
                new BudgetProfileRecord { Id = ownedId, OwnerAccountId = accountId, Name = "Category review home", MainCurrencyCode = "GBP" },
                new BudgetProfileRecord { Id = sharedId, OwnerAccountId = otherId, Name = "Shared categories", MainCurrencyCode = "GBP" });
            db.ProfileAccess.Add(new ProfileAccessRecord { ProfileId = sharedId, AccountId = accountId, Role = "view_only" });
            foreach (var id in new[] { ownedId, sharedId })
                db.Categories.AddRange(
                    new CategoryRecord { Id = Guid.NewGuid(), ProfileId = id, Name = id == ownedId ? "Wages" : "Shared earnings", Kind = "income" },
                    new CategoryRecord { Id = Guid.NewGuid(), ProfileId = id, Name = "Rainy day", Kind = "savings" },
                    new CategoryRecord { Id = Guid.NewGuid(), ProfileId = id, Name = "Loans", Kind = "debt" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var page = browser.Page;
        var ownedUrl = browser.ClientUrl + $"/budget-profiles/{ownedId}/categories";
        var sharedUrl = browser.ClientUrl + $"/budget-profiles/{sharedId}/categories";
        await page.GotoAsync(ownedUrl);
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + $"/login?returnUrl=%2Fbudget-profiles%2F{ownedId}%2Fcategories");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("categories-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(ownedUrl);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Categories", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Your access: Owner", new() { Exact = true })).ToBeVisibleAsync();
        var categories = page.GetByRole(AriaRole.List, new() { Name = "Profile categories", Exact = true });
        await Expect(categories.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(3);
        await Expect(categories).ToContainTextAsync("Wages");
        await Expect(categories).ToContainTextAsync("System category: Income");
        await Expect(categories).ToContainTextAsync("System category: Savings");
        await Expect(categories).ToContainTextAsync("System category: Debt");

        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profile", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Category review home", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Review categories", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(ownedUrl);
        await page.ReloadAsync();
        await Expect(categories).ToContainTextAsync("Wages");

        await page.RouteAsync("**/api/budget-profiles/*/categories", route => route.AbortAsync());
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("We couldn't load categories.");
        await Expect(categories).ToHaveCountAsync(0);
        await page.UnrouteAsync("**/api/budget-profiles/*/categories");
        await page.GetByRole(AriaRole.Button, new() { Name = "Try again", Exact = true }).ClickAsync();
        await Expect(categories).ToContainTextAsync("Wages");

        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync(sharedUrl);
        await Expect(page.GetByText("Your access: View only", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(categories).ToContainTextAsync("Shared earnings");
        await Expect(page.GetByText("Wages", new() { Exact = true })).ToHaveCountAsync(0);
        await page.ReloadAsync();
        await Expect(categories).ToContainTextAsync("Shared earnings");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await browser.Api.WithDatabaseAsync(db => db.ProfileAccess.Where(access => access.ProfileId == sharedId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Categories unavailable", Exact = true })).ToBeVisibleAsync();
        await Expect(categories).ToHaveCountAsync(0);
        await Expect(page.GetByText("Shared earnings", new() { Exact = true })).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profiles", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");
        Assert.Empty(browser.PageErrors);
    }
}
