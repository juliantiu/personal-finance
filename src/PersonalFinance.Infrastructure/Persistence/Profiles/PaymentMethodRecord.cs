namespace PersonalFinance.Infrastructure.Persistence.Profiles;

public sealed class PaymentMethodRecord
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Kind { get; set; } = string.Empty;
}
