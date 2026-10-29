using MailKit.Security;

namespace PersonalFinance.Infrastructure.Email;

public sealed class Smtp4DevOptions
{
    public const string SectionName = "Email:Smtp4Dev";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 25;

    public SecureSocketOptions SecureSocketOptions { get; set; } = SecureSocketOptions.None;

    public string? Username { get; set; }

    public string? Password { get; set; }
}
