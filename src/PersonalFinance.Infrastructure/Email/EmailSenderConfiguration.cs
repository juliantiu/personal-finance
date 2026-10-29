using System.Net.Mail;
using MailKit.Security;
using Microsoft.Extensions.Configuration;

namespace PersonalFinance.Infrastructure.Email;

public static class EmailSenderConfiguration
{
    public static bool IsConfigured(IConfiguration configuration, bool isDevelopment)
    {
        if (!IsValidClientBaseUrl(configuration["Email:ClientBaseUrl"]) ||
            !IsValidAddress(configuration["Email:SenderAddress"]))
        {
            return false;
        }

        return isDevelopment
            ? IsSmtp4DevConfigured(configuration)
            : IsAzureConfigured(configuration);
    }

    private static bool IsSmtp4DevConfigured(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration["Email:Smtp4Dev:Host"]) ||
            !int.TryParse(configuration["Email:Smtp4Dev:Port"], out var port) ||
            port is < 1 or > 65535 ||
            !Enum.TryParse(configuration["Email:Smtp4Dev:SecureSocketOptions"], true, out SecureSocketOptions securityMode) ||
            !Enum.IsDefined(securityMode))
        {
            return false;
        }

        var username = configuration["Email:Smtp4Dev:Username"];
        var password = configuration["Email:Smtp4Dev:Password"];
        return string.IsNullOrWhiteSpace(username) == string.IsNullOrWhiteSpace(password);
    }

    private static bool IsAzureConfigured(IConfiguration configuration) =>
        Uri.TryCreate(configuration["Email:Azure:Endpoint"], UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps;

    private static bool IsValidClientBaseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsValidAddress(string? value) =>
        MailAddress.TryCreate(value, out var address) && address.Address == value;
}
