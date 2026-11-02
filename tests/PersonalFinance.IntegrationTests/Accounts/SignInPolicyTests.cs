using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Contracts.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class SignInPolicyTests
{
    [Fact]
    public async Task Confirmed_account_signs_in_and_invalid_or_unverified_credentials_get_generic_failure()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        await AddUser(host, "confirmed@example.test", confirmed: true);
        await AddUser(host, "unverified@example.test", confirmed: false);
        using var client = host.CreateClient();

        var valid = await SignIn(client, "confirmed@example.test", "abcdefghijkl");
        var invalid = await SignIn(client, "confirmed@example.test", "wrong-password");
        var unknown = await SignIn(client, "missing@example.test", "wrong-password");
        var unverified = await SignIn(client, "unverified@example.test", "abcdefghijkl");

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(await invalid.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(await invalid.Content.ReadAsStringAsync(), await unverified.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Five_failed_attempts_lock_account_and_throttling_uses_same_failure_response()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        await AddUser(host, "locked@example.test", confirmed: true);
        using var client = host.CreateClient();
        HttpResponseMessage? lastFailure = null;
        for (var attempt = 0; attempt < 5; attempt++)
            lastFailure = await SignIn(client, "locked@example.test", "wrong-password");
        var locked = await SignIn(client, "locked@example.test", "abcdefghijkl");
        for (var attempt = 0; attempt < 4; attempt++)
            await SignIn(client, "another@example.test", "wrong-password");
        var throttled = await SignIn(client, "another@example.test", "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(await lastFailure!.Content.ReadAsStringAsync(), await locked.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, throttled.StatusCode);
        Assert.Equal(await locked.Content.ReadAsStringAsync(), await throttled.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> SignIn(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/accounts/sign-in", new SignInRequest(email, password), TestContext.Current.CancellationToken);

    private static async Task AddUser(AccountTestHost host, string email, bool confirmed)
    {
        using var scope = host.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentityUser>>();
        var user = new AccountIdentityUser { UserName = email, Email = email, EmailConfirmed = confirmed };
        Assert.True((await manager.CreateAsync(user, "abcdefghijkl")).Succeeded);
    }
}
