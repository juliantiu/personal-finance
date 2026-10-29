using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts;
using Azure.Communication.Email;
using Azure.Identity;
using PersonalFinance.Infrastructure.Accounts;
using PersonalFinance.Infrastructure.Email;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var connectionString = configuration.GetConnectionString("Identity");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The Identity database connection string is required.");

        services.AddDataProtection();
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

        services.Configure<ConfirmationEmailOptions>(configuration.GetSection(ConfirmationEmailOptions.SectionName));
        if (isDevelopment)
        {
            services.Configure<Smtp4DevOptions>(configuration.GetSection(Smtp4DevOptions.SectionName));
            services.AddScoped<IEmailTransport, Smtp4DevEmailTransport>();
        }
        else
        {
            services.Configure<AzureCommunicationEmailOptions>(configuration.GetSection(AzureCommunicationEmailOptions.SectionName));
            services.AddSingleton(provider =>
            {
                var endpoint = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureCommunicationEmailOptions>>().Value.Endpoint;
                return new EmailClient(new Uri(endpoint, UriKind.Absolute), new DefaultAzureCredential());
            });
            services.AddScoped<IEmailTransport, AzureCommunicationEmailTransport>();
        }

        services.AddScoped<IConfirmationEmailSender, ConfirmationEmailSender>();

        return services;
    }
}
