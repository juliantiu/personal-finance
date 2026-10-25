namespace PersonalFinance.Infrastructure.Email;

public sealed class DevelopmentEmailOptions
{
    public const string SectionName = "Email:Development";

    public string ClientBaseUrl { get; set; } = "http://localhost:5203/";
}
