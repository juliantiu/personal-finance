using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Infrastructure;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Email;
using PersonalFinance.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class AccountInfrastructureTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Account_routes_require_database_and_configured_email_sender(
        bool databaseConfigured, bool emailSenderConfigured, bool expected)
    {
        Assert.Equal(expected, AccountEndpointAvailability.IsEnabled(databaseConfigured, emailSenderConfigured));
    }

    [Fact]
    public void Identity_requires_unique_confirmed_email_and_a_twelve_character_password()
    {
        using var provider = CreateInfrastructureProvider();
        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(identity.User.RequireUniqueEmail);
        Assert.True(identity.SignIn.RequireConfirmedEmail);
        Assert.Equal(12, identity.Password.RequiredLength);
        Assert.False(identity.Password.RequireDigit);
        Assert.False(identity.Password.RequireLowercase);
        Assert.False(identity.Password.RequireUppercase);
        Assert.False(identity.Password.RequireNonAlphanumeric);
    }

    [Fact]
    public void Confirmation_tokens_expire_after_twenty_four_hours()
    {
        using var provider = CreateInfrastructureProvider();

        var options = provider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;

        Assert.Equal(TimeSpan.FromHours(24), options.TokenLifespan);
    }

    [Fact]
    public void Normalized_email_index_is_unique_without_connecting_to_sql_server()
    {
        using var provider = CreateInfrastructureProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AccountIdentityDbContext>();
        var index = context.Model.FindEntityType(typeof(AccountIdentityUser))!
            .GetIndexes()
            .Single(candidate => candidate.Properties.Any(property => property.Name == nameof(AccountIdentityUser.NormalizedEmail)));

        Assert.True(index.IsUnique);
        Assert.Equal("EmailIndex", index.GetDatabaseName());
    }

    private static ServiceProvider CreateInfrastructureProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Identity"] = "Server=localhost;Database=PersonalFinanceTests;User Id=test;Password=not-used;TrustServerCertificate=True"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, isDevelopment: true);
        return services.BuildServiceProvider(validateScopes: true);
    }
}
