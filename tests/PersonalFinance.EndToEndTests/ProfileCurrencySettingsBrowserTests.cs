using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence.Profiles;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace PersonalFinance.EndToEndTests;

public sealed class ProfileCurrencySettingsBrowserTests
{
    [Fact]
    public async Task Owner_can_disable_and_reenable_the_additional_currency_from_profile_settings()
    {
        await using var browser = await BrowserSession.StartAsync();
        Guid accountId;
        using (var scope = browser.Api.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
            var account = new AccountIdentityUser { UserName = "currency-settings@example.test", Email = "currency-settings@example.test", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(account, "abcdefghijkl")).Succeeded);
            accountId = account.Id;
        }

        var profileId = Guid.NewGuid();
        await browser.Api.WithDatabaseAsync(async db =>
        {
            db.BudgetProfiles.Add(new BudgetProfileRecord
                { Id = profileId, OwnerAccountId = accountId, Name = "Currency profile", MainCurrencyCode = "GBP" });
            db.ProfileCurrencies.Add(new ProfileCurrencyRecord { ProfileId = profileId, CurrencyCode = "USD" });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });

        using (var scope = browser.Api.Services.CreateScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
            var sessionId = await sessions.CreateAsync(
                new BffSession(accountId, "synthetic-access", "synthetic-refresh", DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)),
                TestContext.Current.CancellationToken);
            await browser.Page.Context.AddCookiesAsync([new Cookie
            {
                Name = "__Host-PF.Session", Value = sessionId, Url = browser.ApiUrl, Secure = true,
                HttpOnly = true, SameSite = SameSiteAttribute.Lax
            }]);
        }

        var page = browser.Page;
        var settingsUrl = browser.ClientUrl + $"/budget-profiles/{profileId}/currencies";
        await page.GotoAsync(settingsUrl);
        Assert.Equal(settingsUrl, page.Url);
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Currency settings", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Your access: Owner", new() { Exact = true })).ToBeVisibleAsync();
        var alternative = page.GetByRole(AriaRole.Checkbox, new() { Name = "Enable USD as an additional currency", Exact = true });
        await Expect(alternative).ToBeCheckedAsync();
        await alternative.UncheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Save currency settings", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Currency settings saved.");
        await Expect(page.GetByText("Additional currencies: None", new() { Exact = true })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Link, new() { Name = "Back to profile", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(browser.ClientUrl + $"/budget-profiles/{profileId}");
        await page.GetByRole(AriaRole.Link, new() { Name = "Review currency choices", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(settingsUrl);
        await Expect(page.GetByText("Additional currencies: None", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Checkbox, new() { Name = "Enable USD as an additional currency", Exact = true })).Not.ToBeCheckedAsync();

        await browser.Api.WithDatabaseAsync(async db =>
        {
            var profile = await db.BudgetProfiles.SingleAsync(value => value.Id == profileId, TestContext.Current.CancellationToken);
            Assert.Equal(1, profile.CurrencySettingsVersion);
            Assert.False(await db.ProfileCurrencies.AnyAsync(value => value.ProfileId == profileId, TestContext.Current.CancellationToken));
            return true;
        });
        Assert.Empty(browser.PageErrors);
    }
}
