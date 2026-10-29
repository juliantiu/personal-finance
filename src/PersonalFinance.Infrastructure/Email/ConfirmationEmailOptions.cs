namespace PersonalFinance.Infrastructure.Email;

public sealed class ConfirmationEmailOptions
{
    public const string SectionName = "Email";

    public string ClientBaseUrl { get; set; } = string.Empty;

    public string SenderAddress { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Personal Finance";
}
