using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PersonalFinance.Infrastructure.Accounts;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class CreateProfileBrowserTests
{
    [Fact]
    public async Task Signed_in_account_can_create_complete_profiles_with_explicit_currency_settings()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "create-browser@example.test", Email = "create-browser@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(account, "abcdefghijkl")).Succeeded);
            accountId = account.Id;
        }
        var page = browser.Page;
        await page.GotoAsync(browser.ClientUrl + "/budget-profiles/create");
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/login?returnUrl=%2Fbudget-profiles%2Fcreate");
        await page.GetByLabel("Email address", new() { Exact = true }).FillAsync("create-browser@example.test");
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync("abcdefghijkl");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles/create");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Create budget profile", Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Create profile", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Enter a profile name.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Select GBP or USD as the main currency.", new() { Exact = true })).ToBeVisibleAsync();
        await page.SetViewportSizeAsync(390, 844);
        await page.GetByLabel("Profile name", new() { Exact = true }).FillAsync("  Home budget  ");
        await page.GetByLabel("Main currency", new() { Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "GBP — Pound sterling (£)", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Enable USD as an additional currency", Exact = true }).CheckAsync();
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth"));
        await page.GetByRole(AriaRole.Button, new() { Name = "Create profile", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");
        var list = page.GetByRole(AriaRole.List, new() { Name = "Accessible budget profiles" });
        await Expect(list).ToContainTextAsync("Home budget");
        await Expect(list).ToContainTextAsync("GBP");
        await Expect(list).ToContainTextAsync("Owner");
        await page.GetByRole(AriaRole.Link, new() { Name = "Create another profile", Exact = true }).ClickAsync();
        await page.GetByLabel("Profile name", new() { Exact = true }).FillAsync("Travel budget");
        await page.GetByLabel("Main currency", new() { Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = "USD — US dollar ($)", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Checkbox, new() { Name = "Enable GBP as an additional currency", Exact = true })).Not.ToBeCheckedAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Create profile", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + "/budget-profiles");
        await Expect(list.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(2);
        await browser.Api.WithDatabaseAsync(async db =>
        {
            var profiles = await db.BudgetProfiles.ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, profiles.Length);
            Assert.All(profiles, profile => Assert.Equal(accountId, profile.OwnerAccountId));
            var home = Assert.Single(profiles, profile => profile.Name == "Home budget");
            var travel = Assert.Single(profiles, profile => profile.Name == "Travel budget");
            Assert.Equal("GBP", home.MainCurrencyCode);
            Assert.Equal("USD", travel.MainCurrencyCode);
            var additional = await db.ProfileCurrencies.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(home.Id, additional.ProfileId);
            Assert.Equal("USD", additional.CurrencyCode);
            foreach (var profile in profiles)
            {
                Assert.Equal(3, await db.Categories.CountAsync(value => value.ProfileId == profile.Id, TestContext.Current.CancellationToken));
                Assert.Equal("cash", (await db.PaymentMethods.SingleAsync(value => value.ProfileId == profile.Id, TestContext.Current.CancellationToken)).Kind);
                Assert.Equal(2, await db.Tags.CountAsync(value => value.ProfileId == profile.Id, TestContext.Current.CancellationToken));
            }
            Assert.False(await db.ProfileAccess.AnyAsync(TestContext.Current.CancellationToken));
            return true;
        });
        Assert.Empty(browser.PageErrors);
    }
}
