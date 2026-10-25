using Mediator;
using PersonalFinance.Api.Accounts;
using PersonalFinance.Application;
using PersonalFinance.Application.Accounts.RegisterAccount;
using PersonalFinance.Application.Common;
using PersonalFinance.Infrastructure;
using PersonalFinance.Infrastructure.Email;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddApplication();

var hasIdentityDatabase = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Identity"));
var hasDevelopmentEmailSink = builder.Environment.IsDevelopment();
var accountEndpointsEnabled = AccountEndpointAvailability.IsEnabled(hasIdentityDatabase, hasDevelopmentEmailSink);
if (accountEndpointsEnabled)
{
    builder.Services.AddInfrastructure(builder.Configuration, hasDevelopmentEmailSink);
    builder.Services.AddMediator(options =>
    {
        options.Assemblies = [typeof(RegisterAccountCommand).Assembly];
        options.ServiceLifetime = ServiceLifetime.Scoped;
        options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
    });
}

var app = builder.Build();

app.UseExceptionHandler();

// This phase exposes the account workflow only where both persistence configuration and the
// local email sink are available. Production email delivery is wired in a later slice.
if (accountEndpointsEnabled)
{
    app.MapControllers();
    app.MapGet("/dev-email", (IDevelopmentEmailInbox inbox) =>
    {
        var rows = string.Join("", inbox.ReadAll().Select(message =>
            $"<li>{HtmlEncoder.Default.Encode(message.Recipient)} — " +
            $"<a href=\"{HtmlEncoder.Default.Encode(message.ConfirmationLink.ToString())}\">Open confirmation link</a></li>"));
        var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>Development email inbox</title></head>" +
                   $"<body><h1>Development email inbox</h1><ul>{rows}</ul></body></html>";
        return Results.Content(html, "text/html; charset=utf-8");
    });
}

app.Run();

public partial class Program;
