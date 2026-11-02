using Mediator;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Application;
using PersonalFinance.Application.Accounts.RegisterAccount;
using PersonalFinance.Application.Common;
using PersonalFinance.Infrastructure;
using PersonalFinance.Infrastructure.Email;

var builder = WebApplication.CreateBuilder(args);
var clientOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
    policy.WithOrigins(clientOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddApplication();

var hasIdentityDatabase = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Identity"));
var isDevelopment = builder.Environment.IsDevelopment();
var hasConfiguredEmailSender = EmailSenderConfiguration.IsConfigured(builder.Configuration, isDevelopment);
var accountEndpointsEnabled = AccountEndpointAvailability.IsEnabled(hasIdentityDatabase, hasConfiguredEmailSender);
var openIddictEnabled = builder.Configuration.GetValue<bool>("Authentication:OpenIddict:Enabled");
if (accountEndpointsEnabled)
{
    builder.Services.AddInfrastructure(builder.Configuration, isDevelopment);
    if (openIddictEnabled)
    {
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetEndSessionEndpointUris("/connect/logout");
                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange();
                options.RegisterScopes("email", "profile", "offline_access", "personal_finance_api");
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.UseDataProtection(protection => protection.PreferDefaultAccessTokenFormat());
                var issuer = builder.Configuration["Authentication:OpenIddict:Issuer"];
                if (Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
                    options.SetIssuer(issuerUri);

                if (isDevelopment)
                {
                    options.AddEphemeralEncryptionKey()
                        .AddEphemeralSigningKey();
                }
                else
                {
                    throw new InvalidOperationException(
                        "Production OpenIddict signing and encryption credentials must be configured before enabling the server.");
                }

                options.UseAspNetCore();
            });
    }
    builder.Services.AddMediator(options =>
    {
        options.Assemblies = [typeof(RegisterAccountCommand).Assembly];
        options.ServiceLifetime = ServiceLifetime.Scoped;
        options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
    });
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("Client");
if (accountEndpointsEnabled && openIddictEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (accountEndpointsEnabled)
{
    app.MapControllers();
}

app.Run();

public partial class Program;
