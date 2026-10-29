using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using PersonalFinance.Application.Accounts;
using PersonalFinance.Infrastructure;
using PersonalFinance.Infrastructure.Email;
using Xunit;

namespace PersonalFinance.IntegrationTests.Accounts;

public sealed class EmailDeliveryTests
{
    [Fact]
    public async Task Confirmation_sender_builds_a_safe_plain_text_and_html_message()
    {
        var transport = new FakeEmailTransport();
        var sender = new ConfirmationEmailSender(
            Options.Create(new ConfirmationEmailOptions
            {
                ClientBaseUrl = "http://localhost:5136/",
                SenderAddress = "no-reply@example.test",
                SenderName = "Personal Finance"
            }),
            transport);
        var confirmation = new EmailConfirmation(
            Guid.Parse("ad63f037-8b50-4ce9-b900-157cc092352e"),
            "person@example.test",
            "token+with&reserved=chars");

        await sender.SendAsync(confirmation, TestContext.Current.CancellationToken);

        var message = Assert.IsType<OutgoingEmailMessage>(transport.Message);
        Assert.Equal("person@example.test", message.Recipient);
        Assert.Equal("no-reply@example.test", message.SenderAddress);
        Assert.Equal("Personal Finance", message.SenderName);
        Assert.Equal("Confirm your email address", message.Subject);
        Assert.Contains("http://localhost:5136/confirm-email#accountId=ad63f037-8b50-4ce9-b900-157cc092352e&token=token%2Bwith%26reserved%3Dchars", message.PlainTextBody);
        Assert.Contains("token%2Bwith%26reserved%3Dchars", message.HtmlBody);
        Assert.DoesNotContain("person@example.test", message.ToString());
        Assert.DoesNotContain("token+with", message.ToString());
    }

    [Fact]
    public async Task Smtp4dev_transport_maps_connection_failures_to_safe_delivery_errors()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Start();
        listener.Stop();
        var cancellationToken = TestContext.Current.CancellationToken;
        var transport = new Smtp4DevEmailTransport(
            Options.Create(new Smtp4DevOptions { Host = "127.0.0.1", Port = port }),
            NullLogger<Smtp4DevEmailTransport>.Instance);
        var message = new OutgoingEmailMessage(
            "person@example.test", "no-reply@example.test", "Personal Finance", "Confirm email", "token body", "token body");

        var exception = await Assert.ThrowsAsync<ConfirmationEmailDeliveryException>(
            () => transport.SendAsync(message, cancellationToken));

        Assert.Equal("Confirmation email could not be delivered.", exception.Message);
        Assert.DoesNotContain("person@example.test", exception.ToString());
        Assert.DoesNotContain("token body", exception.ToString());
    }

    [Fact]
    public void Development_requires_smtp4dev_configuration()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Email:ClientBaseUrl"] = "http://localhost:5136/",
            ["Email:SenderAddress"] = "no-reply@example.test",
            ["Email:Smtp4Dev:Host"] = "127.0.0.1",
            ["Email:Smtp4Dev:Port"] = "2525",
            ["Email:Smtp4Dev:SecureSocketOptions"] = "None"
        });

        Assert.True(EmailSenderConfiguration.IsConfigured(configuration, isDevelopment: true));
        Assert.False(EmailSenderConfiguration.IsConfigured(Configuration([]), isDevelopment: true));
    }

    [Fact]
    public void Production_requires_azure_endpoint_and_does_not_fall_back_to_smtp()
    {
        var productionConfiguration = Configuration(new Dictionary<string, string?>
        {
            ["Email:ClientBaseUrl"] = "https://app.example.test/",
            ["Email:SenderAddress"] = "no-reply@example.test",
            ["Email:Azure:Endpoint"] = "https://example.communication.azure.com"
        });
        var smtpOnlyConfiguration = Configuration(new Dictionary<string, string?>
        {
            ["Email:ClientBaseUrl"] = "https://app.example.test/",
            ["Email:SenderAddress"] = "no-reply@example.test",
            ["Email:Smtp4Dev:Host"] = "127.0.0.1",
            ["Email:Smtp4Dev:Port"] = "2525"
        });

        Assert.True(EmailSenderConfiguration.IsConfigured(productionConfiguration, isDevelopment: false));
        Assert.False(EmailSenderConfiguration.IsConfigured(smtpOnlyConfiguration, isDevelopment: false));
    }

    [Fact]
    public void Infrastructure_selects_smtp4dev_for_development()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration(WithDatabase(DevelopmentSettings())), isDevelopment: true);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<Smtp4DevEmailTransport>(provider.GetRequiredService<IEmailTransport>());
        Assert.IsType<ConfirmationEmailSender>(provider.GetRequiredService<IConfirmationEmailSender>());
    }

    [Fact]
    public void Infrastructure_selects_azure_communication_email_for_production()
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Email:ClientBaseUrl"] = "https://app.example.test/",
            ["Email:SenderAddress"] = "no-reply@example.test",
            ["Email:Azure:Endpoint"] = "https://example.communication.azure.com"
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(Configuration(WithDatabase(settings)), isDevelopment: false);
        using var provider = services.BuildServiceProvider();

        Assert.IsType<AzureCommunicationEmailTransport>(provider.GetRequiredService<IEmailTransport>());
        Assert.IsType<ConfirmationEmailSender>(provider.GetRequiredService<IConfirmationEmailSender>());
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static Dictionary<string, string?> DevelopmentSettings() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Email:ClientBaseUrl"] = "http://localhost:5136/",
        ["Email:SenderAddress"] = "no-reply@example.test",
        ["Email:Smtp4Dev:Host"] = "127.0.0.1",
        ["Email:Smtp4Dev:Port"] = "2525",
        ["Email:Smtp4Dev:SecureSocketOptions"] = "None"
    };

    private static Dictionary<string, string?> WithDatabase(Dictionary<string, string?> settings)
    {
        settings["ConnectionStrings:Identity"] =
            "Server=localhost;Database=EmailProviderTests;User Id=test;Password=not-used;TrustServerCertificate=True";
        return settings;
    }

    private sealed class FakeEmailTransport : IEmailTransport
    {
        public OutgoingEmailMessage? Message { get; private set; }

        public Task SendAsync(OutgoingEmailMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Message = message;
            return Task.CompletedTask;
        }
    }
}
