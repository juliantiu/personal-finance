namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class ProfileCurrencyRecord
{
    public Guid ProfileId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}
