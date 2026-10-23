using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Application.Accounts.RegisterAccount;

namespace PersonalFinance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterAccountValidator>(ServiceLifetime.Scoped);
        return services;
    }
}
