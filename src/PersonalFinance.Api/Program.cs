using Mediator;
using Microsoft.AspNetCore.Mvc;
using System.Threading.RateLimiting;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Application;
using PersonalFinance.Application.Accounts.RegisterAccount;
using PersonalFinance.Application.Common;
using PersonalFinance.Infrastructure;
using PersonalFinance.Infrastructure.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
var clientOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
    policy.WithOrigins(clientOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Configuration.GetValue<bool?>("Authentication:Antiforgery:SecureCookie") == false && isDevelopment
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = Enum.TryParse<SameSiteMode>(builder.Configuration["Authentication:Antiforgery:SameSite"], true, out var sameSite)
        ? sameSite
        : SameSiteMode.None;
});
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddRateLimiter(options => options.AddPolicy("SignIn", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        })));
builder.Services.Configure<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>(options =>
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Title = "Sign-in failed. Check your details and try again.", Status = StatusCodes.Status401Unauthorized },
            cancellationToken);
    });
builder.Services.AddApplication();

var hasIdentityDatabase = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Identity"));
var hasConfiguredEmailSender = EmailSenderConfiguration.IsConfigured(builder.Configuration, isDevelopment);
var accountEndpointsEnabled = AccountEndpointAvailability.IsEnabled(hasIdentityDatabase, hasConfiguredEmailSender);
var openIddictEnabled = builder.Configuration.GetValue<bool>("Authentication:OpenIddict:Enabled");
if (accountEndpointsEnabled)
{
    builder.Services.AddInfrastructure(builder.Configuration, isDevelopment);
    if (openIddictEnabled)
    {
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "__Host-PF.Login";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
                options.SlidingExpiration = false;
            });
        builder.Services.AddAuthorization();
        builder.Services.AddOpenIddict()
            .AddClient(options =>
            {
                var issuer = builder.Configuration["Authentication:OpenIddict:Issuer"]
                    ?? throw new InvalidOperationException("The OpenIddict issuer is required.");
                var clientId = builder.Configuration["Authentication:OpenIddict:ClientId"] ?? "personal-finance-client";
                options.AllowAuthorizationCodeFlow();
                options.SetRedirectionEndpointUris("/api/auth/callback");
                options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                options.UseAspNetCore().EnableRedirectionEndpointPassthrough();
                if (isDevelopment)
                    options.UseAspNetCore().DisableTransportSecurityRequirement();
                options.UseSystemNetHttp();
                options.AddRegistration(new OpenIddictClientRegistration
                {
                    Issuer = new Uri(issuer, UriKind.Absolute),
                    ProviderName = "PersonalFinance",
                    ClientId = clientId,
                    RedirectUri = new Uri(new Uri(issuer, UriKind.Absolute), "api/auth/callback"),
                    Scopes = { Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, "personal_finance_api" }
                });
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetEndSessionEndpointUris("/connect/logout");
                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow()
                    .RequireProofKeyForCodeExchange();
                options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, "personal_finance_api");
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));
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

                options.UseAspNetCore().EnableAuthorizationEndpointPassthrough();
                if (isDevelopment)
                    options.UseAspNetCore().DisableTransportSecurityRequirement();
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
app.UseRateLimiter();
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
