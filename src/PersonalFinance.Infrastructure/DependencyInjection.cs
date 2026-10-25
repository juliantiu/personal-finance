using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Email;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool developmentEmailSinkEnabled)
    {
        var connectionString = configuration.GetConnectionString("Identity");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The Identity database connection string is required.");

        services.AddDbContext<AccountIdentityDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentityCore<AccountIdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddEntityFrameworkStores<AccountIdentityDbContext>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));
        services.AddScoped<IAccountIdentity, IdentityAccountIdentity>();

        if (developmentEmailSinkEnabled)
        {
            services.Configure<DevelopmentEmailOptions>(configuration.GetSection(DevelopmentEmailOptions.SectionName));
            services.AddSingleton<DevelopmentEmailInbox>();
            services.AddSingleton<IDevelopmentEmailInbox>(provider => provider.GetRequiredService<DevelopmentEmailInbox>());
            services.AddScoped<IConfirmationEmailSender, DevelopmentConfirmationEmailSender>();
        }

        return services;
    }
}
