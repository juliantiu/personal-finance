using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Tests.Support;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class BffFoundationTests
{
    [Fact]
    public async Task Api_discovery_advertises_authorization_code_with_PKCE_endpoints()
    {
        await using var host = new AccountTestHost();
        using var client = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost:7207/")
        });

        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await client.GetAsync("/.well-known/openid-configuration", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var discovery = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        Assert.NotNull(discovery);
        var root = discovery.RootElement;
        Assert.Contains("code", root.GetProperty("response_types_supported").EnumerateArray()
            .Select(value => value.GetString()));
        Assert.Contains("S256", root.GetProperty("code_challenge_methods_supported").EnumerateArray()
            .Select(value => value.GetString()));
        Assert.Equal("https://localhost:7207/", root.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task LocalDb_migration_creates_OpenIddict_and_protected_BFF_session_tables()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();

        var tables = await host.WithDatabaseAsync(async db => await db.Database
            .SqlQueryRaw<string>("SELECT [name] AS [Value] FROM sys.tables")
            .ToListAsync());

        Assert.Contains("OpenIddictApplications", tables);
        Assert.Contains("OpenIddictAuthorizations", tables);
        Assert.Contains("OpenIddictScopes", tables);
        Assert.Contains("OpenIddictTokens", tables);
        Assert.Contains("BffSessions", tables);
    }

    [Fact]
    public async Task Bff_session_round_trips_tokens_but_persists_only_protected_payload()
    {
        await using var host = new AccountTestHost();
        await host.MigrateAsync();
        var accountId = Guid.NewGuid();
        await host.WithDatabaseAsync(async db =>
        {
            db.Users.Add(new AccountIdentityUser { Id = accountId, UserName = "session@example.test" });
            await db.SaveChangesAsync();
            return true;
        });

        const string accessToken = "test-access-token-should-not-appear-in-database";
        const string refreshToken = "test-refresh-token-should-not-appear-in-database";
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IBffSessionStore>();
        var sessionId = await store.CreateAsync(new BffSession(accountId, accessToken, refreshToken,
            DateTimeOffset.UtcNow.AddMinutes(15), DateTimeOffset.UtcNow.AddHours(1)), CancellationToken.None);

        var stored = await host.WithDatabaseAsync(async db =>
            await db.BffSessions.AsNoTracking().SingleAsync());
        var persistedPayload = Encoding.UTF8.GetString(stored.ProtectedPayload);
        Assert.DoesNotContain(accessToken, persistedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshToken, persistedPayload, StringComparison.Ordinal);

        var restored = await store.FindAsync(sessionId, CancellationToken.None);
        Assert.NotNull(restored);
        Assert.Equal(accountId, restored.AccountId);
        Assert.Equal(accessToken, restored.AccessToken);
        Assert.Equal(refreshToken, restored.RefreshToken);

        await store.RevokeAsync(sessionId, CancellationToken.None);
        Assert.Null(await store.FindAsync(sessionId, CancellationToken.None));
    }
}
