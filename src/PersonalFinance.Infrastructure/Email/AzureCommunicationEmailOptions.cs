namespace PersonalFinance.Infrastructure.Email;

public sealed class AzureCommunicationEmailOptions
{
    public const string SectionName = "Email:Azure";

    public string Endpoint { get; set; } = string.Empty;
}
