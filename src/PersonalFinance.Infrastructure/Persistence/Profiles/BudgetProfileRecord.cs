namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class BudgetProfileRecord
{
    public Guid Id { get; set; }
    public Guid OwnerAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MainCurrencyCode { get; set; } = string.Empty;
    public long CurrencySettingsVersion { get; set; }
}
