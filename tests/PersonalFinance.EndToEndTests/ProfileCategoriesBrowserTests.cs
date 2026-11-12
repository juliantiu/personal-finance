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
    public async Task Writer_can_stop_tracking_a_subcategory_and_review_it_after_reload()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "subcategory-delete@example.test", Email = "subcategory-delete@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(account, "abcdefghijkl")).Succeeded);
            accountId = account.Id;
        }
        var profileId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var subcategoryId = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord { Id = profileId, OwnerAccountId = accountId, Name = "Tracking status", MainCurrencyCode = "GBP" });
            db.Categories.Add(new CategoryRecord { Id = categoryId, ProfileId = profileId, Name = "Income", Kind = "income" });
            db.Subcategories.Add(new SubcategoryRecord { Id = subcategoryId, CategoryId = categoryId, Name = "Salary", Comment = "Monthly pay" });
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var page = browser.Page;
        var categoriesUrl = browser.ClientUrl + $"/budget-profiles/{profileId}/categories";
        await page.GotoAsync(browser.ClientUrl + $"/login?returnUrl=%2Fbudget-profiles%2F{profileId}%2Fcategories");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("subcategory-delete@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(categoriesUrl);

        var salary = page.GetByRole(AriaRole.List, new() { Name = "Subcategories for Income", Exact = true }).GetByRole(AriaRole.Listitem).First;
        var stopResponse = page.WaitForResponseAsync(response => response.Url.Contains($"/subcategories/{subcategoryId}", StringComparison.Ordinal)
            && response.Request.Method == "DELETE");
        await salary.GetByRole(AriaRole.Button, new() { Name = "Stop tracking", Exact = true }).ClickAsync();
        Assert.Equal(200, (int)(await stopResponse).Status);
        await Expect(salary).ToContainTextAsync("Tracking stopped on");
        await Expect(salary).ToContainTextAsync("Monthly pay");
        await page.ReloadAsync();
        salary = page.GetByRole(AriaRole.List, new() { Name = "Subcategories for Income", Exact = true }).GetByRole(AriaRole.Listitem).First;
        await Expect(salary).ToContainTextAsync("Salary");
        await Expect(salary).ToContainTextAsync("Tracking stopped on");
        await Expect(salary.GetByRole(AriaRole.Button, new() { Name = "Stop tracking", Exact = true })).ToHaveCountAsync(0);
        Assert.Empty(browser.PageErrors);
    }

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
        await page.GotoAsync(browser.ClientUrl + $"/login?returnUrl=%2Fbudget-profiles%2F{ownedId}%2Fcategories");
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + $"/login?returnUrl=%2Fbudget-profiles%2F{ownedId}%2Fcategories");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("categories-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(ownedUrl);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Categories", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Your access: Owner", new() { Exact = true })).ToBeVisibleAsync();
        var categories = page.GetByRole(AriaRole.List, new() { Name = "Profile categories", Exact = true });
        await Expect(categories.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(3);
        var incomeItem = categories.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "System category: Income" });
        var incomeName = incomeItem.GetByLabel("Category name", new() { Exact = true });
        await Expect(incomeName).ToHaveValueAsync("Wages");
        await Expect(categories).ToContainTextAsync("System category: Income");
        await Expect(categories).ToContainTextAsync("System category: Savings");
        await Expect(categories).ToContainTextAsync("System category: Debt");

        await incomeName.FillAsync("Wages revised");
        await incomeItem.GetByRole(AriaRole.Button, new() { Name = "Save category", Exact = true }).ClickAsync();
        await Expect(incomeName).ToHaveValueAsync("Wages revised");
        await page.GetByLabel("Category name", new() { Exact = true }).First.FillAsync("Wages revised");
        await page.GetByLabel("Comment (optional)", new() { Exact = true }).First.FillAsync("duplicate labels are allowed");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add category", Exact = true }).ClickAsync();
        try { await Expect(categories.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(4); }
        catch (PlaywrightException exception)
        {
            throw new InvalidOperationException($"Category create did not refresh. API: {string.Join(" | ", browser.ApiResponses)}. Alerts: {string.Join(" | ", await page.GetByRole(AriaRole.Alert).AllTextContentsAsync())}. Console: {string.Join(" | ", browser.ConsoleErrors)}", exception);
        }
        var ordinaryItem = categories.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Category type: Ordinary" });
        await Expect(incomeName).ToHaveValueAsync("Wages revised");
        await Expect(ordinaryItem.GetByLabel("Category name", new() { Exact = true })).ToHaveValueAsync("Wages revised");

        var savingsItem = categories.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "System category: Savings" });
        await savingsItem.GetByLabel("New subcategory name", new() { Exact = true }).FillAsync("Emergency fund");
        await savingsItem.GetByLabel("New subcategory comment (optional)", new() { Exact = true }).FillAsync("Unexpected costs");
        var createSubcategoryResponse = page.WaitForResponseAsync(response =>
            response.Url.Contains("/subcategories", StringComparison.Ordinal) && response.Request.Method == "POST");
        await savingsItem.GetByRole(AriaRole.Button, new() { Name = "Add subcategory", Exact = true }).ClickAsync();
        Assert.Equal(201, (int)(await createSubcategoryResponse).Status);
        var savingsChildren = page.GetByRole(AriaRole.List, new() { Name = "Subcategories for Rainy day", Exact = true });
        var emergencyFund = savingsChildren.GetByRole(AriaRole.Listitem).First;
        await Expect(emergencyFund.GetByLabel("Subcategory name", new() { Exact = true })).ToHaveValueAsync("Emergency fund");
        await Expect(emergencyFund).ToContainTextAsync("Unexpected costs");
        await emergencyFund.GetByLabel("Subcategory name", new() { Exact = true }).FillAsync("Travel fund");
        await emergencyFund.GetByRole(AriaRole.Button, new() { Name = "Save subcategory", Exact = true }).ClickAsync();
        await Expect(emergencyFund.GetByLabel("Subcategory name", new() { Exact = true })).ToHaveValueAsync("Travel fund");

        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profile", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Category review home", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Review categories", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(ownedUrl);
        await page.ReloadAsync();
        await Expect(categories.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(5);
        await Expect(incomeItem.GetByLabel("Category name", new() { Exact = true })).ToHaveValueAsync("Wages revised");
        await Expect(savingsChildren.GetByRole(AriaRole.Listitem).First.GetByLabel("Subcategory name", new() { Exact = true }))
            .ToHaveValueAsync("Travel fund");

        emergencyFund = savingsChildren.GetByRole(AriaRole.Listitem).First;
        var stopTrackingResponse = page.WaitForResponseAsync(response =>
            response.Url.Contains("/subcategories/", StringComparison.Ordinal) && response.Request.Method == "DELETE");
        await emergencyFund.GetByRole(AriaRole.Button, new() { Name = "Stop tracking", Exact = true }).ClickAsync();
        Assert.Equal(200, (int)(await stopTrackingResponse).Status);
        await Expect(emergencyFund).ToContainTextAsync("Tracking stopped on");
        await Expect(emergencyFund).ToContainTextAsync("Unexpected costs");
        await Expect(emergencyFund.GetByLabel("Subcategory name", new() { Exact = true })).ToHaveCountAsync(0);
        await page.ReloadAsync();
        emergencyFund = savingsChildren.GetByRole(AriaRole.Listitem).First;
        await Expect(emergencyFund).ToContainTextAsync("Tracking stopped on");
        await Expect(emergencyFund).ToContainTextAsync("Travel fund");

        await page.RouteAsync("**/api/budget-profiles/*/categories", route => route.AbortAsync());
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync("We couldn't load categories.");
        await Expect(categories).ToHaveCountAsync(0);
        await page.UnrouteAsync("**/api/budget-profiles/*/categories");
        await page.GetByRole(AriaRole.Button, new() { Name = "Try again", Exact = true }).ClickAsync();
        await Expect(categories.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(5);

        await page.SetViewportSizeAsync(390, 844);
        await page.GotoAsync(sharedUrl);
        await Expect(page.GetByText("Your access: View only", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(categories).ToContainTextAsync("Shared earnings");
        await Expect(page.GetByText("Wages revised", new() { Exact = true })).ToHaveCountAsync(0);
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
