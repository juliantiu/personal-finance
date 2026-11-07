namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class CurrencyRecord
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
}
