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
if (accountEndpointsEnabled)
{
    builder.Services.AddInfrastructure(builder.Configuration, isDevelopment);
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

if (accountEndpointsEnabled)
{
    app.MapControllers();
}

app.Run();

public partial class Program;
